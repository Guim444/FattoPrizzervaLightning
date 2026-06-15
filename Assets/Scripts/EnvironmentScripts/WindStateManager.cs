using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Gestiona la ventisca con VideoPlayers fijos por clip.
///
/// Idea clave:
/// - Cada clip tiene su propio VideoPlayer.
/// - Todos los clips se asignan y se preparan una sola vez en Awake.
/// - Después, los players quedan corriendo.
/// - Al cambiar de estado NO se cambia clip, NO se llama Prepare, NO se llama Stop.
/// - Tampoco se hace seek/time=0/frame=0 en runtime.
/// - Solo se cambia targetCameraAlpha para decidir qué vídeo se ve.
///
/// Estructura por tecla:
///   Tecla 1 → IdleLoop_Fast              (snap inmediato, sin transición)
///   Tecla 2 → Transition_2 → IdleLoop_2
///   Tecla 3 → Transition_3 → IdleLoop_3
///   Tecla 4 → Transition_4 → IdleLoop_4
///
/// Mapeo wind → árboles:
///   W1 / W4 → fast
///   W2 / W3 → slow
/// </summary>
public class WindStateManager : MonoBehaviour
{
    // ── Enum público (mantiene compatibilidad con EnvironmentStateManager) ────

    public enum WindPreset { W1_MaxIdle = 0, W2_MaxToMedium = 1, W3_MediumToMin = 2, W4_MinToMedium = 3 }

    // ── Datos serializables por estado ────────────────────────────────────────

    [System.Serializable]
    public struct WindStateData
    {
        [Tooltip("Clip de transición (se reproduce visualmente una vez). Dejar vacío en W1.")]
        public VideoClip transitionClip;

        [Tooltip("Clip idle que se reproduce en loop tras la transición (o directamente en W1).")]
        public VideoClip idleLoopClip;

        [Tooltip("Emisión de partículas por segundo para este estado.")]
        public float emissionRate;

        [Range(0f, 1f)]
        [Tooltip("Opacidad del vídeo sobre la cámara durante la transición.")]
        public float transitionOpacity;

        [Range(0f, 1f)]
        [Tooltip("Opacidad del vídeo sobre la cámara durante el idle loop.")]
        public float idleOpacity;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Estados (0=W1, 1=W2, 2=W3, 3=W4)")]
    [SerializeField] private WindStateData[] windStates = new WindStateData[]
    {
        // W1: solo idle fast, sin transición
        new WindStateData { emissionRate = 4000f, transitionOpacity = 0f,   idleOpacity = 0.28f },
        // W2
        new WindStateData { emissionRate = 2000f, transitionOpacity = 0.20f, idleOpacity = 0.18f },
        // W3
        new WindStateData { emissionRate = 500f,  transitionOpacity = 0.12f, idleOpacity = 0.08f },
        // W4
        new WindStateData { emissionRate = 1000f, transitionOpacity = 0.16f, idleOpacity = 0.14f },
    };

    [Header("Cámara")]
    [Tooltip("Cámara sobre la que se dibuja el vídeo. Si queda vacía se busca CAM_Main o MainCamera.")]
    [SerializeField] private Camera blizzardVideoCamera;

    [Header("Árboles Alembic")]
    [SerializeField] private AlembicTreeWindController[] treeControllers;

    [Header("Particle System")]
    [SerializeField] private ParticleSystem blizzardParticles;

    [Header("Preload / Playback")]
    [Tooltip("Crea un VideoPlayer por clip en Awake y los deja preparados/corriendo.")]
    [SerializeField] private bool preloadPlayersOnAwake = true;

    [Tooltip("Nombre del contenedor runtime donde se crean los VideoPlayers.")]
    [SerializeField] private string videoPlayersRootName = "Runtime_VideoPlayers";

    // ── Estado interno ────────────────────────────────────────────────────────

    private int  _currentStateIndex = -1;
    private bool _cameraWasAssigned;
    private bool _missingCameraWarningShown;

