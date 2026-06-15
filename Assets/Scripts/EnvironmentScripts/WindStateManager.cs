using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

/// <summary>
/// Gestiona la ventisca de sprites mediante CrossFade entre 4 clips de Animator,
/// controla la emisión de un ParticleSystem por estado,
/// y activa la animación Alembic slow/fast de los árboles.
///
/// Animator setup (ventisca):
///   - 4 estados (uno por clip). Entry apunta al estado por defecto (W1_MaxIdle).
///   - Sin flechas entre estados.
///   - Asignar el nombre exacto de cada estado en windStates[].stateName.
///
/// Mapeo wind → árboles:
///   W1_MaxIdle / W4_MinToMedium → fast
///   W2_MaxToMedium / W3_MediumToMin → slow
///
/// Vídeo de ventisca:
///   - Cada estado reproduce en bucle un tramo del vídeo definido en segundos.
///   - Cada estado controla si se muestra, el inicio/fin del tramo y su opacidad.
///   - El clip es una referencia a asset y el VideoPlayer se recupera o crea en runtime.
/// </summary>
public class WindStateManager : MonoBehaviour
{
    public enum WindPreset { W1_MaxIdle = 0, W2_MaxToMedium = 1, W3_MediumToMin = 2, W4_MinToMedium = 3 }

    [System.Serializable]
    public struct WindStateData
    {
        [Tooltip("Nombre exacto del estado en el Animator Controller.")]
        public string stateName;
        [Range(0f, 1f)]
        [Tooltip("Tiempo normalizado de CrossFade hacia este estado (0 = instantáneo).")]
        public float crossFadeTime;
        [Tooltip("Emisión de partículas por segundo para este estado.")]
        public float emissionRate;
        [Tooltip("Indica si el vídeo de ventisca debe reproducirse en este estado.")]
        public bool playVideo;
        [Min(0f)]
        [Tooltip("Segundo del vídeo donde comienza el tramo de este estado.")]
        public float videoStartTime;
        [Min(0f)]
        [Tooltip("Segundo del vídeo donde termina el tramo de este estado.")]
        public float videoEndTime;
        [Range(0f, 1f)]
        [Tooltip("Opacidad del vídeo sobre la cámara. El clip no tiene canal alfa, así que valores bajos evitan oscurecer demasiado la escena.")]
        public float videoOpacity;
    }

    [Header("Estados (0=W1, 1=W2, 2=W3, 3=W4)")]
    [SerializeField] private WindStateData[] windStates = new WindStateData[]
    {
        new WindStateData { stateName = "Blizzard_Fast",           crossFadeTime = 0f,   emissionRate = 4000f, playVideo = true, videoStartTime = 2f, videoEndTime = 3f, videoOpacity = 0.28f },
        new WindStateData { stateName = "Blizzard_Fast_to_Medium", crossFadeTime = 0.1f, emissionRate = 2000f, playVideo = true, videoStartTime = 1f, videoEndTime = 2f, videoOpacity = 0.20f },
        new WindStateData { stateName = "Blizzard_Slow",           crossFadeTime = 0.1f, emissionRate = 500f,  playVideo = true, videoStartTime = 1f, videoEndTime = 2f, videoOpacity = 0.08f },
        new WindStateData { stateName = "Blizzard_Medium",         crossFadeTime = 0.1f, emissionRate = 1000f, playVideo = true, videoStartTime = 2f, videoEndTime = 3f, videoOpacity = 0.16f },
    };

    [Header("Animators de ventisca")]
    [SerializeField] private Animator[] windAnimators;

    [Header("Árboles Alembic")]
    [SerializeField] private AlembicTreeWindController[] treeControllers;

    [Header("Particle System")]
    [SerializeField] private ParticleSystem blizzardParticles;

