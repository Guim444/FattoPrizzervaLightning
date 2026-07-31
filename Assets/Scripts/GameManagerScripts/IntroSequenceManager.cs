using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroSequenceManager : MonoBehaviour
{
    private static readonly int IdleFrontHash = Animator.StringToHash("IdleFront");
    private static readonly int IdleFrontHumanHash = Animator.StringToHash("IdleFrontHuman");
    private static readonly int TransformToGhostHash = Animator.StringToHash("TransformToGhost");

    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController playerCC;
    [SerializeField] private PlayerBoundaryClamp boundaryClamp;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private HUDManager hudManager;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("Dialogue Placement")]
    [Tooltip("Superficie sobre la que se coloca al jugador al activar la escena de diálogo.")]
    [SerializeField] private Transform dialoguePlayerPlatform;
    [SerializeField, Min(0f)] private float dialoguePlayerPlatformClearance = 0.02f;

    [Header("Dialogue Camera")]
    [Tooltip("Altura mundial fija de la cámara durante el diálogo.")]
    [SerializeField] private float dialogueCameraWorldHeight = 2.5f;
    [SerializeField, Range(1f, 179f)] private float dialogueCameraFieldOfView = 15f;
    [SerializeField] private Vector3 dialogueCameraWorldRotation =
        new Vector3(-6f, 89.006f, -0.108f);
    [SerializeField] private int dialogueCameraPriority = 100;
    [SerializeField, Range(0.1f, 1f)] private float dialogueCameraFramingSize = 0.95f;
    [SerializeField, Min(0f)] private float dialogueCameraTargetRadius = 0.5f;
    [SerializeField, Min(0.01f)] private float dialogueCameraInitialDistance = 6f;
    [SerializeField, Min(0.01f)] private float dialogueCameraMinimumDistance = 1f;
    [SerializeField, Min(0.01f)] private float dialogueCameraMaximumDistance = 30f;
    [Tooltip("Valor positivo acerca la cámara al encuadre.")]
    [SerializeField] private float dialogueCameraDistanceOffset;
    [SerializeField, Min(0f)] private float dialogueCameraPositionDamping = 0.15f;
    [SerializeField, Min(0f)] private float dialogueCameraBlendDuration = 2f;

    [Header("Camera — Intro FOV")]
    [SerializeField] private CinemachineVirtualCamera introVirtualCamera;
    [SerializeField] private float introStartFOV  = 30f;
    [SerializeField] private float introEndFOV    = 60f;
    [SerializeField] private float introCameraTargetYOffset = 2.2f;
    [SerializeField] private Color fadeEndBgColor = Color.black;

    [Header("Phase 1 — Black Screen")]
    [SerializeField] private float holdBlackDuration = 1f;

    [Header("Blizzard — Intro")]
    [SerializeField] private bool activateBlizzardOnIntro = true;
    [SerializeField] private float introBlizzardEmissionRate = 5500f;
    [SerializeField] private float introBlizzardPrewarmTime = 4f;
    [SerializeField] private bool anchorIntroBlizzardToCamera = true;
    [Tooltip("Mantiene la ventisca pegada visualmente a pantalla para evitar parallax al cambiar la Z de camara.")]
    [SerializeField] private bool stabilizeIntroBlizzardOnScreen = true;
    [Tooltip("Evita que la ventisca copie el desplazamiento lateral de la camara en Z.")]
    [SerializeField] private bool lockIntroBlizzardWorldZ = false;
    [SerializeField] private float introBlizzardCameraDistance = 15f;
    [SerializeField] private Vector3 introBlizzardCameraOffset = Vector3.zero;
    [SerializeField] private Vector3 introBlizzardShapeScale = new Vector3(95f, 55f, 25f);

    [Header("Phase 2 — Fade + auto movement")]
    [SerializeField] private float fadeInDuration = 4f;
    [SerializeField] private float fadeExponent   = 2f;
    [SerializeField] private Color introPlayerFadeTint = Color.white;
    [SerializeField] private float introPlayerFadeDelay = 0.25f;
    [SerializeField] private float introPlayerFadeDuration = 2f;
    [Tooltip("Mueve al jugador automáticamente en X mientras se revela durante el fade.")]
    [SerializeField] private bool autoWalkEnabled = true;
    [SerializeField] private float autoWalkSpeed  = 3f;
    [SerializeField] private float introPlayerZ   = 0f;

    [Header("Blink — Position")]
    [SerializeField] private float blinkStartX   = -100f;
    [SerializeField] private float blinkEndX     = 0f;
    [SerializeField] private float blinkInterval = 0.5f;

    [Header("Blink — Human ratio per quarter (1=always human, 0=always soul)")]
    [SerializeField] private float ratioQ1 = 1.00f;
    [SerializeField] private float ratioQ2 = 0.85f;
    [SerializeField] private float ratioQ3 = 0.50f;
    [SerializeField] private float ratioQ4 = 0.15f;

    [Header("Blink — Form Lock")]
    [SerializeField] private bool lockHumanForm = false;

    [Header("Animator — Intro Controller")]
    [SerializeField] private RuntimeAnimatorController introAnimatorController;

    private RuntimeAnimatorController _originalAnimatorController;
    private Material[] _originalMaterials;
    private Material[] _playerFadeMaterials;
    private SpriteRenderer[] _playerSpriteRenderers;
    private Color[] _originalRendererColors;
    private bool[] _originalRendererEnabledStates;
    private Coroutine _blinkCoroutine;
    private Coroutine _churchSequenceCoroutine;
    private Coroutine _dialogueReturnCoroutine;
    private CinemachineFramingTransposer _introFramingTransposer;
    private Vector3 _originalIntroTrackedObjectOffset;
    private CameraClearFlags _originalClearFlags;
    private float _activePlayerFadeAlpha = -1f;
    private PlayerAnimations _playerAnimations;
    private RuntimeAnimatorController _cachedAnimatorController;
    private DialogueCameraController dialogueCameraController;
    private bool _originalPauseAnimatorControl;
    private bool _hasOriginalPauseAnimatorControl;
    private bool _playerFadeMaterialRestored;
    private bool _isIntroBlizzardCameraAnchored;
    private bool _hasIntroBlizzardWorldZAnchor;
    private float _introBlizzardWorldZAnchor;
    private bool _hasOriginalIntroTrackedObjectOffset;
    private readonly Dictionary<int, AnimatorControllerParameterType> _animatorParameterTypes =
        new Dictionary<int, AnimatorControllerParameterType>();
    private readonly List<ParticleSystem> _introBlizzardParticles = new List<ParticleSystem>();

    public float IntroPlayerZ => introPlayerZ;


    private bool isWalking;
    private bool played;

    private float _timer;

    

    private void Awake()
    {
        if (playerSpriteRenderer == null && playerTransform != null)
            playerSpriteRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>();

        if (playerController == null && playerTransform != null)
            playerController = playerTransform.GetComponent<PlayerController>();

        CachePlayerSpriteRenderers();
        CacheOriginalMaterials();
        CacheOriginalColors();
        CachePlayerAnimations();

        if (mainCamera != null)
            _originalClearFlags = mainCamera.clearFlags;

        CacheIntroCameraFraming();
    }

    private void LateUpdate()
    {
        if (_activePlayerFadeAlpha >= 0f)
            SetPlayerFadeColor(_activePlayerFadeAlpha);

        if (_isIntroBlizzardCameraAnchored)
            PositionIntroBlizzardParticles();
    }

    private void Update()
    {
        if (isWalking && !played)
        {
            _timer += Time.deltaTime;

            if (_timer >= 5f)
            {
                SetPlayerAnimatorTriggerIfAvailable(TransformToGhostHash);
                played = true;
            }
        }
    }

    public void ShowBlackScreen()
    {
        PauseGameplayAnimatorControl();

        if (playerController != null)
        {
            playerController.ResetToIdle();
            playerController.enabled = false;
        }

        if (mainCamera != null)
        {
            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
            introVirtualCamera.m_Lens.FieldOfView = introStartFOV;
        }

        if (playerAnimator != null)     playerAnimator.enabled = false;
        if (playerInputHandler != null) playerInputHandler.enabled = false;
        if (playerTransform != null)    playerTransform.gameObject.SetActive(false);
    }

    public void StartIntro()
    {
        if (activateBlizzardOnIntro)
            ActivateIntroBlizzard();

        StartCoroutine(PlayIntroSequence());
    }

    private void ActivateIntroBlizzard()
    {
        WindStateManager.ActivateAllVideoPlayersRoots();
        _introBlizzardParticles.Clear();
        _hasIntroBlizzardWorldZAnchor = false;

        ParticleSystem[] blizzardParticles = Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (ParticleSystem particles in blizzardParticles)
        {
            if (particles == null || particles.name != "SnowStorm") continue;

            PrepareIntroBlizzardParticles(particles);
        }
    }

    private void PrepareIntroBlizzardParticles(ParticleSystem particles)
    {
        ActivateHierarchy(particles.transform);

        if (!_introBlizzardParticles.Contains(particles))
            _introBlizzardParticles.Add(particles);

        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.None;
        main.startDelay = 0f;
        main.useUnscaledTime = true;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        if (anchorIntroBlizzardToCamera)
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = introBlizzardEmissionRate;

        if (anchorIntroBlizzardToCamera)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.position = Vector3.zero;
            shape.scale = introBlizzardShapeScale;

            _isIntroBlizzardCameraAnchored = true;
            PositionIntroBlizzardParticle(particles);
        }

        particles.Clear(true);

        if (introBlizzardPrewarmTime > 0f)
            particles.Simulate(introBlizzardPrewarmTime, true, true, true);

        particles.Play(true);
    }

    private void PositionIntroBlizzardParticles()
    {
        for (int i = _introBlizzardParticles.Count - 1; i >= 0; i--)
        {
            ParticleSystem particles = _introBlizzardParticles[i];
            if (particles == null)
            {
                _introBlizzardParticles.RemoveAt(i);
                continue;
            }

            PositionIntroBlizzardParticle(particles);
        }
    }

    private void PositionIntroBlizzardParticle(ParticleSystem particles)
    {
        if (particles == null || mainCamera == null) return;

        Transform cameraTransform = mainCamera.transform;
        Vector3 localOffset = introBlizzardCameraOffset + Vector3.forward * introBlizzardCameraDistance;
        Vector3 targetPosition = cameraTransform.TransformPoint(localOffset);

        if (!stabilizeIntroBlizzardOnScreen && lockIntroBlizzardWorldZ)
        {
            if (!_hasIntroBlizzardWorldZAnchor)
            {
                _introBlizzardWorldZAnchor = targetPosition.z;
                _hasIntroBlizzardWorldZAnchor = true;
            }

            targetPosition.z = _introBlizzardWorldZAnchor;
        }

        particles.transform.position = targetPosition;
        particles.transform.rotation = cameraTransform.rotation;
    }

    private void ActivateHierarchy(Transform target)
    {
        while (target != null)
        {
            target.gameObject.SetActive(true);
            target = target.parent;
        }
    }

    private IEnumerator PlayIntroSequence()
    {
        PauseGameplayAnimatorControl();
        ActivatePlayerInvisible();

        yield return StartCoroutine(HoldBlack());
        yield return StartCoroutine(FadeFromBlack());

        KeepPlayerAtIntroDepth();
        if (playerCC != null) playerCC.enabled = true;
        if (playerInputHandler != null) playerInputHandler.enabled = true;
        if (playerController != null) playerController.enabled = true;

        _blinkCoroutine = StartCoroutine(BlinkCoroutine());
    }

    private IEnumerator HoldBlack()
    {
        if (holdBlackDuration <= 0f) yield break;

        if (mainCamera != null)
            introVirtualCamera.m_Lens.FieldOfView = introStartFOV;

        yield return new WaitForSecondsRealtime(holdBlackDuration);
    }

    private void ActivatePlayerInvisible()
    {
        if (playerTransform == null) return;
        if (playerCC != null) playerCC.enabled = false;
        KeepPlayerAtIntroDepth();
        Physics.SyncTransforms();
    }

    private IEnumerator FadeFromBlack()
    {
        CachePlayerSpriteRenderers();
        CacheRendererEnabledStates();
        SetPlayerRenderersEnabled(false);
        playerTransform.gameObject.SetActive(true);
        ApplyIntroCameraFraming();
        ApplyPlayerFadeMaterial();
        _playerFadeMaterialRestored = false;
        SetPlayerFadeAlpha(0f);
        RestorePlayerRenderersEnabled();

        if (playerAnimator != null && introAnimatorController != null)
        {
            _originalAnimatorController = playerAnimator.runtimeAnimatorController;
            playerAnimator.runtimeAnimatorController = introAnimatorController;
        }

        if (playerAnimator != null)
        {
            playerAnimator.enabled = true;
            SetHumanForm();
        }

        yield return null;
        SetPlayerFadeAlpha(0f);

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t      = Mathf.Clamp01(elapsed / fadeInDuration);
            float reveal = Mathf.Pow(t, fadeExponent);

            if (mainCamera != null)
            {
                mainCamera.backgroundColor = Color.Lerp(Color.black, fadeEndBgColor, reveal);
                introVirtualCamera.m_Lens.FieldOfView = Mathf.Lerp(introStartFOV, introEndFOV, reveal);
            }

            if (!_playerFadeMaterialRestored)
            {
                float playerFadeAlpha = GetIntroPlayerFadeAlpha(elapsed);
                SetPlayerFadeAlpha(playerFadeAlpha);

                if (playerFadeAlpha >= 0.999f)
                    CompletePlayerFadeMaterial();
            }

            if (autoWalkEnabled)
            {
                Vector3 pos = playerTransform.position;
                pos.x += autoWalkSpeed * Time.unscaledDeltaTime;
                pos.z = introPlayerZ;
                playerTransform.position = pos;
            }
            else
            {
                KeepPlayerAtIntroDepth();
            }

            yield return null;
        }

        CompletePlayerFadeMaterial();
        RestoreIntroCameraFraming();
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = fadeEndBgColor;
            mainCamera.clearFlags      = _originalClearFlags;
        }
        played = false;
        isWalking = true;
    }

    private float GetIntroPlayerFadeAlpha(float elapsed)
    {
        if (introPlayerFadeDuration <= 0f)
            return 1f;

        float t = Mathf.Clamp01((elapsed - introPlayerFadeDelay) / introPlayerFadeDuration);
        return Mathf.SmoothStep(0f, 1f, t);
    }

    private void CompletePlayerFadeMaterial()
    {
        if (_playerFadeMaterialRestored)
        {
            _activePlayerFadeAlpha = -1f;
            return;
        }

        SetPlayerFadeAlpha(1f);
        _activePlayerFadeAlpha = -1f;
        RestoreOriginalColors();
        RestoreOriginalMaterials();
        _playerFadeMaterialRestored = true;
    }

    private void KeepPlayerAtIntroDepth()
    {
        if (playerTransform == null) return;

        Vector3 pos = playerTransform.position;
        pos.z = introPlayerZ;
        playerTransform.position = pos;
    }

    private void CacheIntroCameraFraming()
    {
        if (_introFramingTransposer != null || introVirtualCamera == null) return;

        _introFramingTransposer = introVirtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (_introFramingTransposer == null) return;

        _originalIntroTrackedObjectOffset = _introFramingTransposer.m_TrackedObjectOffset;
        _hasOriginalIntroTrackedObjectOffset = true;
    }

    private void ApplyIntroCameraFraming()
    {
        CacheIntroCameraFraming();
        if (_introFramingTransposer == null) return;

        if (!_hasOriginalIntroTrackedObjectOffset)
        {
            _originalIntroTrackedObjectOffset = _introFramingTransposer.m_TrackedObjectOffset;
            _hasOriginalIntroTrackedObjectOffset = true;
        }

        Vector3 offset = _originalIntroTrackedObjectOffset;
        offset.y = introCameraTargetYOffset;
        _introFramingTransposer.m_TrackedObjectOffset = offset;
    }

    private void RestoreIntroCameraFraming()
    {
        CacheIntroCameraFraming();
        if (_introFramingTransposer == null || !_hasOriginalIntroTrackedObjectOffset) return;

        _introFramingTransposer.m_TrackedObjectOffset = _originalIntroTrackedObjectOffset;
    }

    private void SetPlayerFadeAlpha(float alpha)
    {
        _activePlayerFadeAlpha = Mathf.Clamp01(alpha);
        SetPlayerFadeColor(_activePlayerFadeAlpha);
    }

    private void SetPlayerFadeColor(float alpha)
    {
        CachePlayerSpriteRenderers();
        if (_playerSpriteRenderers == null) return;

        float clampedAlpha = Mathf.Clamp01(alpha);
        for (int i = 0; i < _playerSpriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = _playerSpriteRenderers[i];
            if (renderer == null) continue;

            Color baseColor = GetOriginalRendererColor(i);
            Color fadeColor = new Color(
                baseColor.r * introPlayerFadeTint.r,
                baseColor.g * introPlayerFadeTint.g,
                baseColor.b * introPlayerFadeTint.b,
                baseColor.a * introPlayerFadeTint.a * clampedAlpha
            );

            renderer.color = fadeColor;
        }
    }

    private void SetPlayerRenderersEnabled(bool enabled)
    {
        CachePlayerSpriteRenderers();
        if (_playerSpriteRenderers == null) return;

        foreach (SpriteRenderer renderer in _playerSpriteRenderers)
            if (renderer != null) renderer.enabled = enabled;
    }

    private void CacheRendererEnabledStates()
    {
        CachePlayerSpriteRenderers();
        if (_playerSpriteRenderers == null) return;

        _originalRendererEnabledStates = new bool[_playerSpriteRenderers.Length];

        for (int i = 0; i < _playerSpriteRenderers.Length; i++)
            _originalRendererEnabledStates[i] = _playerSpriteRenderers[i] != null && _playerSpriteRenderers[i].enabled;
    }

    private void RestorePlayerRenderersEnabled()
    {
        CachePlayerSpriteRenderers();
        if (_playerSpriteRenderers == null) return;

        if (_originalRendererEnabledStates == null ||
            _originalRendererEnabledStates.Length != _playerSpriteRenderers.Length)
        {
            SetPlayerRenderersEnabled(true);
            return;
        }

        for (int i = 0; i < _playerSpriteRenderers.Length; i++)
            if (_playerSpriteRenderers[i] != null)
                _playerSpriteRenderers[i].enabled = _originalRendererEnabledStates[i];
    }

    private void ApplyPlayerFadeMaterial()
    {
        CachePlayerSpriteRenderers();
        CacheOriginalMaterials();
        if (_playerSpriteRenderers == null || _originalMaterials == null) return;

        if (_playerFadeMaterials == null || _playerFadeMaterials.Length != _playerSpriteRenderers.Length)
            _playerFadeMaterials = new Material[_playerSpriteRenderers.Length];

        int count = Mathf.Min(_playerSpriteRenderers.Length, _originalMaterials.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = _playerSpriteRenderers[i];
            if (renderer == null) continue;

            Material sourceMaterial = _originalMaterials[i] != null ? _originalMaterials[i] : renderer.sharedMaterial;
            if (sourceMaterial == null) continue;

            if (_playerFadeMaterials[i] == null)
                _playerFadeMaterials[i] = CreatePlayerFadeMaterial(sourceMaterial);

            renderer.sharedMaterial = _playerFadeMaterials[i];
        }
    }

    private Material CreatePlayerFadeMaterial(Material sourceMaterial)
    {
        Shader fadeShader = ResolveSpriteFadeShader(sourceMaterial);
        Material material = fadeShader != null ? new Material(fadeShader) : new Material(sourceMaterial);
        material.name = sourceMaterial.name + "_IntroFade";

        CopyTextureIfPresent(sourceMaterial, material, "_MainTex");
        CopyTextureIfPresent(sourceMaterial, material, "_BaseMap");
        CopyColorIfPresent(sourceMaterial, material, "_Color");
        CopyColorIfPresent(sourceMaterial, material, "_BaseColor");
        ConfigurePlayerFadeMaterial(material);
        return material;
    }

    private Shader ResolveSpriteFadeShader(Material sourceMaterial)
    {
        Shader spriteLitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (spriteLitShader != null)
            return spriteLitShader;

        Shader spriteDefaultShader = Shader.Find("Sprites/Default");
        if (spriteDefaultShader != null)
            return spriteDefaultShader;

        if (sourceMaterial != null && sourceMaterial.HasProperty("_MainTex"))
            return sourceMaterial.shader;

        Debug.LogWarning("[IntroSequenceManager] No se encontro un shader de SpriteRenderer compatible para el fade del player.", this);
        return null;
    }

    private void ConfigurePlayerFadeMaterial(Material material)
    {
        if (material == null) return;

        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0f);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private void CopyTextureIfPresent(Material source, Material target, string propertyName)
    {
        if (source == null || target == null) return;
        if (!source.HasProperty(propertyName) || !target.HasProperty(propertyName)) return;

        target.SetTexture(propertyName, source.GetTexture(propertyName));
        target.SetTextureScale(propertyName, source.GetTextureScale(propertyName));
        target.SetTextureOffset(propertyName, source.GetTextureOffset(propertyName));
    }

    private void CopyColorIfPresent(Material source, Material target, string propertyName)
    {
        if (source == null || target == null) return;
        if (!source.HasProperty(propertyName) || !target.HasProperty(propertyName)) return;

        target.SetColor(propertyName, source.GetColor(propertyName));
    }

    private Color GetOriginalRendererColor(int index)
    {
        if (_originalRendererColors != null &&
            index >= 0 &&
            index < _originalRendererColors.Length)
        {
            return _originalRendererColors[index];
        }

        if (_playerSpriteRenderers != null &&
            index >= 0 &&
            index < _playerSpriteRenderers.Length &&
            _playerSpriteRenderers[index] != null)
        {
            return _playerSpriteRenderers[index].color;
        }

        return Color.white;
    }

    private void RestoreOriginalMaterials()
    {
        CachePlayerSpriteRenderers();
        if (_playerSpriteRenderers == null || _originalMaterials == null) return;

        int count = Mathf.Min(_playerSpriteRenderers.Length, _originalMaterials.Length);
        for (int i = 0; i < count; i++)
        {
            if (_playerSpriteRenderers[i] != null && _originalMaterials[i] != null)
                _playerSpriteRenderers[i].sharedMaterial = _originalMaterials[i];
        }
    }

    private void RestoreOriginalColors()
    {
        CachePlayerSpriteRenderers();
        if (_playerSpriteRenderers == null || _originalRendererColors == null) return;

        int count = Mathf.Min(_playerSpriteRenderers.Length, _originalRendererColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (_playerSpriteRenderers[i] != null)
                _playerSpriteRenderers[i].color = _originalRendererColors[i];
        }
    }

    private void CachePlayerSpriteRenderers()
    {
        if (playerTransform == null) return;
        if (_playerSpriteRenderers != null && _playerSpriteRenderers.Length > 0) return;

        _playerSpriteRenderers = playerTransform.GetComponentsInChildren<SpriteRenderer>(true);

        if (playerSpriteRenderer == null && _playerSpriteRenderers.Length > 0)
            playerSpriteRenderer = _playerSpriteRenderers[0];
    }

    private void CacheOriginalMaterials()
    {
        if (_playerSpriteRenderers == null) return;
        if (_originalMaterials != null && _originalMaterials.Length == _playerSpriteRenderers.Length) return;

        _originalMaterials = new Material[_playerSpriteRenderers.Length];

        for (int i = 0; i < _playerSpriteRenderers.Length; i++)
        {
            if (_playerSpriteRenderers[i] != null)
                _originalMaterials[i] = _playerSpriteRenderers[i].sharedMaterial;
        }
    }

    private void CacheOriginalColors()
    {
        if (_playerSpriteRenderers == null) return;
        if (_originalRendererColors != null && _originalRendererColors.Length == _playerSpriteRenderers.Length) return;

        _originalRendererColors = new Color[_playerSpriteRenderers.Length];

        for (int i = 0; i < _playerSpriteRenderers.Length; i++)
        {
            if (_playerSpriteRenderers[i] != null)
                _originalRendererColors[i] = _playerSpriteRenderers[i].color;
        }
    }

    private IEnumerator BlinkCoroutine()
    {
        while (true)
        {
            if (lockHumanForm)
            {
                SetHumanForm();
                yield return null;
                continue;
            }

            float ratio = GetHumanRatio();

            if (ratio >= 1f)
            {
                SetHumanForm();
                yield return null;
                continue;
            }

            float humanTime = ratio * blinkInterval;
            float soulTime  = (1f - ratio) * blinkInterval;

            SetHumanForm();
            if (humanTime > 0.01f) yield return new WaitForSeconds(humanTime);

            SetSoulForm();
            if (soulTime > 0.01f) yield return new WaitForSeconds(soulTime);
        }
    }

    private void SetHumanForm()
    {
        if (playerAnimator == null || !playerAnimator.enabled) return;
        SetPlayerAnimatorBoolIfAvailable(IdleFrontHumanHash, true);
        SetPlayerAnimatorBoolIfAvailable(IdleFrontHash, false);
    }

    private void SetSoulForm()
    {
        if (playerAnimator == null || !playerAnimator.enabled) return;
        SetPlayerAnimatorBoolIfAvailable(IdleFrontHash, true);
        SetPlayerAnimatorBoolIfAvailable(IdleFrontHumanHash, false);
    }

    private float GetHumanRatio()
    {
        float x             = playerTransform.position.x;
        float quarterLength = (blinkEndX - blinkStartX) / 4f;

        if (x < blinkStartX + quarterLength)      return ratioQ1;
        if (x < blinkStartX + quarterLength * 2f) return ratioQ2;
        if (x < blinkStartX + quarterLength * 3f) return ratioQ3;
        return ratioQ4;
    }

    public void RestoreOriginalAnimator()
    {
        if (playerAnimator != null && _originalAnimatorController != null)
            playerAnimator.runtimeAnimatorController = _originalAnimatorController;

        RestoreGameplayAnimatorControl();
        _activePlayerFadeAlpha = -1f;
        _isIntroBlizzardCameraAnchored = false;
        RestoreIntroCameraFraming();
        RestoreOriginalColors();
        RestoreOriginalMaterials();
    }

    private void OnDisable()
    {
        RestoreGameplayAnimatorControl();
    }

    public bool TryStartChurchSequence(
        Transform firstTarget,
        float firstDuration,
        Transform secondTarget,
        float secondDuration,
        Transform rioTutteStandard,
        Transform rioTutteTransformation,
        float autoMoveCameraY,
        string dialogueSceneName,
        string lightingSceneName)
    {
        if (_churchSequenceCoroutine != null)
            return false;

        if (firstTarget == null || secondTarget == null ||
            rioTutteStandard == null || rioTutteTransformation == null)
        {
            Debug.LogError(
                $"[{nameof(IntroSequenceManager)}] Asigna los dos puntos de automove y las dos variantes visuales de RioTutte.",
                this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(dialogueSceneName) ||
            !SceneManager.GetSceneByName(dialogueSceneName).isLoaded)
        {
            Debug.LogError(
                $"[{nameof(IntroSequenceManager)}] La escena de diálogo '{dialogueSceneName}' no está cargada.",
                this);
            return false;
        }

        if (string.IsNullOrWhiteSpace(lightingSceneName) ||
            !SceneManager.GetSceneByName(lightingSceneName).isLoaded)
        {
            Debug.LogError(
                $"[{nameof(IntroSequenceManager)}] La escena de iluminación '{lightingSceneName}' no está cargada.",
                this);
            return false;
        }

        if (dialogueSceneName == lightingSceneName)
        {
            Debug.LogError(
                $"[{nameof(IntroSequenceManager)}] Las escenas de diálogo e iluminación deben tener nombres distintos.",
                this);
            return false;
        }

        _churchSequenceCoroutine = StartCoroutine(EnterChurchSequence(
            firstTarget,
            firstDuration,
            secondTarget,
            secondDuration,
            rioTutteStandard,
            rioTutteTransformation,
            autoMoveCameraY,
            dialogueSceneName,
            lightingSceneName));
        return true;
    }

    private IEnumerator EnterChurchSequence(
        Transform firstTarget,
        float firstDuration,
        Transform secondTarget,
        float secondDuration,
        Transform rioTutteStandard,
        Transform rioTutteTransformation,
        float autoMoveCameraY,
        string dialogueSceneName,
        string lightingSceneName)
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }

        RestoreOriginalAnimator();
        PauseGameplayAnimatorControl();

        if (playerAnimator != null) playerAnimator.enabled = true;
        SetSoulForm();

        if (playerInputHandler != null) playerInputHandler.enabled = false;
        if (playerController != null) playerController.enabled = false;
        if (boundaryClamp != null) boundaryClamp.enabled = false;

        Coroutine cameraLowering = StartCoroutine(
            LowerCameraDuringAutoMove(autoMoveCameraY, firstDuration));
        yield return StartCoroutine(MovePlayerToPosition(firstTarget.position, firstDuration));

        ShowRioTutteTransformation(rioTutteStandard, rioTutteTransformation);
        ApplyEnvironmentPhase(3);

        yield return StartCoroutine(MovePlayerToPosition(secondTarget.position, secondDuration));

        if (cameraLowering != null)
            StopCoroutine(cameraLowering);

        RestoreIntroCameraFraming();

        if (!SwitchLoadedSceneRoots(lightingSceneName, dialogueSceneName))
        {
            hudManager?.CompleteChurchTestCameraTransition();
            ResumePlayerAfterChurchSequence();
            _churchSequenceCoroutine = null;
            yield break;
        }

        PlacePlayerOnDialoguePlatform();

        bool dialogueCameraActivated = false;
        EnsureDialogueCameraController();
        if (dialogueCameraController != null)
        {
            hudManager?.PrepareDialogueCameraTransition(
                dialogueCameraBlendDuration);
            dialogueCameraActivated = dialogueCameraController.ActivateCamera(
                playerTransform,
                rioTutteTransformation);

            if (!dialogueCameraActivated)
                hudManager?.CompleteChurchTestCameraTransition();
        }
        else
        {
            Debug.LogWarning(
                $"[{nameof(IntroSequenceManager)}] DialogueCameraController no está asignado.",
                this);
        }

        _churchSequenceCoroutine = null;
    }

    private bool SwitchLoadedSceneRoots(string sceneToHideName, string sceneToShowName)
    {
        Scene sceneToHide = SceneManager.GetSceneByName(sceneToHideName);
        Scene sceneToShow = SceneManager.GetSceneByName(sceneToShowName);

        if (!sceneToHide.isLoaded || !sceneToShow.isLoaded)
        {
            Debug.LogError(
                $"[{nameof(IntroSequenceManager)}] Para cambiar el entorno deben estar cargadas '{sceneToHideName}' y '{sceneToShowName}'.",
                this);
            return false;
        }

        foreach (GameObject root in sceneToHide.GetRootGameObjects())
            root.SetActive(false);

        foreach (GameObject root in sceneToShow.GetRootGameObjects())
            root.SetActive(true);

        return true;
    }

    public bool ResumeAfterDialogueTest(
        string lightingSceneName,
        string dialogueSceneName)
    {
        if (_dialogueReturnCoroutine != null)
            return false;

        if (!SwitchLoadedSceneRoots(dialogueSceneName, lightingSceneName))
            return false;

        _dialogueReturnCoroutine = StartCoroutine(ReturnFromDialogueCamera());
        return true;
    }

    private IEnumerator ReturnFromDialogueCamera()
    {
        float blendDuration = 0f;
        if (dialogueCameraController != null)
        {
            blendDuration = dialogueCameraBlendDuration;
            dialogueCameraController.DeactivateCamera();
        }

        if (blendDuration > 0f)
            yield return new WaitForSecondsRealtime(blendDuration);

        hudManager?.CompleteChurchTestCameraTransition();
        ApplyEnvironmentPhase(3);
        ResumePlayerAfterChurchSequence();
        _dialogueReturnCoroutine = null;
    }

    private void EnsureDialogueCameraController()
    {
        if (dialogueCameraController == null)
        {
            GameObject cameraObject = new GameObject("CM_DialogueCamera");
            cameraObject.SetActive(false);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.AddComponent<CinemachineVirtualCamera>();

            dialogueCameraController =
                cameraObject.AddComponent<DialogueCameraController>();
        }

        ApplyDialogueCameraConfiguration();
    }

    private void ApplyDialogueCameraConfiguration()
    {
        if (dialogueCameraController == null)
            return;

        dialogueCameraController.Configure(
            dialogueCameraWorldHeight,
            dialogueCameraFieldOfView,
            dialogueCameraWorldRotation,
            dialogueCameraPriority,
            dialogueCameraFramingSize,
            dialogueCameraTargetRadius,
            dialogueCameraInitialDistance,
            dialogueCameraMinimumDistance,
            dialogueCameraMaximumDistance,
            dialogueCameraDistanceOffset,
            dialogueCameraPositionDamping,
            dialogueCameraBlendDuration);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            ApplyDialogueCameraConfiguration();
    }

    private void ResumePlayerAfterChurchSequence()
    {
        SetPlayerAnimatorBoolIfAvailable(IdleFrontHash, false);
        RestoreGameplayAnimatorControl();

        if (playerInputHandler != null) playerInputHandler.enabled = true;
        if (playerController != null) playerController.enabled = true;
    }

    private void ShowRioTutteTransformation(
        Transform rioTutteStandard,
        Transform rioTutteTransformation)
    {
        rioTutteStandard.gameObject.SetActive(false);
        rioTutteTransformation.gameObject.SetActive(true);
    }

    private IEnumerator LowerCameraDuringAutoMove(float targetWorldY, float duration)
    {
        CacheIntroCameraFraming();
        if (_introFramingTransposer == null || mainCamera == null)
            yield break;

        Vector3 startOffset = _introFramingTransposer.m_TrackedObjectOffset;
        Vector3 targetOffset = startOffset;
        targetOffset.y += targetWorldY - mainCamera.transform.position.y;

        if (duration <= 0f)
        {
            _introFramingTransposer.m_TrackedObjectOffset = targetOffset;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _introFramingTransposer.m_TrackedObjectOffset =
                Vector3.LerpUnclamped(startOffset, targetOffset, t);
            yield return null;
        }

        _introFramingTransposer.m_TrackedObjectOffset = targetOffset;
    }

    private IEnumerator MovePlayerToPosition(Vector3 target, float duration)
    {
        if (playerTransform == null)
            yield break;

        Vector3 start = playerTransform.position;
        float elapsed = 0f;

        if (playerCC != null) playerCC.enabled = false;

        if (duration <= 0f)
        {
            playerTransform.position = target;
            Physics.SyncTransforms();
            if (playerCC != null) playerCC.enabled = true;
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerTransform.position = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        playerTransform.position = target;
        Physics.SyncTransforms();
        if (playerCC != null) playerCC.enabled = true;
    }

    private void PlacePlayerOnDialoguePlatform()
    {
        if (playerTransform == null || dialoguePlayerPlatform == null)
        {
            Debug.LogWarning(
                $"[{nameof(IntroSequenceManager)}] No se puede colocar al jugador: falta Player o Dialogue Player Platform.",
                this);
            return;
        }

        Collider platformCollider =
            dialoguePlayerPlatform.GetComponentInChildren<Collider>();
        Vector3 targetPosition = dialoguePlayerPlatform.position;

        if (platformCollider != null)
        {
            Bounds platformBounds = platformCollider.bounds;
            targetPosition.x = platformBounds.center.x;
            targetPosition.y = platformBounds.max.y;
            targetPosition.z = platformBounds.center.z;
        }

        if (playerCC != null)
        {
            float playerBottomOffset =
                (playerCC.center.y - playerCC.height * 0.5f)
                * Mathf.Abs(playerTransform.lossyScale.y);
            targetPosition.y -= playerBottomOffset;
        }

        targetPosition.y += dialoguePlayerPlatformClearance;

        bool characterControllerWasEnabled =
            playerCC != null && playerCC.enabled;
        if (characterControllerWasEnabled)
            playerCC.enabled = false;

        playerTransform.position = targetPosition;
        Physics.SyncTransforms();

        if (characterControllerWasEnabled)
            playerCC.enabled = true;
    }

    private void ApplyEnvironmentPhase(int phase)
    {
        int activatedManagers = WindStateManager.ActivateAllVideoPlayersRoots();
        if (activatedManagers == 0)
            Debug.LogWarning(
                $"[{nameof(IntroSequenceManager)}] No se encontró ningún WindStateManager para activar la ventisca.",
                this);

        LightingStateManager[] lightingManagers = Object.FindObjectsByType<LightingStateManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        EnvironmentStateManager[] environmentManagers = Object.FindObjectsByType<EnvironmentStateManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (LightingStateManager manager in lightingManagers)
            manager.ApplyPhase(phase);

        foreach (EnvironmentStateManager manager in environmentManagers)
            manager.ApplyPhase(phase);

        if (lightingManagers.Length == 0 || environmentManagers.Length == 0)
        {
            Debug.LogWarning(
                $"[{nameof(IntroSequenceManager)}] La fase {phase} no encontró todos los gestores de iluminación y entorno activos.",
                this);
        }
    }

    private void CachePlayerAnimations()
    {
        if (_playerAnimations != null) return;

        if (playerController != null)
            _playerAnimations = playerController.GetComponent<PlayerAnimations>();

        if (_playerAnimations == null && playerTransform != null)
            _playerAnimations = playerTransform.GetComponent<PlayerAnimations>();
    }

    private void PauseGameplayAnimatorControl()
    {
        CachePlayerAnimations();
        if (_playerAnimations == null) return;

        if (!_hasOriginalPauseAnimatorControl)
        {
            _originalPauseAnimatorControl = _playerAnimations.pauseAnimatorControl;
            _hasOriginalPauseAnimatorControl = true;
        }

        _playerAnimations.pauseAnimatorControl = true;
    }

    private void RestoreGameplayAnimatorControl()
    {
        CachePlayerAnimations();
        if (_playerAnimations == null || !_hasOriginalPauseAnimatorControl) return;

        _playerAnimations.pauseAnimatorControl = _originalPauseAnimatorControl;
        _hasOriginalPauseAnimatorControl = false;
    }

    private void SetPlayerAnimatorBoolIfAvailable(int parameterHash, bool value)
    {
        if (HasPlayerAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
            playerAnimator.SetBool(parameterHash, value);
    }

    private void SetPlayerAnimatorTriggerIfAvailable(int parameterHash)
    {
        if (HasPlayerAnimatorParameter(parameterHash, AnimatorControllerParameterType.Trigger))
            playerAnimator.SetTrigger(parameterHash);
    }

    private bool HasPlayerAnimatorParameter(int parameterHash, AnimatorControllerParameterType expectedType)
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null)
            return false;

        if (_cachedAnimatorController != playerAnimator.runtimeAnimatorController)
        {
            _cachedAnimatorController = playerAnimator.runtimeAnimatorController;
            _animatorParameterTypes.Clear();

            foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
                _animatorParameterTypes[parameter.nameHash] = parameter.type;
        }

        return _animatorParameterTypes.TryGetValue(parameterHash, out AnimatorControllerParameterType actualType)
            && actualType == expectedType;
    }
}