    private Transform _playersRoot;
    private VideoPlayer[] _idlePlayers;
    private VideoPlayer[] _transitionPlayers;
    private readonly List<VideoPlayer> _allPlayers = new List<VideoPlayer>();

    private int   _activeIdleIndex       = -1;
    private int   _activeTransitionIndex = -1;
    private int   _pendingIdleAfterTransitionIndex = -1;
    private float _transitionEndsAtUnscaled = -1f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cameraWasAssigned = blizzardVideoCamera != null;

        ResolveMissingReferences();

        if (preloadPlayersOnAwake)
            BuildAndPrepareFixedPlayers();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Si otro manager llama SnapToState/TransitionTo en Awake, respetamos eso.
        // Si nadie llamó nada, dejamos W1 visible por defecto cuando exista.
        if (_currentStateIndex < 0 && windStates != null && windStates.Length > 0)
            SnapToState(WindPreset.W1_MaxIdle);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeAll();
        HideAllVideos();
        // No llamamos Stop() durante cambios de estado.
        // En OnDestroy ya no importa, pero tampoco hace falta detener explícitamente.
    }

    private void Update()
    {
        RefreshVideoCamera();
        UpdateTransitionTimer();
    }

    // ── API pública ────────────────────────────────────────────────────────────

    /// <summary>
    /// Tecla 1: salta directamente al idle fast sin transición.
    /// No detiene ni prepara players; solo cambia opacidades.
    /// </summary>
    public void SnapToState(WindPreset preset)
    {
        int idx = (int)preset;
        if (!IsValidStateIndex(idx)) return;

        _currentStateIndex = idx;
        _activeTransitionIndex = -1;
        _pendingIdleAfterTransitionIndex = -1;
        _transitionEndsAtUnscaled = -1f;

        SetEmission(windStates[idx].emissionRate);
        SetTreeWindSpeed(preset);

        ShowIdle(idx);
    }

    /// <summary>
    /// Teclas 2-4: muestra transición y, al cumplirse su duración,
    /// cambia al idle. No toca clip, no llama Prepare(), no llama Stop().
    /// </summary>
    public void TransitionTo(WindPreset preset, float _crossFadeOverride = -1f)
    {
        int idx = (int)preset;
        if (!IsValidStateIndex(idx)) return;

        if (idx == _currentStateIndex && _activeTransitionIndex < 0)
            return;

        _currentStateIndex = idx;

        SetEmission(windStates[idx].emissionRate);
        SetTreeWindSpeed(preset);

        WindStateData data = windStates[idx];
        VideoPlayer transition = GetTransitionPlayer(idx);

        // W1 es siempre idle directo. Aunque por accidente tenga transitionClip en el Inspector,
        // el estado inicial no debe mostrar transición.
        bool isInitialIdleState = idx == (int)WindPreset.W1_MaxIdle;

        // Si no hay transición asignada, o es W1, ir directo al idle.
        if (isInitialIdleState || data.transitionClip == null || transition == null)
        {
            ShowIdle(idx);
            return;
        }

        HideAllVideos();

        _activeIdleIndex = -1;
        _activeTransitionIndex = idx;
        _pendingIdleAfterTransitionIndex = idx;

        // No hacemos seek/restart aquí. time=0/frame=0 también puede congelar el decoder.
        // El player ya está corriendo en loop desde Awake; solo cambiamos alpha.
        // Aseguramos que esté corriendo sin preparar ni reasignar clip.
        if (!transition.isPlaying)
            transition.Play();

        float opacity = _crossFadeOverride >= 0f ? _crossFadeOverride : data.transitionOpacity;
        transition.targetCameraAlpha = opacity;

        _transitionEndsAtUnscaled = Time.unscaledTime + GetClipDurationSafe(transition);
    }

    // ── Preload de VideoPlayers fijos ─────────────────────────────────────────