    [Header("Vídeo de ventisca")]
    [SerializeField] private VideoClip blizzardVideoClip;
    [SerializeField] private VideoPlayer blizzardVideoPlayer;
    [Tooltip("Cámara sobre la que se dibuja el vídeo. Si queda vacía, se usa la cámara del VideoPlayer o Camera.main.")]
    [SerializeField] private Camera blizzardVideoCamera;

    private int _currentStateIndex = -1;
    private bool _videoPrepareRequested;
    private bool _videoShouldPlay;
    private bool _videoNeedsSeek = true;
    private bool _videoSeekPending;
    private float _videoPendingSeekTime;
    private float _videoSegmentStartTime;
    private float _videoSegmentEndTime = 0.05f;
    private float _videoOpacity;
    private VideoPlayer _subscribedVideoPlayer;
    private bool _videoCameraWasAssigned;
    private bool _missingVideoCameraWarningShown;

    private void Awake()
    {
        _videoCameraWasAssigned = blizzardVideoCamera != null;
        ResolveMissingReferences();
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (windStates != null && windStates.Length > 0)
            ApplyVideoState(windStates[0]);
    }

    private void Update()
    {
        RefreshVideoCamera();
        KeepVideoInsideCurrentSegment();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromVideoPlayer();

        if (blizzardVideoPlayer != null)
        {
            blizzardVideoPlayer.targetCameraAlpha = 0f;

            if (blizzardVideoPlayer.isActiveAndEnabled)
                blizzardVideoPlayer.Pause();
        }
    }

    // ── API pública — viento ─────────────────────────────────────────────────

    /// <summary>Transiciona al preset dado usando el CrossFade configurado en el Inspector.</summary>
    public void TransitionTo(WindPreset preset, float crossFadeOverride = -1f)
    {
        int idx = (int)preset;
        if (idx == _currentStateIndex) return;
        _currentStateIndex = idx;

        float cft = crossFadeOverride >= 0f ? crossFadeOverride : windStates[idx].crossFadeTime;
        CrossFadeAll(windStates[idx].stateName, cft);
        SetEmission(windStates[idx].emissionRate);
        SetTreeWindSpeed(preset);
        ApplyVideoState(windStates[idx]);
    }

    /// <summary>Salta instantáneamente al preset dado desde el frame 0 del clip.</summary>
    public void SnapToState(WindPreset preset)
    {
        int idx = (int)preset;
        _currentStateIndex = idx;
        CrossFadeAll(windStates[idx].stateName, 0f);
        SetEmission(windStates[idx].emissionRate);
        SetTreeWindSpeed(preset);
        ApplyVideoState(windStates[idx]);
    }

    // ── Privado ──────────────────────────────────────────────────────────────

    private void ResolveMissingReferences()
    {
        bool repaired = false;

        if (HasMissingReferences(windAnimators))
        {
            var matches = new List<Animator>();
            foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (HasAllWindStates(animator))
                    matches.Add(animator);
            }

            windAnimators = matches.ToArray();
            repaired |= windAnimators.Length > 0;
        }

        if (HasMissingReferences(treeControllers))
        {
            var matches = new List<AlembicTreeWindController>();
            foreach (var tree in Object.FindObjectsByType<AlembicTreeWindController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tree != null && tree.HasAvailablePlayers)
                    matches.Add(tree);
            }

