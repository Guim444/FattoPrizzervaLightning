using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.Video;

/// <summary>
/// Gestiona la ventisca con VideoPlayers fijos por clip.
///
/// Idea clave:
/// - Cada clip tiene su propio VideoPlayer.
/// - Los clips se asignan al crear el root, en Awake o al entrar en la iglesia.
/// - Todos los players se preparan una vez y los que no están visibles quedan pausados.
/// - Al cambiar de estado NO se cambia clip, NO se llama Prepare, NO se llama Stop.
/// - Tampoco se hace seek/time=0/frame=0 en runtime.
/// - Al cambiar de estado se pausa el anterior, se reproduce el activo y se actualiza el alpha.
///
/// Estructura por tecla:
///   Tecla 1 → IdleLoop_Fast              (snap inmediato, sin transición)
///   Tecla 2 → Transition_2 → IdleLoop_2
///   Tecla 3 → Transition_3 → IdleLoop_3
///   Tecla 4 → Transition_4 → IdleLoop_4
///
/// Mapeo wind → vegetación VAT:
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

    [Header("Vegetación VAT - Viento dual")]
    [Tooltip("Duración global de la mezcla entre viento débil y fuerte de la vegetación VAT.")]
    [SerializeField, Min(0f)] private float vatWindBlendDuration = 1.5f;

    [Header("Planta_MJ Alembic - Distance Culling")]
    [Tooltip("Distancia de animación de Planta_MJ durante la caminata inicial. 0 = sin límite.")]
    [SerializeField, Min(0f)] private float initialWalkAlembicDistance = 30f;
    [Tooltip("Distancia de animación al aproximarse y entrar en la iglesia. 0 = sin límite.")]
    [SerializeField, Min(0f)] private float churchAlembicDistance = 100f;
    [Tooltip("Tiempo que tarda en crecer desde la distancia inicial hasta la distancia de iglesia. 0 = instantaneo.")]
    [SerializeField, Min(0f)] private float churchAlembicDistanceTransitionDuration = 2f;
    [Tooltip("Cada cuantos segundos se recalcula la visibilidad de Planta_MJ.")]
    [FormerlySerializedAs("standaloneAlembicVisibilityCheckInterval")]
    [SerializeField, Min(0.02f)] private float alembicVisibilityCheckInterval = 0.2f;
    [Header("Planta_MJ Alembic - Playback")]
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

    [Tooltip("Prepara los VideoPlayers ocultos para evitar tirones al mostrarlos. Solo el video visible se reproduce.")]
    [SerializeField] private bool prewarmPlayersWhileHidden = true;

    [Header("Video Surface")]
    [Tooltip("0 usa la resolucion del clip. Usar un valor fijo solo si todos los videos comparten formato.")]
    [SerializeField, Min(0)] private int renderTextureWidth = 0;
    [Tooltip("0 usa la resolucion del clip. Usar un valor fijo solo si todos los videos comparten formato.")]
    [SerializeField, Min(0)] private int renderTextureHeight = 0;
    [Tooltip("Ancho en unidades de mundo del plano cercano de ventisca.")]
    [SerializeField, Min(0.01f)] private float cameraSurfaceWidth = 220f;
    [Tooltip("Alto en unidades de mundo del plano cercano de ventisca.")]
    [SerializeField, Min(0.01f)] private float cameraSurfaceHeight = 140f;
    [Tooltip("Profundidad/distancia local desde la camara del plano cercano.")]
    [SerializeField, Min(0.01f)] private float cameraSurfaceDepth = 18f;
    [Tooltip("Offset local del plano cercano. Y negativo baja la ventisca en pantalla.")]
    [SerializeField] private Vector2 cameraSurfaceLocalOffset = Vector2.zero;
    [Tooltip("Mantiene las capas de ventisca pegadas a pantalla para evitar parallax al cambiar de dentro/fuera.")]
    [SerializeField] private bool stabilizeBlizzardSurfaceOnScreen = true;
    [Tooltip("Mantiene fija la posicion Z de la ventisca aunque la camara se desplace lateralmente en Z.")]
    [SerializeField] private bool lockBlizzardSurfaceWorldZ = false;

    [Header("Video Surface - Background Copy")]
    [Tooltip("Distancia extra desde la ventisca cercana hasta su copia de fondo. Bajarlo acerca la copia al plano cercano.")]
    [SerializeField, Min(0.01f)] private float backgroundCopyDistanceFromNearBlizzard = 12f;

    [Tooltip("Compensa el movimiento horizontal de la camara desplazando las UVs del video. 0 = pegado a pantalla, 1 = mayor sensacion de mundo.")]
    [SerializeField, Range(0f, 1f)] private float horizontalMotionCompensation = 0.35f;
    [Tooltip("Cuantos metros horizontales de camara equivalen a una repeticion completa del video.")]
    [SerializeField, Min(0.01f)] private float horizontalWorldUnitsPerTextureRepeat = 24f;
    [Tooltip("Rendering Layer Mask del quad de ventisca. Todos=4294967295, Default=1, Light Layer 1=2, Light Layer 2=4.")]
    [SerializeField] private uint blizzardRenderingLayerMask = uint.MaxValue;

    [Header("Video Surface - Idle Background Copy")]
    [SerializeField] private bool enableIdleBackgroundLayer = true;
    [Tooltip("Ancla la capa grande al mundo cuando se hace visible. Si se desactiva, vuelve a seguir a la camara.")]
    [SerializeField] private bool idleBackgroundAnchoredToWorld = true;
    [SerializeField, Min(0.01f)] private float idleBackgroundWidth = 160f;
    [SerializeField, Min(0.01f)] private float idleBackgroundHeight = 90f;
    [SerializeField, Min(0.01f)] private float idleBackgroundDepth = 80f;
    [SerializeField] private Vector2 idleBackgroundOffset = Vector2.zero;
    [Tooltip("Repeticiones del video sobre la capa grande para evitar que la ventisca se estire.")]
    [SerializeField] private Vector2 idleBackgroundUvTiling = new Vector2(4f, 3f);
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
    private readonly HashSet<VideoPlayer> _prepareRequestedPlayers = new HashSet<VideoPlayer>();
    private readonly Dictionary<VideoPlayer, VideoSurface> _videoSurfaces = new Dictionary<VideoPlayer, VideoSurface>();
    private readonly Dictionary<VideoPlayer, VideoSurface> _cameraBackgroundSurfaces = new Dictionary<VideoPlayer, VideoSurface>();
    private readonly Dictionary<VideoPlayer, VideoSurface> _idleBackgroundSurfaces = new Dictionary<VideoPlayer, VideoSurface>();

    private int   _activeIdleIndex       = -1;
    private int   _activeTransitionIndex = -1;
    private int   _pendingIdleAfterTransitionIndex = -1;
    private float _transitionEndsAtUnscaled = -1f;
    private float _activeTransitionOpacity = 0f;
    private bool  _videoPlayersVisible;
    private float _videoOpacityMultiplier = 1f;
    private bool _videoOpacityFadeActive;
    private float _videoOpacityFadeStartedAt;
    private float _videoOpacityFadeDuration;
    private bool  _hasHorizontalMotionReference;
    private float _horizontalMotionReferenceX;
    private Camera _horizontalMotionReferenceCamera;
    private bool _hasBlizzardSurfaceWorldZAnchor;
    private float _blizzardSurfaceWorldZAnchor;
    private Camera _blizzardSurfaceWorldZAnchorCamera;
    private readonly List<AlembicStreamPlayer> _plantaMjPlayers = new List<AlembicStreamPlayer>();
    private readonly List<float> _plantaMjTimes = new List<float>();
    private readonly List<Renderer[]> _plantaMjRenderers = new List<Renderer[]>();
    private readonly List<bool> _plantaMjVisible = new List<bool>();
    private bool _plantaMjAutoPlaybackReported;
    private bool _standaloneVegetationPlaybackEnabled = true;
    private bool _blizzardVideoPlaybackEnabled = true;
    private float _currentAlembicMaxDistance = 30f;
    private float _nextAlembicVisibilityCheck;
    private bool _usesChurchAlembicDistance;
    private bool _alembicDistanceTransitionActive;
    private float _alembicDistanceTransitionStartedAt;
    private float _alembicDistanceTransitionFrom;
    private float _alembicDistanceTransitionTo;
    private static readonly int VatWindBlendId = Shader.PropertyToID("_VAT_WindBlend");
    private float _vatWindBlend = 1f;
    private float _vatWindBlendTarget = 1f;

    private sealed class VideoSurface
    {
        public GameObject quad;
        public MeshRenderer renderer;
        public Mesh mesh;
        public Material material;
        public RenderTexture texture;
        public bool ownsTexture;
        public bool usesCameraSurfaceSettings;
        public bool usesCameraBackgroundSettings;
        public bool usesFixedCameraPlane;
        public bool usesWorldPlane;
        public bool worldPoseInitialized;
        public float fixedWidth;
        public float fixedHeight;
        public float fixedDepth;
        public Vector2 fixedOffset;
        public Vector2 uvTiling = Vector2.one;
        public float horizontalMotionCompensation;
        public float alpha;
        public Vector3 worldCenter;
        public Quaternion worldRotation = Quaternion.identity;
        public readonly Vector3[] vertices = new Vector3[4];
        public readonly Vector2[] uvs = new Vector2[4];
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cameraWasAssigned = blizzardVideoCamera != null;
        Shader.SetGlobalFloat(VatWindBlendId, _vatWindBlend);

        ResolvePlantaMjAlembicPlayers();
        UseInitialWalkAlembicDistance();

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
        UpdateAlembicDistanceTransition();
        UpdateAlembicCulling();
        UpdateVideoOpacityFade();
        UpdateVatWindBlend();

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

        SetVegetationWindState(preset);
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

        SetVegetationWindState(preset);
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
        // El player ya fue preparado al cargar. Pausamos el anterior y reanudamos
        // únicamente esta transición, sin preparar ni reasignar clips.
        PlayOnlyActiveFixedPlayer(requestPrepareIfNeeded: true);

        float opacity = _crossFadeOverride >= 0f ? _crossFadeOverride : GetTransitionOpacity(idx);
        _activeTransitionOpacity = opacity;
        SetVideoOpacity(transition, ShouldShowVideos ? opacity : 0f);

        _transitionEndsAtUnscaled = Time.unscaledTime + GetClipDurationSafe(transition);
    }

    public static int ActivateAllVideoPlayersRoots()
    {
        WindStateManager[] managers = Object.FindObjectsByType<WindStateManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (WindStateManager manager in managers)
            manager.ActivateVideoPlayersRoot();

        return managers.Length;
    }

    public static int FadeInAllVideoPlayers(float duration)
    {
        WindStateManager[] managers = Object.FindObjectsByType<WindStateManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (WindStateManager manager in managers)
            manager.FadeInVideos(duration);

        return managers.Length;
    }

    public void FadeInVideos(float duration)
    {
        _videoOpacityFadeDuration = Mathf.Max(0f, duration);
        _videoOpacityFadeStartedAt = Time.unscaledTime;
        _videoOpacityFadeActive = _videoOpacityFadeDuration > 0f;
        _videoOpacityMultiplier = _videoOpacityFadeActive ? 0f : 1f;

        ApplyCurrentVideoAlphas();
    }

    /// <summary>
    /// Amplia la distancia de actualizacion de los Alembic en todos los managers activos.
    /// Permite que entradas sin desplazamiento fisico, como un teletransporte, produzcan
    /// el mismo cambio de estado que el trigger del recorrido inicial.
    /// </summary>
    public static int UseChurchAlembicDistanceOnAll()
    {
        WindStateManager[] managers = Object.FindObjectsByType<WindStateManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (WindStateManager manager in managers)
            manager.UseChurchAlembicDistance();

        return managers.Length;
    }

    public static int PrewarmAllVideoPlayersHidden()
    {
        WindStateManager[] managers = Object.FindObjectsByType<WindStateManager>(
            FindObjectsInactive.Exclude,
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
        ResetBlizzardSurfaceWorldZAnchor();
        ResetWorldBackgroundAnchors();

        if (!_blizzardVideoPlaybackEnabled)
        {
            HideAllVideos();
            PauseAllVideoPlayers();
            return;
        }

        PrepareAllFixedPlayers();
        PlayOnlyActiveFixedPlayer(requestPrepareIfNeeded: true);

        ActivateLegacyVideoPlayer();
        ApplyCurrentVideoAlphas();
    }

    public void UseInitialWalkAlembicDistance()
    {
        _usesChurchAlembicDistance = false;
        _alembicDistanceTransitionActive = false;
        ApplyAlembicCullingDistance(initialWalkAlembicDistance);
        _nextAlembicVisibilityCheck = 0f;
    }

    public void UseChurchAlembicDistance()
    {
        _usesChurchAlembicDistance = true;
        _alembicDistanceTransitionFrom = _currentAlembicMaxDistance;
        _alembicDistanceTransitionTo = Mathf.Max(0f, churchAlembicDistance);
        _alembicDistanceTransitionStartedAt = Time.unscaledTime;
        _alembicDistanceTransitionActive = churchAlembicDistanceTransitionDuration > 0f
            && !Mathf.Approximately(_alembicDistanceTransitionFrom, _alembicDistanceTransitionTo);

        if (!_alembicDistanceTransitionActive)
            ApplyAlembicCullingDistance(_alembicDistanceTransitionTo);
        else
            _nextAlembicVisibilityCheck = 0f;
    }

    private void ApplyAlembicCullingDistance(float distance)
    {
        _currentAlembicMaxDistance = Mathf.Max(0f, distance);
    }

    private void UpdateAlembicDistanceTransition()
    {
        if (!_alembicDistanceTransitionActive)
            return;

        float duration = Mathf.Max(0f, churchAlembicDistanceTransitionDuration);
        float progress = duration > 0f
            ? Mathf.Clamp01((Time.unscaledTime - _alembicDistanceTransitionStartedAt) / duration)
            : 1f;

        ApplyAlembicCullingDistance(Mathf.Lerp(
            _alembicDistanceTransitionFrom,
            _alembicDistanceTransitionTo,
            progress));

        if (progress >= 1f)
            _alembicDistanceTransitionActive = false;
    }

    private void UpdateAlembicCulling()
    {
        if (Time.unscaledTime < _nextAlembicVisibilityCheck)
            return;

        _nextAlembicVisibilityCheck = Time.unscaledTime
            + Mathf.Max(0.02f, alembicVisibilityCheckInterval);

        Camera camera = IsUsableCamera(blizzardVideoCamera)
            ? blizzardVideoCamera
            : ResolveCamera();

        UpdatePlantaMjVisibility(camera);

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
        ResetBlizzardSurfaceWorldZAnchor();

        if (!_blizzardVideoPlaybackEnabled)
        {
            HideAllVideos();
            PauseAllVideoPlayers();
            _videoPlayersVisible = wasVisible;
            ApplyCurrentVideoAlphas();
            return;
        }

        PrepareAllFixedPlayers();
        PlayOnlyActiveFixedPlayer(requestPrepareIfNeeded: false);

        _videoPlayersVisible = wasVisible;
        ApplyCurrentVideoAlphas();
        PlayOnlyActiveFixedPlayer(requestPrepareIfNeeded: true);
        PrewarmPlantaMjAlembicPlayers(0.05f);
    }

    public void SetStandaloneVegetationPlaybackEnabled(bool shouldRun)
    {
        _standaloneVegetationPlaybackEnabled = shouldRun;

        if (shouldRun)
        {
            foreach (AlembicStreamPlayer player in _plantaMjPlayers)
                if (player != null && !player.enabled) player.enabled = true;

            _nextAlembicVisibilityCheck = 0f;
        }
    }

    public void SetBlizzardVideoPlaybackEnabled(bool shouldRun)
    {
        _blizzardVideoPlaybackEnabled = shouldRun;

        if (!shouldRun)
        {
            HideAllVideos();
            PauseAllVideoPlayers();
            return;
        }

        if (_playersRoot == null && preloadPlayersOnAwake)
            BuildAndPrepareFixedPlayers();

        if (_playersRoot == null || !_videoPlayersVisible)
        {
            ApplyCurrentVideoAlphas();
            return;
        }

        _playersRoot.gameObject.SetActive(true);
        PrepareAllFixedPlayers();
        PlayOnlyActiveFixedPlayer(requestPrepareIfNeeded: true);
        ActivateLegacyVideoPlayer();
        ApplyCurrentVideoAlphas();
    }

    // ── Preload de VideoPlayers fijos ─────────────────────────────────────────

    private void BuildAndPrepareFixedPlayers()
    {
        if (windStates == null || windStates.Length == 0)
        {
            Debug.LogWarning($"[{nameof(WindStateManager)}] No hay windStates configurados.", this);
            return;
        }

        UnsubscribeAll();
        ClearRuntimePlayers();

        _idlePlayers = new VideoPlayer[windStates.Length];
        _transitionPlayers = new VideoPlayer[windStates.Length];
        _allPlayers.Clear();
        _cameraBackgroundSurfaces.Clear();
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
                // Se prepara al cargar y solo se reproduce mientras esta transición es visible.
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
            RequestPrepare(vp);

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
        _prepareRequestedPlayers.Remove(source);

        ApplyCurrentVideoAlphas();
        PlayOnlyActiveFixedPlayer(requestPrepareIfNeeded: false);
    }

    // ── Cambio visual por opacidad ────────────────────────────────────────────

    private void ShowIdle(int stateIdx)
    {
        if (!IsValidStateIndex(stateIdx)) return;

        VideoPlayer idle = GetIdlePlayer(stateIdx);
        if (idle == null)
        {
            Debug.LogWarning($"[{nameof(WindStateManager)}] Estado {stateIdx} no tiene idleLoopClip/VideoPlayer asignado.", this);
            return;
        }

        _activeIdleIndex = stateIdx;
        _activeTransitionIndex = -1;
        _pendingIdleAfterTransitionIndex = -1;
        _transitionEndsAtUnscaled = -1f;
        _activeTransitionOpacity = 0f;

        HideAllVideos();
        SetVideoOpacity(idle, ShouldShowVideos ? GetIdleOpacity(stateIdx) : 0f);
        PlayOnlyActiveFixedPlayer(requestPrepareIfNeeded: true);
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

    private void PrepareAllFixedPlayers()
    {
        for (int i = 0; i < _allPlayers.Count; i++)
        {
            VideoPlayer player = _allPlayers[i];
            if (player == null) continue;

            AssignCameraToPlayer(player);
            RequestPrepare(player);
        }
    }

    private void RequestPrepare(VideoPlayer player)
    {
        if (player == null || player.isPrepared || _prepareRequestedPlayers.Contains(player))
            return;

        _prepareRequestedPlayers.Add(player);
        player.Prepare();
    }

    private VideoPlayer GetActiveFixedPlayer()
    {
        if (_activeTransitionIndex >= 0)
            return GetTransitionPlayer(_activeTransitionIndex);

        if (_activeIdleIndex >= 0)
            return GetIdlePlayer(_activeIdleIndex);

        return null;
    }

    private void PlayOnlyActiveFixedPlayer(bool requestPrepareIfNeeded)
    {
        VideoPlayer activePlayer = ShouldShowVideos ? GetActiveFixedPlayer() : null;

        for (int i = 0; i < _allPlayers.Count; i++)
        {
            VideoPlayer player = _allPlayers[i];
            if (player == null) continue;

            if (player != activePlayer)
            {
                if (player.isPlaying)
                    player.Pause();

                continue;
            }

            AssignCameraToPlayer(player);

            if (player.isPrepared)
            {
                if (!player.isPlaying)
                    player.Play();
            }
            else if (requestPrepareIfNeeded)
            {
                RequestPrepare(player);
            }
        }
    }

    private void PauseAllVideoPlayers()
    {
        for (int i = 0; i < _allPlayers.Count; i++)
        {
            VideoPlayer player = _allPlayers[i];
            if (player != null && player.isPlaying)
                player.Pause();
        }

        if (blizzardVideoPlayer != null)
        {
            blizzardVideoPlayer.targetCameraAlpha = 0f;

            if (blizzardVideoPlayer.isPlaying)
                blizzardVideoPlayer.Pause();
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

                if (ShouldShowVideos && player == activeTransition)
                {
                    if (player.isPrepared && !player.isPlaying)
                        player.Play();
                }
                else if (player.isPlaying)
                {
                    player.Pause();
                }
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

                if (ShouldShowVideos && player == activeIdle)
                {
                    if (player.isPrepared && !player.isPlaying)
                        player.Play();
                }
                else if (player.isPlaying)
                {
                    player.Pause();
                }
            }
        }
    }

    private void UpdateVideoOpacityFade()
    {
        if (!_videoOpacityFadeActive)
            return;

        float progress = Mathf.Clamp01(
            (Time.unscaledTime - _videoOpacityFadeStartedAt) / _videoOpacityFadeDuration);

        _videoOpacityMultiplier = Mathf.SmoothStep(0f, 1f, progress);

        if (progress >= 1f)
        {
            _videoOpacityMultiplier = 1f;
            _videoOpacityFadeActive = false;
        }
    }

    private void ActivateLegacyVideoPlayer()
    {
        if (blizzardVideoPlayer == null || _allPlayers.Count > 0) return;

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
        blizzardVideoPlayer.targetCameraAlpha = ShouldShowVideos
            ? GetIdleOpacity(stateIdx) * _videoOpacityMultiplier
            : 0f;

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
        _blizzardVideoPlaybackEnabled && _playersRoot != null && _playersRoot.gameObject.activeInHierarchy;

    private bool ShouldShowVideos =>
        CanRunVideoPlayers && _videoPlayersVisible;

    // ── Callbacks de VideoPlayer ───────────────────────────────────────────────

    private void OnVideoError(VideoPlayer source, string message)
    {
        _prepareRequestedPlayers.Remove(source);
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

        _prepareRequestedPlayers.Clear();
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
        ResolvePlantaMjAlembicPlayers();
        if (!_alembicDistanceTransitionActive)
        {
            ApplyAlembicCullingDistance(
                _usesChurchAlembicDistance
                    ? churchAlembicDistance
                    : initialWalkAlembicDistance);
        }
        _nextAlembicVisibilityCheck = 0f;
        AssignCameraToAllPlayers();
        ResetBlizzardSurfaceWorldZAnchor();
        ResetWorldBackgroundAnchors();
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
        foreground.usesCameraSurfaceSettings = true;
        ApplyCameraSurfaceSettings(foreground);
        _videoSurfaces[player] = foreground;

        VideoSurface cameraBackground = CreateVideoSurfaceQuad(
            player.transform,
            $"{playerName}_CameraBackgroundQuad",
            $"{playerName}_CameraBackgroundMesh",
            $"{playerName}_CameraBackgroundMaterial",
            texture);

        if (cameraBackground != null)
        {
            cameraBackground.texture = texture;
            cameraBackground.ownsTexture = false;
            cameraBackground.usesCameraSurfaceSettings = true;
            cameraBackground.usesCameraBackgroundSettings = true;
            ApplyCameraSurfaceSettings(cameraBackground);
            if (cameraBackground.material != null)
                cameraBackground.material.renderQueue = (int)RenderQueue.Transparent + 5;
            _cameraBackgroundSurfaces[player] = cameraBackground;
        }

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
        background.usesWorldPlane = idleBackgroundAnchoredToWorld && !stabilizeBlizzardSurfaceOnScreen;
        background.fixedWidth = idleBackgroundWidth;
        background.fixedHeight = idleBackgroundHeight;
        background.fixedDepth = idleBackgroundDepth;
        background.fixedOffset = idleBackgroundOffset;
        background.uvTiling = ClampUvTiling(idleBackgroundUvTiling);
        background.horizontalMotionCompensation = stabilizeBlizzardSurfaceOnScreen
            ? 0f
            : idleBackgroundHorizontalMotionCompensation;
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

        float alpha = Mathf.Clamp01(opacity * _videoOpacityMultiplier);
        player.targetCameraAlpha = 0f;

        if (!_videoSurfaces.TryGetValue(player, out VideoSurface surface) || surface == null)
            return;

        SetSurfaceOpacity(surface, alpha);

        if (_cameraBackgroundSurfaces.TryGetValue(player, out VideoSurface cameraBackgroundSurface))
            SetSurfaceOpacity(cameraBackgroundSurface, alpha * Mathf.Clamp01(idleBackgroundOpacityMultiplier));

        if (_idleBackgroundSurfaces.TryGetValue(player, out VideoSurface backgroundSurface))
            SetSurfaceOpacity(backgroundSurface, alpha * Mathf.Clamp01(idleBackgroundOpacityMultiplier));
    }

    private void SetSurfaceOpacity(VideoSurface surface, float alpha)
    {
        if (surface == null) return;

        bool wasVisible = surface.alpha > 0.001f;
        float clampedAlpha = Mathf.Clamp01(alpha);

        if (surface.usesWorldPlane && !wasVisible && clampedAlpha > 0.001f)
            surface.worldPoseInitialized = false;

        surface.alpha = clampedAlpha;

        if (surface.renderer != null)
        {
            surface.renderer.enabled = surface.alpha > 0.001f;
            surface.renderer.renderingLayerMask = blizzardRenderingLayerMask;
        }

        SetSurfaceMaterialAlpha(surface.material, surface.alpha);
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

        foreach (VideoSurface surface in _cameraBackgroundSurfaces.Values)
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

        ApplyCameraSurfaceSettings(surface);

        Transform cameraTransform = blizzardVideoCamera.transform;
        Transform quadTransform = surface.quad.transform;

        if (surface.usesWorldPlane)
        {
            if (!TryEnsureWorldPlanePose(surface, cameraTransform))
                return;

            quadTransform.position = surface.worldCenter;
            quadTransform.rotation = surface.worldRotation;
        }
        else
        {
            quadTransform.position = stabilizeBlizzardSurfaceOnScreen
                ? cameraTransform.position
                : GetCameraSurfacePosition(cameraTransform, surface);
            quadTransform.rotation = cameraTransform.rotation;
        }

        quadTransform.localScale = Vector3.one;

        if (surface.usesFixedCameraPlane && surface.usesWorldPlane)
            PositionWorldPlaneSurface(surface);
        else if (surface.usesFixedCameraPlane)
            PositionFixedCameraPlaneSurface(surface);
        else
            PositionViewportSurface(surface, quadTransform);

        surface.mesh.vertices = surface.vertices;
        UpdateSurfaceUvs(surface);
        surface.mesh.RecalculateBounds();
    }

    private void ApplyCameraSurfaceSettings(VideoSurface surface)
    {
        if (surface == null || !surface.usesCameraSurfaceSettings)
            return;

        surface.usesFixedCameraPlane = true;
        surface.fixedWidth = Mathf.Max(0.01f, cameraSurfaceWidth);
        surface.fixedHeight = Mathf.Max(0.01f, cameraSurfaceHeight);
        float extraDepth = surface.usesCameraBackgroundSettings ? backgroundCopyDistanceFromNearBlizzard : 0f;
        surface.fixedDepth = Mathf.Max(0.01f, cameraSurfaceDepth + extraDepth);
        surface.fixedOffset = cameraSurfaceLocalOffset;
        surface.horizontalMotionCompensation = stabilizeBlizzardSurfaceOnScreen
            ? 0f
            : horizontalMotionCompensation;
    }

    private void PositionViewportSurface(VideoSurface surface, Transform quadTransform)
    {
        float distance = Mathf.Max(0.01f, cameraSurfaceDepth);
        float margin = 0.06f;
        Vector3 localOffset = new Vector3(cameraSurfaceLocalOffset.x, cameraSurfaceLocalOffset.y, 0f);

        surface.vertices[0] = GetViewportCornerLocal(quadTransform, -margin, -margin, distance) + localOffset;
        surface.vertices[1] = GetViewportCornerLocal(quadTransform, -margin, 1f + margin, distance) + localOffset;
        surface.vertices[2] = GetViewportCornerLocal(quadTransform, 1f + margin, 1f + margin, distance) + localOffset;
        surface.vertices[3] = GetViewportCornerLocal(quadTransform, 1f + margin, -margin, distance) + localOffset;
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

    private void PositionWorldPlaneSurface(VideoSurface surface)
    {
        float halfWidth = Mathf.Max(0.01f, surface.fixedWidth) * 0.5f;
        float halfHeight = Mathf.Max(0.01f, surface.fixedHeight) * 0.5f;

        surface.vertices[0] = new Vector3(-halfWidth, -halfHeight, 0f);
        surface.vertices[1] = new Vector3(-halfWidth,  halfHeight, 0f);
        surface.vertices[2] = new Vector3( halfWidth,  halfHeight, 0f);
        surface.vertices[3] = new Vector3( halfWidth, -halfHeight, 0f);
    }

    private void UpdateSurfaceUvs(VideoSurface surface)
    {
        float uOffset = GetHorizontalUvOffset(surface);
        Vector2 uvTiling = ClampUvTiling(surface.uvTiling);

        surface.uvs[0] = new Vector2(uOffset, 0f);
        surface.uvs[1] = new Vector2(uOffset, uvTiling.y);
        surface.uvs[2] = new Vector2(uvTiling.x + uOffset, uvTiling.y);
        surface.uvs[3] = new Vector2(uvTiling.x + uOffset, 0f);

        surface.mesh.uv = surface.uvs;
    }

    private bool TryEnsureWorldPlanePose(VideoSurface surface, Transform cameraTransform)
    {
        if (surface == null || cameraTransform == null)
            return false;

        if (surface.worldPoseInitialized)
            return true;

        if (surface.alpha <= 0.001f)
            return false;

        Vector3 localCenter = new Vector3(
            surface.fixedOffset.x,
            surface.fixedOffset.y,
            Mathf.Max(0.01f, surface.fixedDepth));

        surface.worldCenter = ApplyBlizzardSurfaceWorldZLock(cameraTransform.TransformPoint(localCenter), surface);
        surface.worldRotation = cameraTransform.rotation;
        surface.worldPoseInitialized = true;
        return true;
    }

    private Vector2 ClampUvTiling(Vector2 tiling)
    {
        return new Vector2(
            Mathf.Max(0.01f, tiling.x),
            Mathf.Max(0.01f, tiling.y));
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

    private Vector3 GetCameraSurfacePosition(Transform cameraTransform, VideoSurface surface)
    {
        if (cameraTransform == null)
            return Vector3.zero;

        return ApplyBlizzardSurfaceWorldZLock(cameraTransform.position, surface);
    }

    private Vector3 ApplyBlizzardSurfaceWorldZLock(Vector3 position, VideoSurface surface)
    {
        if (!lockBlizzardSurfaceWorldZ || surface == null || surface.alpha <= 0.001f)
            return position;

        if (!_hasBlizzardSurfaceWorldZAnchor ||
            _blizzardSurfaceWorldZAnchorCamera != blizzardVideoCamera)
        {
            _blizzardSurfaceWorldZAnchor = position.z;
            _blizzardSurfaceWorldZAnchorCamera = blizzardVideoCamera;
            _hasBlizzardSurfaceWorldZAnchor = true;
        }

        position.z = _blizzardSurfaceWorldZAnchor;
        return position;
    }

    private void ResetBlizzardSurfaceWorldZAnchor()
    {
        _hasBlizzardSurfaceWorldZAnchor = false;
        _blizzardSurfaceWorldZAnchorCamera = null;
    }

    private void ResetWorldBackgroundAnchors()
    {
        foreach (VideoSurface surface in _idleBackgroundSurfaces.Values)
        {
            if (surface == null || !surface.usesWorldPlane)
                continue;

            surface.worldPoseInitialized = false;
        }
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

        foreach (VideoSurface surface in _cameraBackgroundSurfaces.Values)
            ReleaseVideoSurface(surface);

        foreach (VideoSurface surface in _idleBackgroundSurfaces.Values)
            ReleaseVideoSurface(surface);

        _videoSurfaces.Clear();
        _cameraBackgroundSurfaces.Clear();
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

    // ── Vegetación VAT ────────────────────────────────────────────────────────

    private void SetVegetationWindState(WindPreset preset)
    {
        bool fast = preset == WindPreset.W1_MaxIdle || preset == WindPreset.W4_MinToMedium;

        _vatWindBlendTarget = fast ? 1f : 0f;
    }

    private void UpdateVatWindBlend()
    {
        if (Mathf.Approximately(_vatWindBlend, _vatWindBlendTarget))
            return;

        if (vatWindBlendDuration <= 0f)
        {
            _vatWindBlend = _vatWindBlendTarget;
        }
        else
        {
            _vatWindBlend = Mathf.MoveTowards(
                _vatWindBlend,
                _vatWindBlendTarget,
                Time.deltaTime / vatWindBlendDuration);
        }

        Shader.SetGlobalFloat(VatWindBlendId, _vatWindBlend);
    }

    // ── Alembic standalone ───────────────────────────────────────────────────

    private void ResolvePlantaMjAlembicPlayers()
    {
        _plantaMjPlayers.Clear();
        _plantaMjTimes.Clear();
        _plantaMjRenderers.Clear();
        _plantaMjVisible.Clear();

        if (!playPlantaMjAlembic)
            return;

        foreach (var player in Object.FindObjectsByType<AlembicStreamPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (player == null) continue;
            if (!IsInsideNamedHierarchy(player.transform, "planta_mj")) continue;

            _plantaMjPlayers.Add(player);
            _plantaMjTimes.Add(GetClampedAlembicTime(player));
            _plantaMjRenderers.Add(player.GetComponentsInChildren<Renderer>(true));
            _plantaMjVisible.Add(true);
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

        if (!_standaloneVegetationPlaybackEnabled)
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
                _plantaMjRenderers.RemoveAt(i);
                _plantaMjVisible.RemoveAt(i);
                continue;
            }

            if (!_plantaMjVisible[i])
                continue;

            float duration = player.Duration;
            if (duration <= 0f) continue;

            float time = _plantaMjTimes[i] + Time.deltaTime * speed;
            if (time > duration)
                time %= duration;

            _plantaMjTimes[i] = time;
            player.CurrentTime = time;
        }
    }

    private void UpdatePlantaMjVisibility(Camera camera)
    {
        for (int i = 0; i < _plantaMjPlayers.Count; i++)
        {
            AlembicStreamPlayer player = _plantaMjPlayers[i];
            if (player == null)
                continue;

            bool visible = camera == null || IsAlembicVisible(
                player.transform,
                _plantaMjRenderers[i],
                camera,
                _currentAlembicMaxDistance);
            _plantaMjVisible[i] = visible;
            SetRenderersEnabled(_plantaMjRenderers[i], visible);
        }
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool isEnabled)
    {
        if (renderers == null)
            return;

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.enabled != isEnabled)
                renderer.enabled = isEnabled;
        }
    }

    private static bool IsAlembicVisible(
        Transform root,
        Renderer[] renderers,
        Camera camera,
        float maxDistance)
    {
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        float maxDistanceSqr = maxDistance > 0f
            ? maxDistance * maxDistance
            : float.PositiveInfinity;
        bool foundRenderer = false;

        if (renderers != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                foundRenderer = true;
                Bounds bounds = renderer.bounds;
                Vector3 closestPoint = bounds.ClosestPoint(camera.transform.position);
                if ((closestPoint - camera.transform.position).sqrMagnitude > maxDistanceSqr)
                    continue;

                if (GeometryUtility.TestPlanesAABB(planes, bounds))
                    return true;
            }
        }

        if (foundRenderer)
            return false;

        return maxDistance <= 0f
            || (root.position - camera.transform.position).sqrMagnitude <= maxDistanceSqr;
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

    private bool IsValidStateIndex(int idx)
    {
        if (windStates == null || idx < 0 || idx >= windStates.Length)
        {
            Debug.LogWarning($"[{nameof(WindStateManager)}] Índice de viento inválido: {idx}.", this);
            return false;
        }

        return true;
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