    private void BuildAndPrepareFixedPlayers()
    {
        if (windStates == null || windStates.Length == 0)
        {
            Debug.LogWarning($"[{nameof(WindStateManager)}] No hay windStates configurados.", this);
            return;
        }

        ClearRuntimePlayers();

        _idlePlayers = new VideoPlayer[windStates.Length];
        _transitionPlayers = new VideoPlayer[windStates.Length];
        _allPlayers.Clear();

        _playersRoot = GetOrCreatePlayersRoot();

        for (int i = 0; i < windStates.Length; i++)
        {
            WindStateData data = windStates[i];

            if (data.idleLoopClip != null)
            {
                _idlePlayers[i] = CreateFixedPlayer(
                    $"VP_Idle_{i}_{SanitizeName(data.idleLoopClip.name)}",
                    data.idleLoopClip,
                    loop: true
                );
            }

            if (data.transitionClip != null)
            {
                // Lo dejamos en loop y corriendo también cuando está oculto.
                // Visualmente se muestra una sola pasada gracias al timer.
                _transitionPlayers[i] = CreateFixedPlayer(
                    $"VP_Transition_{i}_{SanitizeName(data.transitionClip.name)}",
                    data.transitionClip,
                    loop: true
                );
            }
        }
    }

    private VideoPlayer CreateFixedPlayer(string playerName, VideoClip clip, bool loop)
    {
        GameObject go = new GameObject(playerName);
        go.transform.SetParent(_playersRoot, false);

        VideoPlayer vp = go.AddComponent<VideoPlayer>();
        ConfigureBasePlayer(vp);

        vp.clip = clip;
        vp.isLooping = loop;
        vp.targetCameraAlpha = 0f;

        vp.prepareCompleted += OnFixedPlayerPrepared;
        vp.errorReceived += OnVideoError;

        AssignCameraToPlayer(vp);

        // Único Prepare permitido: al inicio.
        vp.Prepare();

        _allPlayers.Add(vp);
        return vp;
    }