            treeControllers = matches.ToArray();
            repaired |= treeControllers.Length > 0;
        }

        if (blizzardParticles == null)
        {
            var matches = new List<ParticleSystem>();
            foreach (var particles in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (particles.name == "SnowStorm")
                    matches.Add(particles);
            }

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

        if (blizzardVideoPlayer == null && blizzardVideoClip != null)
            blizzardVideoPlayer = FindVideoPlayerForClip();

        if (repaired)
            Debug.LogWarning($"[{nameof(WindStateManager)}] Se repararon referencias perdidas durante esta ejecución. Revisa el Inspector para corregirlas de forma permanente.", this);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!EnsureVideoPlayer())
            return;

        ApplyPendingVideoState();
    }

    private VideoPlayer FindVideoPlayerForClip()
    {
        foreach (var player in Object.FindObjectsByType<VideoPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (player != null && player.clip == blizzardVideoClip)
                return player;
        }

        return null;
    }

    private bool EnsureVideoPlayer()
    {
        if (blizzardVideoClip == null)
            return false;

        if (blizzardVideoPlayer == null)
        {
            blizzardVideoPlayer = FindVideoPlayerForClip();

            if (blizzardVideoPlayer == null)
            {
                Camera playbackCamera = _videoCameraWasAssigned
                    ? blizzardVideoCamera
                    : FindPlaybackCamera();

                if (!IsUsablePlaybackCamera(playbackCamera))
                    return false;

                blizzardVideoPlayer = gameObject.AddComponent<VideoPlayer>();
                blizzardVideoPlayer.targetCamera = playbackCamera;
            }
        }

        return ConfigureVideoPlayer();
    }

    private bool ConfigureVideoPlayer()
    {
        if (blizzardVideoPlayer == null || blizzardVideoClip == null)
            return false;

        SubscribeToVideoPlayer();

        if (blizzardVideoPlayer.clip != blizzardVideoClip)
        {
            blizzardVideoPlayer.Stop();
            blizzardVideoPlayer.clip = blizzardVideoClip;
            _videoPrepareRequested = false;
            _videoNeedsSeek = true;
        }

        blizzardVideoPlayer.playOnAwake = false;
        blizzardVideoPlayer.isLooping = false;
        blizzardVideoPlayer.waitForFirstFrame = true;
        blizzardVideoPlayer.skipOnDrop = true;
        blizzardVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
        blizzardVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        blizzardVideoPlayer.renderMode = VideoRenderMode.CameraNearPlane;
        blizzardVideoPlayer.aspectRatio = VideoAspectRatio.FitOutside;
        blizzardVideoPlayer.playbackSpeed = 1f;

        return TryAssignVideoCamera();
    }

    private bool TryAssignVideoCamera()
    {
        if (_videoCameraWasAssigned)
        {
            if (!IsUsablePlaybackCamera(blizzardVideoCamera))
            {
                WarnMissingVideoCamera("La cámara asignada está desactivada o ya no existe.");
                return false;
            }
        }
        else
        {
            Camera resolvedCamera = IsUsablePlaybackCamera(blizzardVideoPlayer.targetCamera)
                ? blizzardVideoPlayer.targetCamera
                : FindPlaybackCamera();

            if (resolvedCamera == null)
            {
                blizzardVideoCamera = null;
                WarnMissingVideoCamera("No se encontró una cámara activa llamada CAM_Main ni una cámara con el tag MainCamera.");
                return false;
            }

            blizzardVideoCamera = resolvedCamera;
        }

        _missingVideoCameraWarningShown = false;
        blizzardVideoPlayer.targetCamera = blizzardVideoCamera;
        return true;
    }

    private static Camera FindPlaybackCamera()
    {
        Camera fallback = null;

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsUsablePlaybackCamera(camera))
                continue;

            if (camera.name == "CAM_Main")
                return camera;

            if (camera.CompareTag("MainCamera"))
                fallback = camera;
            else if (fallback == null)
                fallback = camera;
        }

        return fallback;
    }

    private static bool IsUsablePlaybackCamera(Camera camera)
    {
        return camera != null &&
               camera.isActiveAndEnabled &&
               camera.targetTexture == null;
    }

    private void RefreshVideoCamera()
    {
        if (blizzardVideoPlayer == null)
        {
            if (EnsureVideoPlayer())
                ApplyPendingVideoState();

            return;
        }

        if (IsUsablePlaybackCamera(blizzardVideoPlayer.targetCamera))
        {
            return;
        }

        if (TryAssignVideoCamera())
            ApplyPendingVideoState();
        else
            blizzardVideoPlayer.targetCameraAlpha = 0f;
    }

    private void WarnMissingVideoCamera(string reason)
    {
        if (_missingVideoCameraWarningShown)
            return;

        _missingVideoCameraWarningShown = true;
        Debug.LogWarning($"[{nameof(WindStateManager)}] El vídeo de ventisca no puede mostrarse. {reason}", this);
    }

    private void SubscribeToVideoPlayer()
    {
        if (_subscribedVideoPlayer == blizzardVideoPlayer)
            return;

        UnsubscribeFromVideoPlayer();
        _videoPrepareRequested = false;
        _videoSeekPending = false;
        _videoNeedsSeek = true;
        _subscribedVideoPlayer = blizzardVideoPlayer;
        _subscribedVideoPlayer.prepareCompleted += OnVideoPrepared;
        _subscribedVideoPlayer.seekCompleted += OnVideoSeekCompleted;
        _subscribedVideoPlayer.loopPointReached += OnVideoLoopPointReached;
        _subscribedVideoPlayer.errorReceived += OnVideoError;
    }

    private void UnsubscribeFromVideoPlayer()
    {
        if (_subscribedVideoPlayer == null)
            return;

        _subscribedVideoPlayer.prepareCompleted -= OnVideoPrepared;
        _subscribedVideoPlayer.seekCompleted -= OnVideoSeekCompleted;
        _subscribedVideoPlayer.loopPointReached -= OnVideoLoopPointReached;
        _subscribedVideoPlayer.errorReceived -= OnVideoError;
        _subscribedVideoPlayer = null;
    }

    private void ApplyVideoState(WindStateData state)
    {
        float newStartTime = Mathf.Max(0f, state.videoStartTime);
        float newEndTime = Mathf.Max(newStartTime + 0.05f, state.videoEndTime);
        bool segmentChanged =
            !Mathf.Approximately(_videoSegmentStartTime, newStartTime) ||
            !Mathf.Approximately(_videoSegmentEndTime, newEndTime);

        _videoShouldPlay = state.playVideo;
        _videoSegmentStartTime = newStartTime;
        _videoSegmentEndTime = newEndTime;
        _videoOpacity = Mathf.Clamp01(state.videoOpacity);
        _videoNeedsSeek |= segmentChanged;

        if (!EnsureVideoPlayer())
            return;

        ApplyPendingVideoState();
    }

    private void ApplyPendingVideoState()
    {
        if (blizzardVideoPlayer == null)
            return;

        blizzardVideoPlayer.targetCameraAlpha = _videoShouldPlay ? _videoOpacity : 0f;

        if (!_videoShouldPlay)
        {
            blizzardVideoPlayer.Pause();
            return;
        }

        if (blizzardVideoPlayer.isPrepared)
        {
            ClampSegmentToVideoLength();

            if (_videoNeedsSeek || !IsInsideCurrentSegment(blizzardVideoPlayer.time))
                SeekToSegmentStart();
            else if (!blizzardVideoPlayer.isPlaying && !_videoSeekPending)
                blizzardVideoPlayer.Play();

            return;
        }

        if (!_videoPrepareRequested)
        {
            _videoPrepareRequested = true;
            blizzardVideoPlayer.Prepare();
        }
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        if (source != blizzardVideoPlayer)
            return;

        _videoPrepareRequested = false;
        ClampSegmentToVideoLength();
        ApplyPendingVideoState();
    }

    private void OnVideoSeekCompleted(VideoPlayer source)
    {
        if (source != blizzardVideoPlayer)
            return;

        _videoSeekPending = false;

        if (!Mathf.Approximately(_videoPendingSeekTime, _videoSegmentStartTime))
        {
            _videoNeedsSeek = true;
            SeekToSegmentStart();
            return;
        }

        _videoNeedsSeek = false;
        if (_videoShouldPlay && !source.isPlaying)
            source.Play();
    }

    private void OnVideoLoopPointReached(VideoPlayer source)
    {
        if (source == blizzardVideoPlayer && _videoShouldPlay)
            SeekToSegmentStart();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        if (source != blizzardVideoPlayer)
            return;

        _videoPrepareRequested = false;
        _videoSeekPending = false;
        Debug.LogWarning($"[{nameof(WindStateManager)}] No se pudo reproducir el vídeo de ventisca: {message}", this);
    }

    private void KeepVideoInsideCurrentSegment()
    {
        if (blizzardVideoPlayer == null ||
            !blizzardVideoPlayer.isPrepared ||
            !_videoShouldPlay ||
            _videoSeekPending)
        {
            return;
        }

        double frameDuration = blizzardVideoPlayer.frameRate > 0f
            ? 1d / blizzardVideoPlayer.frameRate
            : 0.02d;

        if (_videoNeedsSeek ||
            blizzardVideoPlayer.time < _videoSegmentStartTime ||
            blizzardVideoPlayer.time >= _videoSegmentEndTime - frameDuration)
        {
            SeekToSegmentStart();
        }
    }

    private bool IsInsideCurrentSegment(double time)
    {
        return time >= _videoSegmentStartTime && time < _videoSegmentEndTime;
    }

    private void ClampSegmentToVideoLength()
    {
        float videoLength = (float)blizzardVideoPlayer.length;
        if (videoLength <= 0f)
            return;

        _videoSegmentStartTime = Mathf.Clamp(_videoSegmentStartTime, 0f, Mathf.Max(0f, videoLength - 0.05f));
        _videoSegmentEndTime = Mathf.Clamp(_videoSegmentEndTime, _videoSegmentStartTime + 0.05f, videoLength);
    }

    private void SeekToSegmentStart()
    {
        if (blizzardVideoPlayer == null || !blizzardVideoPlayer.isPrepared || _videoSeekPending)
            return;

        _videoSeekPending = true;
        _videoPendingSeekTime = _videoSegmentStartTime;
        blizzardVideoPlayer.time = _videoPendingSeekTime;

        if (_videoShouldPlay && !blizzardVideoPlayer.isPlaying)
            blizzardVideoPlayer.Play();
    }

    private bool HasAllWindStates(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        foreach (var windState in windStates)
        {
            if (!AnimatorContainsState(animator, windState.stateName))
                return false;
        }

        return true;
    }

    private static bool AnimatorContainsState(Animator animator, string stateName)
    {
        int shortHash = Animator.StringToHash(stateName);
        int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");
        return animator.HasState(0, shortHash) || animator.HasState(0, fullPathHash);
    }

    private static bool HasMissingReferences<T>(T[] references) where T : Object
    {
        if (references == null || references.Length == 0)
            return true;

        foreach (var reference in references)
        {
            if (reference == null)
                return true;
        }

        return false;
    }

    private void CrossFadeAll(string stateName, float transitionTime)
    {
        if (windAnimators == null) return;

        foreach (var anim in windAnimators)
        {
            if (anim != null)
                anim.CrossFade(stateName, transitionTime, 0);
        }
    }

    private void SetEmission(float rate)
    {
        if (blizzardParticles == null) return;
        var emission = blizzardParticles.emission;
        emission.rateOverTime = rate;
    }

    // Teclas 1 y 4 = árboles rápidos. Teclas 2 y 3 = árboles lentos.
    private void SetTreeWindSpeed(WindPreset preset)
    {
        bool fast = preset == WindPreset.W1_MaxIdle || preset == WindPreset.W4_MinToMedium;
        SetTreeWindSpeed(fast);
    }

    private void SetTreeWindSpeed(bool fast)
    {
        if (treeControllers == null) return;
        foreach (var tree in treeControllers)
            if (tree != null) tree.SetFast(fast);
    }
}
