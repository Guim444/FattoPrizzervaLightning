using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Video;

/// <summary>
/// Gestiona la ventisca con VideoPlayers fijos por clip.
///
/// Idea clave:
/// - Cada clip tiene su propio VideoPlayer.
/// - Los clips se asignan al crear el root, en Awake o al entrar en la iglesia.
/// - Después, los players quedan corriendo.
/// - Al cambiar de estado NO se cambia clip, NO se llama Prepare, NO se llama Stop.
/// - Tampoco se hace seek/time=0/frame=0 en runtime.
/// - Solo se cambia el alpha del material del quad para decidir qué vídeo se ve.
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

        [Range(0f, 1f)]
        [Tooltip("Opacidad del vídeo sobre la cámara durante la transición.")]
        public float transitionOpacity;

        [Range(0f, 1f)]
        [Tooltip("Opacidad del vídeo sobre la cámara durante el idle loop.")]
        public float idleOpacity;

        [HideInInspector] public float videoOpacity;
    }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Estados (0=W1, 1=W2, 2=W3, 3=W4)")]
    [SerializeField] private WindStateData[] windStates = new WindStateData[]
    {
        // W1: solo idle fast, sin transición
        new WindStateData { transitionOpacity = 0f,   idleOpacity = 0.36f },
        // W2
        new WindStateData { transitionOpacity = 0.24f, idleOpacity = 0.22f },
        // W3
        new WindStateData { transitionOpacity = 0.12f, idleOpacity = 0.08f },
        // W4
        new WindStateData { transitionOpacity = 0.18f, idleOpacity = 0.16f },
    };

    [Header("Cámara")]
    [Tooltip("Cámara sobre la que se dibuja el vídeo. Si queda vacía se busca CAM_Main o MainCamera.")]
    [SerializeField] private Camera blizzardVideoCamera;

    [Header("Árboles Alembic")]
    [SerializeField] private AlembicTreeWindController[] treeControllers;
    [Tooltip("Desfase en segundos entre cada TreeController del array. 0.3 = cada árbol empieza 0.3s más adelante que el anterior.")]
    [SerializeField, Min(0f)] private float treePlaybackOffsetStep = 0.3f;

    [Header("Alembic standalone")]
    [Tooltip("Reproduce en loop el Alembic de la planta colocado en escena como Planta_MJ.")]
    [SerializeField] private bool playPlantaMjAlembic = true;
    [SerializeField, Min(0f)] private float plantaMjPlaybackSpeed = 1f;

    [Header("Preload / Playback")]
    [Tooltip("Crea el root y sus VideoPlayers en Awake. Si se desactiva, se crean al entrar en la iglesia.")]
    [SerializeField] private bool preloadPlayersOnAwake = true;

    [Tooltip("Nombre del contenedor runtime donde se crean los VideoPlayers.")]
    [SerializeField] private string videoPlayersRootName = "Runtime_VideoPlayers";

    [Tooltip("Si está desactivado, los vídeos permanecen inactivos hasta entrar en la iglesia.")]
    [SerializeField] private bool videoPlayersRootStartsActive = false;

    [Tooltip("Mantiene los VideoPlayers activos con alpha 0 para evitar tirones al mostrarlos.")]
    [SerializeField] private bool prewarmPlayersWhileHidden = true;

    [Header("Video Surface")]
    [Tooltip("0 usa la resolucion del clip. Usar un valor fijo solo si todos los videos comparten formato.")]
    [SerializeField, Min(0)] private int renderTextureWidth = 0;
    [Tooltip("0 usa la resolucion del clip. Usar un valor fijo solo si todos los videos comparten formato.")]
    [SerializeField, Min(0)] private int renderTextureHeight = 0;
    [SerializeField, Min(0.01f)] private float quadCameraDistance = 3f;
    [Tooltip("Offset local del quad respecto a la camara. X desplaza horizontal, Y vertical, Z acerca/aleja junto con la distancia.")]
    [SerializeField] private Vector3 quadCameraLocalOffset = Vector3.zero;
    [SerializeField, Min(1f)] private float quadViewportOverscan = 1.12f;
    [Tooltip("Compensa el movimiento horizontal de la camara desplazando las UVs del video. 0 = pegado a pantalla, 1 = mayor sensacion de mundo.")]
    [SerializeField, Range(0f, 1f)] private float horizontalMotionCompensation = 0.35f;
    [Tooltip("Cuantos metros horizontales de camara equivalen a una repeticion completa del video.")]
    [SerializeField, Min(0.01f)] private float horizontalWorldUnitsPerTextureRepeat = 24f;
    [Tooltip("Rendering Layer Mask del quad de ventisca. Todos=4294967295, Default=1, Light Layer 1=2, Light Layer 2=4.")]
    [SerializeField] private uint blizzardRenderingLayerMask = uint.MaxValue;

    [Header("Video Surface - Idle Background Copy")]
    [SerializeField] private bool enableIdleBackgroundLayer = true;
    [SerializeField, Min(0.01f)] private float idleBackgroundWidth = 42f;
    [SerializeField, Min(0.01f)] private float idleBackgroundHeight = 24f;
    [SerializeField, Min(0.01f)] private float idleBackgroundDepth = 18f;
    [SerializeField] private Vector2 idleBackgroundOffset = Vector2.zero;
    [SerializeField, Range(0f, 1f)] private float idleBackgroundOpacityMultiplier = 0.35f;
    [SerializeField, Range(0f, 1f)] private float idleBackgroundHorizontalMotionCompensation = 0.12f;

    [Header("Compatibilidad")]
    [SerializeField, HideInInspector] private VideoClip blizzardVideoClip;
    [SerializeField, HideInInspector] private VideoPlayer blizzardVideoPlayer;

    // ── Estado interno ────────────────────────────────────────────────────────

    private int  _currentStateIndex = -1;
    private bool _cameraWasAssigned;
    private bool _missingCameraWarningShown;

    private Transform _playersRoot;
    private VideoPlayer[] _idlePlayers;
    private VideoPlayer[] _transitionPlayers;
    private readonly List<VideoPlayer> _allPlayers = new List<VideoPlayer>();
    private readonly Dictionary<VideoPlayer, VideoSurface> _videoSurfaces = new Dictionary<VideoPlayer, VideoSurface>();
    private readonly Dictionary<VideoPlayer, VideoSurface> _idleBackgroundSurfaces = new Dictionary<VideoPlayer, VideoSurface>();

    private int   _activeIdleIndex       = -1;
    private int   _activeTransitionIndex = -1;
    private int   _pendingIdleAfterTransitionIndex = -1;
    private float _transitionEndsAtUnscaled = -1f;
    private float _activeTransitionOpacity = 0f;
    private bool  _videoPlayersVisible;
    private bool  _hasHorizontalMotionReference;
    private float _horizontalMotionReferenceX;
    private Camera _horizontalMotionReferenceCamera;
    private readonly List<AlembicStreamPlayer> _plantaMjPlayers = new List<AlembicStreamPlayer>();
    private readonly List<float> _plantaMjTimes = new List<float>();
    private bool _plantaMjAutoPlaybackReported;

    private sealed class VideoSurface
    {
        public GameObject quad;
        public MeshRenderer renderer;
        public Mesh mesh;
        public Material material;
        public RenderTexture texture;
        public bool ownsTexture;
        public bool usesFixedCameraPlane;
        public float fixedWidth;
        public float fixedHeight;
        public float fixedDepth;
        public Vector2 fixedOffset;
        public float horizontalMotionCompensation;
        public readonly Vector3[] vertices = new Vector3[4];
        public readonly Vector2[] uvs = new Vector2[4];
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cameraWasAssigned = blizzardVideoCamera != null;

        ResolveMissingReferences();
        ApplyTreePlaybackOffsets();
        ResolvePlantaMjAlembicPlayers();

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

        // Mantiene el alpha correcto aunque algún prepareCompleted tardío,
        // recarga de escena o llamada repetida apague el player activo.
        MaintainActiveVideoAlpha();

        AdvancePlantaMjAlembicPlayers();
    }

    private void LateUpdate()
    {
        UpdateVideoSurfaces();
    }

    private void OnEnable()
    {
        Camera.onPreCull += HandleCameraPreCull;
    }

    private void OnDisable()
    {
        Camera.onPreCull -= HandleCameraPreCull;
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
        _activeTransitionOpacity = 0f;

        SetTreeWindSpeed(preset);
        ResetHorizontalMotionReference();

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
        {
            // Si el manager externo llama TransitionTo(W1) en vez de SnapToState(W1),
            // no salimos sin hacer nada: re-aplicamos el idle visible.
            ShowIdle(idx);
            return;
        }

        _currentStateIndex = idx;

        SetTreeWindSpeed(preset);
        ResetHorizontalMotionReference();

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
        if (CanRunVideoPlayers && !transition.isPlaying)
            transition.Play();

        float opacity = _crossFadeOverride >= 0f ? _crossFadeOverride : GetTransitionOpacity(idx);
        _activeTransitionOpacity = opacity;
        SetVideoOpacity(transition, ShouldShowVideos ? opacity : 0f);

        _transitionEndsAtUnscaled = Time.unscaledTime + GetClipDurationSafe(transition);
    }

    public static int ActivateAllVideoPlayersRoots()
    {
        WindStateManager[] managers = Object.FindObjectsByType<WindStateManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (WindStateManager manager in managers)
            manager.ActivateVideoPlayersRoot();

        return managers.Length;
    }

    public static int PrewarmAllVideoPlayersHidden()
    {
        WindStateManager[] managers = Object.FindObjectsByType<WindStateManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (WindStateManager manager in managers)
            manager.PrewarmVideoPlayersHidden();

        return managers.Length;
    }

    public void ActivateVideoPlayersRoot()
    {
        if (_playersRoot == null)
            BuildAndPrepareFixedPlayers();

        if (_playersRoot == null)
        {
            Debug.LogWarning($"[{nameof(WindStateManager)}] No existe el root de VideoPlayers.", this);
            return;
        }

        _playersRoot.gameObject.SetActive(true);
        _videoPlayersVisible = true;
        RefreshVideoCamera();
        ResetHorizontalMotionReference();

        foreach (VideoPlayer player in _allPlayers)
        {
            if (player == null) continue;

            AssignCameraToPlayer(player);

            if (player.isPrepared)
                player.Play();
            else
                player.Prepare();
        }

        ActivateLegacyVideoPlayer();
        ApplyCurrentVideoAlphas();
    }

    public void PrewarmVideoPlayersHidden()
    {
        bool wasVisible = _videoPlayersVisible;

        if (_playersRoot == null)
            BuildAndPrepareFixedPlayers();

        if (_playersRoot == null)
            return;

        _playersRoot.gameObject.SetActive(true);
        _videoPlayersVisible = false;
        RefreshVideoCamera();
        ResetHorizontalMotionReference();

        foreach (VideoPlayer player in _allPlayers)
        {
            if (player == null) continue;

            AssignCameraToPlayer(player);

            if (player.isPrepared)
            {
                if (!player.isPlaying)
                    player.Play();
            }
            else
            {
                player.Prepare();
            }
        }

        _videoPlayersVisible = wasVisible;
        ApplyCurrentVideoAlphas();
        PrewarmPlantaMjAlembicPlayers(0.05f);
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
        _idleBackgroundSurfaces.Clear();

        _playersRoot = GetOrCreatePlayersRoot();
        _videoPlayersVisible = videoPlayersRootStartsActive;
        _playersRoot.gameObject.SetActive(videoPlayersRootStartsActive || prewarmPlayersWhileHidden);

        for (int i = 0; i < windStates.Length; i++)
        {
            WindStateData data = windStates[i];
            VideoClip idleClip = GetConfiguredIdleClip(i);

            if (idleClip != null)
            {
                _idlePlayers[i] = CreateFixedPlayer(
                    $"VP_Idle_{i}_{SanitizeName(idleClip.name)}",
                    idleClip,
                    loop: true,
                    createIdleBackgroundLayer: true
                );
            }

            if (data.transitionClip != null)
            {
                // Lo dejamos en loop y corriendo también cuando está oculto.
                // Visualmente se muestra una sola pasada gracias al timer.
                _transitionPlayers[i] = CreateFixedPlayer(
                    $"VP_Transition_{i}_{SanitizeName(data.transitionClip.name)}",
                    data.transitionClip,
                    loop: true,
                    createIdleBackgroundLayer: false
                );
            }
        }
    }

    private VideoPlayer CreateFixedPlayer(string playerName, VideoClip clip, bool loop, bool createIdleBackgroundLayer)
    {
        GameObject go = new GameObject(playerName);
        go.transform.SetParent(_playersRoot, false);

        VideoPlayer vp = go.AddComponent<VideoPlayer>();
        ConfigureBasePlayer(vp);

        vp.clip = clip;
        vp.isLooping = loop;
        CreateVideoSurface(vp, playerName, clip, createIdleBackgroundLayer);
        SetVideoOpacity(vp, 0f);

        vp.prepareCompleted += OnFixedPlayerPrepared;
        vp.errorReceived += OnVideoError;

        AssignCameraToPlayer(vp);

        if (CanRunVideoPlayers)
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
        ReleaseVideoSurfaces();

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

        if (!CanRunVideoPlayers)
            return;

        // Todos quedan corriendo siempre. El visible se decide solo por alpha.
        if (!source.isPlaying)
            source.Play();

        // IMPORTANTE:
        // No poner el alpha del material a 0 aquí.
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

        if (CanRunVideoPlayers && !idle.isPlaying)
            idle.Play();

        SetVideoOpacity(idle, ShouldShowVideos ? GetIdleOpacity(stateIdx) : 0f);

        _activeIdleIndex = stateIdx;
        _activeTransitionIndex = -1;
        _pendingIdleAfterTransitionIndex = -1;
        _transitionEndsAtUnscaled = -1f;
        _activeTransitionOpacity = 0f;
    }

    private void HideAllVideos()
    {
        for (int i = 0; i < _allPlayers.Count; i++)
        {
            VideoPlayer player = _allPlayers[i];
            if (player != null)
                SetVideoOpacity(player, 0f);
        }
    }

    private void ApplyCurrentVideoAlphas()
    {
        HideAllVideos();

        if (!ShouldShowVideos)
            return;

        if (_activeTransitionIndex >= 0)
        {
            VideoPlayer transition = GetTransitionPlayer(_activeTransitionIndex);
            if (transition != null)
            {
                float opacity = _activeTransitionOpacity > 0f
                    ? _activeTransitionOpacity
                    : GetTransitionOpacity(_activeTransitionIndex);

                SetVideoOpacity(transition, opacity);
            }

            return;
        }

        if (_activeIdleIndex >= 0)
        {
            VideoPlayer idle = GetIdlePlayer(_activeIdleIndex);
            if (idle != null)
                SetVideoOpacity(idle, GetIdleOpacity(_activeIdleIndex));
        }
    }

    private void MaintainActiveVideoAlpha()
    {
        if (!CanRunVideoPlayers)
            return;

        MaintainLegacyVideoPlayer();

        if (_activeTransitionIndex >= 0)
        {
            VideoPlayer activeTransition = GetTransitionPlayer(_activeTransitionIndex);

            for (int i = 0; i < _allPlayers.Count; i++)
            {
                VideoPlayer player = _allPlayers[i];
                if (player == null) continue;

                SetVideoOpacity(player, ShouldShowVideos && player == activeTransition ? _activeTransitionOpacity : 0f);

                if (player == activeTransition && !player.isPlaying)
                    player.Play();
            }

            return;
        }

        if (_activeIdleIndex >= 0)
        {
            VideoPlayer activeIdle = GetIdlePlayer(_activeIdleIndex);
            float idleOpacity = IsValidStateIndex(_activeIdleIndex) ? GetIdleOpacity(_activeIdleIndex) : 0f;

            for (int i = 0; i < _allPlayers.Count; i++)
            {
                VideoPlayer player = _allPlayers[i];
                if (player == null) continue;

                SetVideoOpacity(player, ShouldShowVideos && player == activeIdle ? idleOpacity : 0f);

                if (player == activeIdle && !player.isPlaying)
                    player.Play();
            }
        }
    }

    private void ActivateLegacyVideoPlayer()
    {
        if (blizzardVideoPlayer == null) return;

        ConfigureBasePlayer(blizzardVideoPlayer);

        if (blizzardVideoPlayer.clip == null && blizzardVideoClip != null)
            blizzardVideoPlayer.clip = blizzardVideoClip;

        if (blizzardVideoPlayer.clip == null) return;

        if (blizzardVideoPlayer.isPrepared)
            blizzardVideoPlayer.Play();
        else
            blizzardVideoPlayer.Prepare();

        MaintainLegacyVideoPlayer();
    }

    private void MaintainLegacyVideoPlayer()
    {
        if (blizzardVideoPlayer == null || _allPlayers.Count > 0) return;

        int stateIdx = _activeIdleIndex >= 0 ? _activeIdleIndex : 0;
        blizzardVideoPlayer.targetCameraAlpha = ShouldShowVideos ? GetIdleOpacity(stateIdx) : 0f;

        if (CanRunVideoPlayers && blizzardVideoPlayer.clip != null && blizzardVideoPlayer.isPrepared && !blizzardVideoPlayer.isPlaying)
            blizzardVideoPlayer.Play();
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
                SetVideoOpacity(transition, 0f);
        }

        _activeTransitionIndex = -1;
        _pendingIdleAfterTransitionIndex = -1;
        _transitionEndsAtUnscaled = -1f;
        _activeTransitionOpacity = 0f;

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

    private VideoClip GetConfiguredIdleClip(int stateIdx)
    {
        if (!IsValidStateIndex(stateIdx)) return null;

        VideoClip clip = windStates[stateIdx].idleLoopClip;
        return clip != null ? clip : blizzardVideoClip;
    }

    private float GetIdleOpacity(int stateIdx)
    {
        if (!IsValidStateIndex(stateIdx)) return 0f;

        float opacity = windStates[stateIdx].idleOpacity;
        if (opacity <= 0f && windStates[stateIdx].videoOpacity > 0f)
            opacity = windStates[stateIdx].videoOpacity;

        return opacity;
    }

    private float GetTransitionOpacity(int stateIdx)
    {
        if (!IsValidStateIndex(stateIdx)) return 0f;

        float opacity = windStates[stateIdx].transitionOpacity;
        if (opacity <= 0f && windStates[stateIdx].videoOpacity > 0f)
            opacity = windStates[stateIdx].videoOpacity;

        return opacity;
    }

    private VideoPlayer GetTransitionPlayer(int stateIdx)
    {
        if (_transitionPlayers == null || stateIdx < 0 || stateIdx >= _transitionPlayers.Length)
            return null;

        return _transitionPlayers[stateIdx];
    }

    private bool CanRunVideoPlayers =>
        _playersRoot != null && _playersRoot.gameObject.activeInHierarchy;

    private bool ShouldShowVideos =>
        CanRunVideoPlayers && _videoPlayersVisible;

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
        player.renderMode        = VideoRenderMode.RenderTexture;
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
        ApplyTreePlaybackOffsets();
        ResolvePlantaMjAlembicPlayers();
        AssignCameraToAllPlayers();
    }

    // ── Superficie de vídeo ───────────────────────────────────────────────────

    private void CreateVideoSurface(VideoPlayer player, string playerName, VideoClip clip, bool createIdleBackgroundLayer)
    {
        if (player == null) return;

        int width = renderTextureWidth > 0 ? renderTextureWidth : GetClipWidthSafe(clip);
        int height = renderTextureHeight > 0 ? renderTextureHeight : GetClipHeightSafe(clip);

        RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = $"{playerName}_RT",
            useMipMap = false,
            autoGenerateMips = false
        };
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;
        texture.Create();

        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = texture;

        VideoSurface foreground = CreateVideoSurfaceQuad(
            player.transform,
            $"{playerName}_Quad",
            $"{playerName}_Mesh",
            $"{playerName}_Material",
            texture);

        if (foreground == null)
        {
            texture.Release();
            Destroy(texture);
            return;
        }

        foreground.texture = texture;
        foreground.ownsTexture = true;
        foreground.horizontalMotionCompensation = horizontalMotionCompensation;
        _videoSurfaces[player] = foreground;

        if (!createIdleBackgroundLayer || !enableIdleBackgroundLayer)
            return;

        VideoSurface background = CreateVideoSurfaceQuad(
            player.transform,
            $"{playerName}_BackgroundQuad",
            $"{playerName}_BackgroundMesh",
            $"{playerName}_BackgroundMaterial",
            texture);

        if (background == null)
            return;

        background.texture = texture;
        background.ownsTexture = false;
        background.usesFixedCameraPlane = true;
        background.fixedWidth = idleBackgroundWidth;
        background.fixedHeight = idleBackgroundHeight;
        background.fixedDepth = idleBackgroundDepth;
        background.fixedOffset = idleBackgroundOffset;
        background.horizontalMotionCompensation = idleBackgroundHorizontalMotionCompensation;
        if (background.material != null)
            background.material.renderQueue = (int)RenderQueue.Transparent + 5;
        _idleBackgroundSurfaces[player] = background;
    }

    private VideoSurface CreateVideoSurfaceQuad(Transform parent, string objectName, string meshName, string materialName, Texture texture)
    {
        Shader surfaceShader = ResolveVideoSurfaceShader();
        if (surfaceShader == null)
        {
            Debug.LogWarning($"[{nameof(WindStateManager)}] No se encontro un shader compatible para mostrar la ventisca en un quad.", this);
            return null;
        }

        GameObject quad = new GameObject(objectName);
        quad.transform.SetParent(parent, false);

        MeshFilter meshFilter = quad.AddComponent<MeshFilter>();
        MeshRenderer renderer = quad.AddComponent<MeshRenderer>();
        Mesh mesh = CreateVideoSurfaceMesh(meshName);
        meshFilter.sharedMesh = mesh;

        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.renderingLayerMask = blizzardRenderingLayerMask;

        Material material = new Material(surfaceShader)
        {
            name = materialName,
            renderQueue = (int)RenderQueue.Transparent + 10
        };

        ConfigureVideoSurfaceMaterial(material, texture);
        renderer.sharedMaterial = material;

        return new VideoSurface
        {
            quad = quad,
            renderer = renderer,
            mesh = mesh,
            material = material
        };
    }

    private Mesh CreateVideoSurfaceMesh(string playerName)
    {
        Mesh mesh = new Mesh
        {
            name = $"{playerName}_Mesh"
        };

        mesh.vertices = new[]
        {
            Vector3.zero,
            Vector3.up,
            Vector3.one,
            Vector3.right
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private Shader ResolveVideoSurfaceShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) return shader;

        shader = Shader.Find("Unlit/Transparent");
        if (shader != null) return shader;

        return Shader.Find("Sprites/Default");
    }

    private void ConfigureVideoSurfaceMaterial(Material material, Texture texture)
    {
        if (material == null) return;

        SetMaterialTexture(material, "_BaseMap", texture);
        SetMaterialTexture(material, "_MainTex", texture);

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_ZTest")) material.SetFloat("_ZTest", (float)CompareFunction.Always);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        SetSurfaceMaterialAlpha(material, 0f);
    }

    private void SetVideoOpacity(VideoPlayer player, float opacity)
    {
        if (player == null) return;

        float alpha = Mathf.Clamp01(opacity);
        player.targetCameraAlpha = 0f;

        if (!_videoSurfaces.TryGetValue(player, out VideoSurface surface) || surface == null)
            return;

        SetSurfaceOpacity(surface, alpha);

        if (_idleBackgroundSurfaces.TryGetValue(player, out VideoSurface backgroundSurface))
            SetSurfaceOpacity(backgroundSurface, alpha * Mathf.Clamp01(idleBackgroundOpacityMultiplier));
    }

    private void SetSurfaceOpacity(VideoSurface surface, float alpha)
    {
        if (surface == null) return;

        if (surface.renderer != null)
        {
            surface.renderer.enabled = alpha > 0.001f;
            surface.renderer.renderingLayerMask = blizzardRenderingLayerMask;
        }

        SetSurfaceMaterialAlpha(surface.material, alpha);
    }

    private void SetSurfaceMaterialAlpha(Material material, float alpha)
    {
        if (material == null) return;

        Color color = Color.white;
        if (material.HasProperty("_BaseColor"))
            color = material.GetColor("_BaseColor");
        else if (material.HasProperty("_Color"))
            color = material.GetColor("_Color");

        color.a = Mathf.Clamp01(alpha);

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private void SetMaterialTexture(Material material, string propertyName, Texture texture)
    {
        if (material == null || texture == null || !material.HasProperty(propertyName)) return;
        material.SetTexture(propertyName, texture);
    }

    private void UpdateVideoSurfaces()
    {
        if (blizzardVideoCamera == null) return;

        EnsureHorizontalMotionReference();

        foreach (VideoSurface surface in _videoSurfaces.Values)
            PositionVideoSurface(surface);

        foreach (VideoSurface surface in _idleBackgroundSurfaces.Values)
            PositionVideoSurface(surface);
    }

    private void HandleCameraPreCull(Camera cam)
    {
        if (cam == null || cam != blizzardVideoCamera) return;
        UpdateVideoSurfaces();
    }

    private void PositionVideoSurface(VideoSurface surface)
    {
        if (surface == null || surface.quad == null || surface.mesh == null || blizzardVideoCamera == null) return;

        Transform cameraTransform = blizzardVideoCamera.transform;
        Transform quadTransform = surface.quad.transform;

        quadTransform.position = cameraTransform.position;
        quadTransform.rotation = cameraTransform.rotation;
        quadTransform.localScale = Vector3.one;

        if (surface.usesFixedCameraPlane)
            PositionFixedCameraPlaneSurface(surface);
        else
            PositionViewportSurface(surface, quadTransform);

        surface.mesh.vertices = surface.vertices;
        UpdateSurfaceUvs(surface);
        surface.mesh.RecalculateBounds();
    }

    private void PositionViewportSurface(VideoSurface surface, Transform quadTransform)
    {
        float distance = Mathf.Max(0.01f, quadCameraDistance);
        float overscan = Mathf.Max(1f, quadViewportOverscan);
        float margin = (overscan - 1f) * 0.5f;

        surface.vertices[0] = GetViewportCornerLocal(quadTransform, -margin, -margin, distance) + quadCameraLocalOffset;
        surface.vertices[1] = GetViewportCornerLocal(quadTransform, -margin, 1f + margin, distance) + quadCameraLocalOffset;
        surface.vertices[2] = GetViewportCornerLocal(quadTransform, 1f + margin, 1f + margin, distance) + quadCameraLocalOffset;
        surface.vertices[3] = GetViewportCornerLocal(quadTransform, 1f + margin, -margin, distance) + quadCameraLocalOffset;
    }

    private void PositionFixedCameraPlaneSurface(VideoSurface surface)
    {
        float halfWidth = Mathf.Max(0.01f, surface.fixedWidth) * 0.5f;
        float halfHeight = Mathf.Max(0.01f, surface.fixedHeight) * 0.5f;
        float depth = Mathf.Max(0.01f, surface.fixedDepth);
        Vector3 center = new Vector3(surface.fixedOffset.x, surface.fixedOffset.y, depth);

        surface.vertices[0] = center + new Vector3(-halfWidth, -halfHeight, 0f);
        surface.vertices[1] = center + new Vector3(-halfWidth,  halfHeight, 0f);
        surface.vertices[2] = center + new Vector3( halfWidth,  halfHeight, 0f);
        surface.vertices[3] = center + new Vector3( halfWidth, -halfHeight, 0f);
    }

    private void UpdateSurfaceUvs(VideoSurface surface)
    {
        float uOffset = GetHorizontalUvOffset(surface);

        surface.uvs[0] = new Vector2(uOffset, 0f);
        surface.uvs[1] = new Vector2(uOffset, 1f);
        surface.uvs[2] = new Vector2(1f + uOffset, 1f);
        surface.uvs[3] = new Vector2(1f + uOffset, 0f);

        surface.mesh.uv = surface.uvs;
    }

    private float GetHorizontalUvOffset(VideoSurface surface)
    {
        if (surface == null || blizzardVideoCamera == null || !_hasHorizontalMotionReference)
            return 0f;

        float compensation = Mathf.Clamp01(surface.horizontalMotionCompensation);
        if (compensation <= 0f) return 0f;

        float worldUnitsPerRepeat = Mathf.Max(0.01f, horizontalWorldUnitsPerTextureRepeat);
        float cameraDeltaX = blizzardVideoCamera.transform.position.x - _horizontalMotionReferenceX;
        return cameraDeltaX / worldUnitsPerRepeat * compensation;
    }

    private void EnsureHorizontalMotionReference()
    {
        if (blizzardVideoCamera == null) return;

        if (_hasHorizontalMotionReference && _horizontalMotionReferenceCamera == blizzardVideoCamera)
            return;

        ResetHorizontalMotionReference();
    }

    private void ResetHorizontalMotionReference()
    {
        if (blizzardVideoCamera == null)
        {
            _hasHorizontalMotionReference = false;
            _horizontalMotionReferenceCamera = null;
            return;
        }

        _horizontalMotionReferenceX = blizzardVideoCamera.transform.position.x;
        _horizontalMotionReferenceCamera = blizzardVideoCamera;
        _hasHorizontalMotionReference = true;
    }

    private Vector3 GetViewportCornerLocal(Transform surfaceTransform, float viewportX, float viewportY, float distance)
    {
        Vector3 worldPosition = blizzardVideoCamera.ViewportToWorldPoint(new Vector3(viewportX, viewportY, distance));
        return surfaceTransform.InverseTransformPoint(worldPosition);
    }

    private void ReleaseVideoSurfaces()
    {
        foreach (VideoSurface surface in _videoSurfaces.Values)
            ReleaseVideoSurface(surface);

        foreach (VideoSurface surface in _idleBackgroundSurfaces.Values)
            ReleaseVideoSurface(surface);

        _videoSurfaces.Clear();
        _idleBackgroundSurfaces.Clear();
    }

    private void ReleaseVideoSurface(VideoSurface surface)
    {
        if (surface == null) return;

        if (surface.ownsTexture && surface.texture != null)
        {
            surface.texture.Release();
            Destroy(surface.texture);
        }

        if (surface.material != null)
            Destroy(surface.material);

        if (surface.mesh != null)
            Destroy(surface.mesh);
    }

    private int GetClipWidthSafe(VideoClip clip)
        => clip != null && clip.width > 0 ? (int)clip.width : 1920;

    private int GetClipHeightSafe(VideoClip clip)
        => clip != null && clip.height > 0 ? (int)clip.height : 1080;

    // ── Árboles ───────────────────────────────────────────────────────────────

    private void SetTreeWindSpeed(WindPreset preset)
    {
        bool fast = preset == WindPreset.W1_MaxIdle || preset == WindPreset.W4_MinToMedium;
        if (treeControllers == null) return;

        foreach (var tree in treeControllers)
            if (tree != null) tree.SetFast(fast);
    }

    private void ApplyTreePlaybackOffsets()
    {
        if (treeControllers == null)
            return;

        float offsetStep = Mathf.Max(0f, treePlaybackOffsetStep);
        for (int i = 0; i < treeControllers.Length; i++)
        {
            AlembicTreeWindController tree = treeControllers[i];
            if (tree != null)
                tree.SetPlaybackOffset(i * offsetStep);
        }
    }

    // ── Alembic standalone ───────────────────────────────────────────────────

    private void ResolvePlantaMjAlembicPlayers()
    {
        _plantaMjPlayers.Clear();
        _plantaMjTimes.Clear();

        if (!playPlantaMjAlembic)
            return;

        foreach (var player in Object.FindObjectsByType<AlembicStreamPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (player == null) continue;
            if (player.GetComponentInParent<AlembicTreeWindController>(true) != null) continue;
            if (!IsInsideNamedHierarchy(player.transform, "planta_mj")) continue;

            _plantaMjPlayers.Add(player);
            _plantaMjTimes.Add(GetClampedAlembicTime(player));
        }

        if (_plantaMjPlayers.Count > 0 && !_plantaMjAutoPlaybackReported)
        {
            _plantaMjAutoPlaybackReported = true;
            Debug.Log($"[{nameof(WindStateManager)}] Planta_MJ Alembic enlazado para reproducción en loop: {_plantaMjPlayers.Count} player(s).", this);
        }
    }

    private void AdvancePlantaMjAlembicPlayers()
    {
        if (!playPlantaMjAlembic || _plantaMjPlayers.Count == 0)
            return;

        float speed = Mathf.Max(0f, plantaMjPlaybackSpeed);
        if (speed <= 0f)
            return;

        for (int i = _plantaMjPlayers.Count - 1; i >= 0; i--)
        {
            AlembicStreamPlayer player = _plantaMjPlayers[i];
            if (player == null)
            {
                _plantaMjPlayers.RemoveAt(i);
                _plantaMjTimes.RemoveAt(i);
                continue;
            }

            float duration = player.Duration;
            if (duration <= 0f) continue;

            float time = _plantaMjTimes[i] + Time.deltaTime * speed;
            if (time > duration)
                time %= duration;

            _plantaMjTimes[i] = time;
            player.CurrentTime = time;
        }
    }

    private void PrewarmPlantaMjAlembicPlayers(float sampleTime)
    {
        if (!playPlantaMjAlembic)
            return;

        if (_plantaMjPlayers.Count == 0)
            ResolvePlantaMjAlembicPlayers();

        for (int i = 0; i < _plantaMjPlayers.Count; i++)
        {
            AlembicStreamPlayer player = _plantaMjPlayers[i];
            if (player == null) continue;

            float duration = player.Duration;
            if (duration <= 0f) continue;

            float time = Mathf.Clamp(sampleTime, 0f, duration);
            _plantaMjTimes[i] = time;
            player.CurrentTime = time;
        }
    }

    private static float GetClampedAlembicTime(AlembicStreamPlayer player)
    {
        if (player == null) return 0f;

        float duration = player.Duration;
        if (duration <= 0f) return 0f;

        return Mathf.Clamp((float)player.CurrentTime, 0f, duration);
    }

    private static bool IsInsideNamedHierarchy(Transform candidate, string normalizedName)
    {
        while (candidate != null)
        {
            if (candidate.name.ToLowerInvariant().Contains(normalizedName))
                return true;

            candidate = candidate.parent;
        }

        return false;
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