    private Transform GetOrCreatePlayersRoot()
    {
        Transform existing = transform.Find(videoPlayersRootName);
        if (existing != null)
            return existing;

        GameObject go = new GameObject(videoPlayersRootName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

    private void ClearRuntimePlayers()
    {
        Transform root = transform.Find(videoPlayersRootName);
        if (root == null) return;

        if (Application.isPlaying)
            Destroy(root.gameObject);
        else
            DestroyImmediate(root.gameObject);
    }

    private void OnFixedPlayerPrepared(VideoPlayer source)
    {
        source.prepareCompleted -= OnFixedPlayerPrepared;

        // Todos quedan corriendo siempre. El visible se decide solo por alpha.
        if (!source.isPlaying)
            source.Play();

        // IMPORTANTE:
        // No poner source.targetCameraAlpha = 0 aquí.
        // Si Start() o un manager ya mostró el idle inicial antes de que termine Prepare(),
        // este callback llegaría después y apagaría el vídeo activo.
        ApplyCurrentVideoAlphas();
    }

    // ── Cambio visual por opacidad ────────────────────────────────────────────

    private void ShowIdle(int stateIdx)
    {
        if (!IsValidStateIndex(stateIdx)) return;

        HideAllVideos();

        VideoPlayer idle = GetIdlePlayer(stateIdx);
        if (idle == null)
        {
            Debug.LogWarning($"[{nameof(WindStateManager)}] Estado {stateIdx} no tiene idleLoopClip/VideoPlayer asignado.", this);
            return;
        }

        if (!idle.isPlaying)
            idle.Play();

        idle.targetCameraAlpha = windStates[stateIdx].idleOpacity;

        _activeIdleIndex = stateIdx;
        _activeTransitionIndex = -1;
        _pendingIdleAfterTransitionIndex = -1;
        _transitionEndsAtUnscaled = -1f;
    }

    private void HideAllVideos()
    {
        for (int i = 0; i < _allPlayers.Count; i++)
        {
            VideoPlayer player = _allPlayers[i];
            if (player != null)
                player.targetCameraAlpha = 0f;
        }
    }

    private void ApplyCurrentVideoAlphas()
    {
        HideAllVideos();

        if (_activeTransitionIndex >= 0)
        {
            VideoPlayer transition = GetTransitionPlayer(_activeTransitionIndex);
            if (transition != null)
            {
                float opacity = windStates[_activeTransitionIndex].transitionOpacity;
                transition.targetCameraAlpha = opacity;
            }

            return;
        }

        if (_activeIdleIndex >= 0)
        {
            VideoPlayer idle = GetIdlePlayer(_activeIdleIndex);
            if (idle != null)
                idle.targetCameraAlpha = windStates[_activeIdleIndex].idleOpacity;
        }
    }

    private void UpdateTransitionTimer()
    {
        if (_activeTransitionIndex < 0 || _transitionEndsAtUnscaled < 0f)
            return;

        if (Time.unscaledTime < _transitionEndsAtUnscaled)
            return;

        int idleIdx = _pendingIdleAfterTransitionIndex;

        if (_activeTransitionIndex >= 0)
        {
            VideoPlayer transition = GetTransitionPlayer(_activeTransitionIndex);
            if (transition != null)
                transition.targetCameraAlpha = 0f;
        }

        _activeTransitionIndex = -1;
        _pendingIdleAfterTransitionIndex = -1;
        _transitionEndsAtUnscaled = -1f;

        ShowIdle(idleIdx);
    }


    private float GetClipDurationSafe(VideoPlayer player)
    {
        if (player != null && player.clip != null && player.clip.length > 0.01)
        {
            float speed = Mathf.Max(0.01f, player.playbackSpeed);
            return (float)(player.clip.length / speed);
        }

        return 0.1f;
    }

    private VideoPlayer GetIdlePlayer(int stateIdx)
    {
        if (_idlePlayers == null || stateIdx < 0 || stateIdx >= _idlePlayers.Length)
            return null;

        return _idlePlayers[stateIdx];
    }

    private VideoPlayer GetTransitionPlayer(int stateIdx)
    {
        if (_transitionPlayers == null || stateIdx < 0 || stateIdx >= _transitionPlayers.Length)
            return null;

        return _transitionPlayers[stateIdx];
    }

    // ── Callbacks de VideoPlayer ───────────────────────────────────────────────

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogWarning($"[{nameof(WindStateManager)}] Error en VideoPlayer '{source.name}': {message}", this);
    }

    private void UnsubscribeAll()
    {
        for (int i = 0; i < _allPlayers.Count; i++)
        {
            VideoPlayer player = _allPlayers[i];
            if (player == null) continue;

            player.prepareCompleted -= OnFixedPlayerPrepared;
            player.errorReceived -= OnVideoError;
        }
    }

    // ── Configuración de VideoPlayer ──────────────────────────────────────────

    private void ConfigureBasePlayer(VideoPlayer player)
    {
        if (player == null) return;

        player.playOnAwake       = false;
        player.waitForFirstFrame = false;
        player.skipOnDrop        = true;
        player.audioOutputMode   = VideoAudioOutputMode.None;
        player.timeUpdateMode    = VideoTimeUpdateMode.UnscaledGameTime;
        player.renderMode        = VideoRenderMode.CameraNearPlane;
        player.aspectRatio       = VideoAspectRatio.FitOutside;
        player.playbackSpeed     = 1f;

        AssignCameraToPlayer(player);
    }

    private void AssignCameraToPlayer(VideoPlayer player)
    {
        if (player == null) return;

        Camera cam = _cameraWasAssigned ? blizzardVideoCamera : ResolveCamera();
        if (cam == null)
        {
            WarnMissingCamera();
            return;
        }

        blizzardVideoCamera = cam;
        player.targetCamera = cam;
        _missingCameraWarningShown = false;
    }

    private void AssignCameraToAllPlayers()
    {
        for (int i = 0; i < _allPlayers.Count; i++)
        {
            VideoPlayer player = _allPlayers[i];
            if (player != null)
                AssignCameraToPlayer(player);
        }
    }

    // ── Cámara ────────────────────────────────────────────────────────────────

    private void RefreshVideoCamera()
    {
        bool cameraOk = IsUsableCamera(blizzardVideoCamera);

        if (!cameraOk)
        {
            Camera resolved = ResolveCamera();
            if (resolved == null)
            {
                HideAllVideos();
                WarnMissingCamera();
                return;
            }

            blizzardVideoCamera = resolved;
            _missingCameraWarningShown = false;
        }

        for (int i = 0; i < _allPlayers.Count; i++)
        {
            VideoPlayer player = _allPlayers[i];
            if (player != null && player.targetCamera != blizzardVideoCamera)
                player.targetCamera = blizzardVideoCamera;
        }
    }

    private Camera ResolveCamera()
    {
        Camera fallback = null;

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsUsableCamera(cam)) continue;
            if (cam.name == "CAM_Main") return cam;
            if (cam.CompareTag("MainCamera")) fallback = cam;
            else if (fallback == null) fallback = cam;
        }

        return fallback;
    }

    private static bool IsUsableCamera(Camera cam)
        => cam != null && cam.isActiveAndEnabled && cam.targetTexture == null;

    private void WarnMissingCamera()
    {
        if (_missingCameraWarningShown) return;
        _missingCameraWarningShown = true;
        Debug.LogWarning($"[{nameof(WindStateManager)}] No se encontró una cámara activa válida para reproducir el vídeo.", this);
    }

    // ── Scene reload ──────────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResolveMissingReferences();
        AssignCameraToAllPlayers();
    }

    // ── Partículas & Árboles ──────────────────────────────────────────────────

    private void SetEmission(float rate)
    {
        if (blizzardParticles == null) return;
        var emission = blizzardParticles.emission;
        emission.rateOverTime = rate;
    }

    private void SetTreeWindSpeed(WindPreset preset)
    {
        bool fast = preset == WindPreset.W1_MaxIdle || preset == WindPreset.W4_MinToMedium;
        if (treeControllers == null) return;

        foreach (var tree in treeControllers)
            if (tree != null) tree.SetFast(fast);
    }

    // ── Auto-reparación de referencias ───────────────────────────────────────

    private void ResolveMissingReferences()
    {
        bool repaired = false;

        if (HasMissingReferences(treeControllers))
        {
            var matches = new List<AlembicTreeWindController>();
            foreach (var tree in Object.FindObjectsByType<AlembicTreeWindController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (tree != null && tree.HasAvailablePlayers) matches.Add(tree);

            treeControllers = matches.ToArray();
            repaired |= treeControllers.Length > 0;
        }

        if (blizzardParticles == null)
        {
            var matches = new List<ParticleSystem>();
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (ps.name == "SnowStorm") matches.Add(ps);

            if (matches.Count == 1)
            {
                blizzardParticles = matches[0];
                repaired = true;
            }
            else if (matches.Count > 1)
            {
                Debug.LogWarning($"[{nameof(WindStateManager)}] Hay varios ParticleSystem llamados SnowStorm. Asigna blizzardParticles manualmente.", this);
            }
        }

        if (repaired)
            Debug.LogWarning($"[{nameof(WindStateManager)}] Se repararon referencias perdidas. Revisa el Inspector para asignarlas de forma permanente.", this);
    }

    private bool IsValidStateIndex(int idx)
    {
        if (windStates == null || idx < 0 || idx >= windStates.Length)
        {
            Debug.LogWarning($"[{nameof(WindStateManager)}] Índice de viento inválido: {idx}.", this);
            return false;
        }

        return true;
    }

    private static bool HasMissingReferences<T>(T[] arr) where T : Object
    {
        if (arr == null || arr.Length == 0) return true;
        foreach (var item in arr) if (item == null) return true;
        return false;
    }

    private static string SanitizeName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "Clip";

        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_' && chars[i] != '-')
                chars[i] = '_';
        }

        return new string(chars);
    }
}

