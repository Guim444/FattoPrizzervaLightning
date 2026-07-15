using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Video;

public class IntroSequenceManager : MonoBehaviour
{
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

    [Header("Animator — Intro Video Forms")]
    [SerializeField] private bool useIntroFormVideos = false;
    [SerializeField] private VideoClip introSoulWalkClip;
    [SerializeField] private VideoClip introHumanWalkClip;
    [SerializeField] private string introVideoSurfaceName = "IntroPlayerVideoSurface";
    [SerializeField] private Vector3 introVideoLocalOffset = Vector3.zero;
    [SerializeField] private Vector2 introVideoSizeMultiplier = Vector2.one;
    [Tooltip("Recorte UV del video humano: X=izquierda, Y=derecha, Z=abajo, W=arriba.")]
    [SerializeField] private Vector4 introHumanVideoUvCrop = Vector4.zero;
    [SerializeField, Min(0)] private int introVideoRenderTextureWidth = 0;
    [SerializeField, Min(0)] private int introVideoRenderTextureHeight = 0;
    [SerializeField] private bool keepAnimatorUntilIntroVideoPrepared = true;

    private RuntimeAnimatorController _originalAnimatorController;
    private Material[] _originalMaterials;
    private Material[] _playerFadeMaterials;
    private SpriteRenderer[] _playerSpriteRenderers;
    private Color[] _originalRendererColors;
    private bool[] _originalRendererEnabledStates;
    private Coroutine _blinkCoroutine;
    private CinemachineFramingTransposer _introFramingTransposer;
    private Vector3 _originalIntroTrackedObjectOffset;
    private CameraClearFlags _originalClearFlags;
    private float _activePlayerFadeAlpha = -1f;
    private bool _playerFadeMaterialRestored;
    private bool _isIntroBlizzardCameraAnchored;
    private bool _hasOriginalIntroTrackedObjectOffset;
    private readonly List<ParticleSystem> _introBlizzardParticles = new List<ParticleSystem>();
    private IntroVideoSlot _introSoulVideoSlot;
    private IntroVideoSlot _introHumanVideoSlot;
    private IntroVideoSlot _activeIntroVideoSlot;
    private MeshRenderer _introVideoRenderer;
    private Mesh _introVideoMesh;
    private Material _introVideoMaterial;

    private sealed class IntroVideoSlot
    {
        public VideoClip clip;
        public VideoPlayer player;
        public RenderTexture texture;
        public bool ready;
    }

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
        PrepareIntroFormVideos();

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

        if (_activeIntroVideoSlot != null)
            UpdateIntroVideoCropForActiveSlot();
    }

    private void OnDisable()
    {
        HideIntroFormVideo(restorePlayerRenderers: true);
    }

    private void OnDestroy()
    {
        ReleaseIntroVideoSlot(_introSoulVideoSlot);
        ReleaseIntroVideoSlot(_introHumanVideoSlot);

        if (_introVideoMaterial != null)
            Destroy(_introVideoMaterial);

        if (_introVideoMesh != null)
            Destroy(_introVideoMesh);
    }

    private void Update()
    {
        if (isWalking && !played)
        {
            _timer += Time.deltaTime;

            if (_timer >= 5f)
            {
                playerAnimator.SetTrigger("TransformToGhost");
                played = true;
            }
        }
    }

    public void ShowBlackScreen()
    {
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
        SetHumanForm();
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

        particles.transform.position = cameraTransform.TransformPoint(localOffset);
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

        SetIntroVideoAlpha(clampedAlpha);
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

    private void PrepareIntroFormVideos()
    {
        if (!useIntroFormVideos || playerTransform == null ||
            (introSoulWalkClip == null && introHumanWalkClip == null))
            return;

        BuildIntroVideoSurface();

        if (_introSoulVideoSlot == null)
            _introSoulVideoSlot = CreateIntroVideoSlot(introSoulWalkClip, "Soul");

        if (_introHumanVideoSlot == null)
            _introHumanVideoSlot = CreateIntroVideoSlot(introHumanWalkClip, "Human");

        PrepareIntroVideoSlot(_introSoulVideoSlot);
        PrepareIntroVideoSlot(_introHumanVideoSlot);
    }

    private void BuildIntroVideoSurface()
    {
        if (_introVideoRenderer != null)
            return;

        GameObject surface = new GameObject(introVideoSurfaceName);
        surface.layer = playerTransform.gameObject.layer;
        surface.transform.SetParent(playerTransform, false);
        surface.transform.localPosition = introVideoLocalOffset;
        surface.transform.localRotation = Quaternion.identity;
        surface.transform.localScale = Vector3.one;

        MeshFilter meshFilter = surface.AddComponent<MeshFilter>();
        _introVideoRenderer = surface.AddComponent<MeshRenderer>();
        _introVideoRenderer.enabled = false;
        _introVideoRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _introVideoRenderer.receiveShadows = false;
        _introVideoRenderer.lightProbeUsage = LightProbeUsage.Off;
        _introVideoRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

        if (playerSpriteRenderer != null)
        {
            _introVideoRenderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            _introVideoRenderer.sortingOrder = playerSpriteRenderer.sortingOrder;
            _introVideoRenderer.renderingLayerMask = playerSpriteRenderer.renderingLayerMask;
        }

        _introVideoMesh = CreateIntroVideoMesh();
        meshFilter.sharedMesh = _introVideoMesh;

        _introVideoMaterial = CreateIntroVideoMaterial();
        _introVideoRenderer.sharedMaterial = _introVideoMaterial;
    }

    private Mesh CreateIntroVideoMesh()
    {
        Mesh mesh = new Mesh { name = $"{name}_IntroVideoSurfaceMesh" };
        ApplyIntroVideoMeshSize(mesh);
        ApplyIntroVideoCrop(mesh, null);
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        return mesh;
    }

    private void UpdateIntroVideoCropForActiveSlot()
    {
        if (_introVideoMesh == null)
            return;

        ApplyIntroVideoCrop(_introVideoMesh, _activeIntroVideoSlot);
    }

    private void ApplyIntroVideoMeshSize(Mesh mesh)
    {
        if (mesh == null)
            return;

        Bounds bounds = GetIntroVideoReferenceBounds();
        Vector2 finalMultiplier = new Vector2(
            Mathf.Max(0.01f, introVideoSizeMultiplier.x),
            Mathf.Max(0.01f, introVideoSizeMultiplier.y)
        );

        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        extents.x *= finalMultiplier.x;
        extents.y *= finalMultiplier.y;

        Vector3 min = center - extents;
        Vector3 max = center + extents;

        mesh.vertices = new[]
        {
            new Vector3(min.x, min.y, 0f),
            new Vector3(min.x, max.y, 0f),
            new Vector3(max.x, max.y, 0f),
            new Vector3(max.x, min.y, 0f)
        };
        mesh.RecalculateBounds();
    }

    private void ApplyIntroVideoCrop(Mesh mesh, IntroVideoSlot slot)
    {
        if (mesh == null)
            return;

        Vector4 crop = GetIntroVideoUvCrop(slot);
        float left = Mathf.Clamp(crop.x, 0f, 0.49f);
        float right = Mathf.Clamp(crop.y, 0f, 0.49f);
        float bottom = Mathf.Clamp(crop.z, 0f, 0.49f);
        float top = Mathf.Clamp(crop.w, 0f, 0.49f);

        ClampCropPair(ref left, ref right);
        ClampCropPair(ref bottom, ref top);

        float uMin = left;
        float uMax = 1f - right;
        float vMin = bottom;
        float vMax = 1f - top;

        mesh.uv = new[]
        {
            new Vector2(uMin, vMin),
            new Vector2(uMin, vMax),
            new Vector2(uMax, vMax),
            new Vector2(uMax, vMin)
        };
    }

    private Vector4 GetIntroVideoUvCrop(IntroVideoSlot slot)
    {
        if (slot == _introHumanVideoSlot)
            return introHumanVideoUvCrop;

        return Vector4.zero;
    }

    private void ClampCropPair(ref float first, ref float second)
    {
        float total = first + second;
        if (total < 0.98f)
            return;

        float factor = 0.98f / total;
        first *= factor;
        second *= factor;
    }

    private Bounds GetIntroVideoReferenceBounds()
    {
        if (playerSpriteRenderer != null)
        {
            if (playerSpriteRenderer.drawMode == SpriteDrawMode.Simple && playerSpriteRenderer.sprite != null)
                return playerSpriteRenderer.sprite.bounds;

            return new Bounds(Vector3.zero, new Vector3(playerSpriteRenderer.size.x, playerSpriteRenderer.size.y, 0f));
        }

        return new Bounds(Vector3.zero, new Vector3(GetIntroVideoAspectRatio(), 1f, 0f));
    }

    private IntroVideoSlot CreateIntroVideoSlot(VideoClip clip, string slotName)
    {
        if (clip == null)
            return null;

        IntroVideoSlot slot = new IntroVideoSlot
        {
            clip = clip,
            texture = CreateIntroVideoRenderTexture(clip, slotName)
        };

        slot.player = gameObject.AddComponent<VideoPlayer>();
        slot.player.playOnAwake = false;
        slot.player.isLooping = true;
        slot.player.skipOnDrop = true;
        slot.player.waitForFirstFrame = true;
        slot.player.source = VideoSource.VideoClip;
        slot.player.clip = clip;
        slot.player.renderMode = VideoRenderMode.RenderTexture;
        slot.player.targetTexture = slot.texture;
        slot.player.audioOutputMode = VideoAudioOutputMode.None;
        slot.player.prepareCompleted += HandleIntroVideoPrepared;
        slot.player.errorReceived += HandleIntroVideoError;

        return slot;
    }

    private RenderTexture CreateIntroVideoRenderTexture(VideoClip clip, string slotName)
    {
        int width = introVideoRenderTextureWidth > 0 ? introVideoRenderTextureWidth : GetIntroClipWidthSafe(clip);
        int height = introVideoRenderTextureHeight > 0 ? introVideoRenderTextureHeight : GetIntroClipHeightSafe(clip);

        RenderTexture texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
        {
            name = $"{name}_{slotName}_{clip.name}_RT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false
        };
        texture.Create();
        return texture;
    }

    private Material CreateIntroVideoMaterial()
    {
        Material material = new Material(ResolveIntroVideoSurfaceShader())
        {
            name = $"{name}_IntroVideo_MAT"
        };

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        return material;
    }

    private Shader ResolveIntroVideoSurfaceShader()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader != null) return shader;

        shader = Shader.Find("Unlit/Transparent");
        if (shader != null) return shader;

        return Shader.Find("Sprites/Default");
    }

    private void PrepareIntroVideoSlot(IntroVideoSlot slot)
    {
        if (slot?.player == null)
            return;

        if (slot.player.isPrepared)
        {
            slot.ready = true;
            return;
        }

        slot.player.Prepare();
    }

    private bool ShowIntroFormVideo(IntroVideoSlot slot)
    {
        if (!useIntroFormVideos || slot?.player == null || _introVideoRenderer == null)
            return false;

        if (!slot.ready && !slot.player.isPrepared)
        {
            PrepareIntroVideoSlot(slot);
            if (keepAnimatorUntilIntroVideoPrepared)
            {
                HideIntroFormVideo(restorePlayerRenderers: true);
                return false;
            }
        }

        slot.ready = slot.ready || slot.player.isPrepared;

        if (_activeIntroVideoSlot != slot)
        {
            PauseIntroVideoSlot(_activeIntroVideoSlot);
            SetIntroVideoMaterialTexture(slot.texture);
            _activeIntroVideoSlot = slot;
        }

        UpdateIntroVideoCropForActiveSlot();
        SetPlayerRenderersEnabled(false);
        _introVideoRenderer.enabled = true;
        SetIntroVideoAlpha(_activePlayerFadeAlpha >= 0f ? _activePlayerFadeAlpha : 1f);

        if (!slot.player.isPlaying)
            slot.player.Play();

        return true;
    }

    private void HideIntroFormVideo(bool restorePlayerRenderers)
    {
        PauseIntroVideoSlot(_activeIntroVideoSlot);
        _activeIntroVideoSlot = null;

        if (_introVideoRenderer != null)
            _introVideoRenderer.enabled = false;

        if (restorePlayerRenderers)
            RestorePlayerRenderersEnabled();
    }

    private void SetIntroVideoMaterialTexture(Texture texture)
    {
        if (_introVideoMaterial == null || texture == null)
            return;

        SetIntroMaterialTexture("_BaseMap", texture);
        SetIntroMaterialTexture("_MainTex", texture);
    }

    private void SetIntroMaterialTexture(string propertyName, Texture texture)
    {
        if (_introVideoMaterial == null || texture == null || !_introVideoMaterial.HasProperty(propertyName))
            return;

        _introVideoMaterial.SetTexture(propertyName, texture);
    }

    private void SetIntroVideoAlpha(float alpha)
    {
        if (_introVideoMaterial == null)
            return;

        Color color = Color.white;
        if (_introVideoMaterial.HasProperty("_BaseColor"))
            color = _introVideoMaterial.GetColor("_BaseColor");
        else if (_introVideoMaterial.HasProperty("_Color"))
            color = _introVideoMaterial.GetColor("_Color");

        color.a = Mathf.Clamp01(alpha);

        if (_introVideoMaterial.HasProperty("_BaseColor")) _introVideoMaterial.SetColor("_BaseColor", color);
        if (_introVideoMaterial.HasProperty("_Color")) _introVideoMaterial.SetColor("_Color", color);
    }

    private void PauseIntroVideoSlot(IntroVideoSlot slot)
    {
        if (slot?.player != null && slot.player.isPlaying)
            slot.player.Pause();
    }

    private void ReleaseIntroVideoSlot(IntroVideoSlot slot)
    {
        if (slot == null)
            return;

        if (slot.player != null)
        {
            slot.player.prepareCompleted -= HandleIntroVideoPrepared;
            slot.player.errorReceived -= HandleIntroVideoError;
            slot.player.targetTexture = null;
        }

        if (slot.texture != null)
        {
            slot.texture.Release();
            Destroy(slot.texture);
        }
    }

    private void HandleIntroVideoPrepared(VideoPlayer source)
    {
        IntroVideoSlot slot = ResolveIntroVideoSlot(source);
        if (slot != null)
            slot.ready = true;
    }

    private void HandleIntroVideoError(VideoPlayer source, string message)
    {
        IntroVideoSlot slot = ResolveIntroVideoSlot(source);
        string clipName = slot?.clip != null ? slot.clip.name : source.name;
        Debug.LogWarning($"[{nameof(IntroSequenceManager)}] Error reproduciendo video de intro '{clipName}': {message}", this);
    }

    private IntroVideoSlot ResolveIntroVideoSlot(VideoPlayer source)
    {
        if (_introSoulVideoSlot != null && _introSoulVideoSlot.player == source)
            return _introSoulVideoSlot;

        if (_introHumanVideoSlot != null && _introHumanVideoSlot.player == source)
            return _introHumanVideoSlot;

        return null;
    }

    private int GetIntroClipWidthSafe(VideoClip clip)
        => clip != null && clip.width > 0 ? (int)clip.width : 512;

    private int GetIntroClipHeightSafe(VideoClip clip)
        => clip != null && clip.height > 0 ? (int)clip.height : 512;

    private float GetIntroVideoAspectRatio()
    {
        VideoClip clip = introHumanWalkClip != null ? introHumanWalkClip : introSoulWalkClip;
        int height = GetIntroClipHeightSafe(clip);
        return height > 0 ? GetIntroClipWidthSafe(clip) / (float)height : 1f;
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
        playerAnimator.SetBool("IdleFrontHuman", true);
        playerAnimator.SetBool("IdleFront", false);
        ShowIntroFormVideo(_introHumanVideoSlot);
    }

    private void SetSoulForm()
    {
        if (playerAnimator == null || !playerAnimator.enabled) return;
        playerAnimator.SetBool("IdleFront", true);
        playerAnimator.SetBool("IdleFrontHuman", false);
        ShowIntroFormVideo(_introSoulVideoSlot);
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
        HideIntroFormVideo(restorePlayerRenderers: true);

        if (playerAnimator != null && _originalAnimatorController != null)
            playerAnimator.runtimeAnimatorController = _originalAnimatorController;

        _activePlayerFadeAlpha = -1f;
        _isIntroBlizzardCameraAnchored = false;
        RestoreIntroCameraFraming();
        RestoreOriginalColors();
        RestoreOriginalMaterials();
    }

    public void OnPlayerEnterChurch(Transform churchPosition)
    {
        StartCoroutine(EnterChurchSequence(churchPosition));
    }

    private IEnumerator EnterChurchSequence(Transform target)
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }

        RestoreOriginalAnimator();

        if (playerAnimator != null) playerAnimator.enabled = true;
        SetSoulForm();

        playerInputHandler.enabled = false;
        boundaryClamp.enabled = true;
        yield return StartCoroutine(MovePlayerToPosition(target.position, 2f));

        playerAnimator.SetBool("IdleFront", false);
        playerInputHandler.enabled = true;

        hudManager?.OnClickCombatPosition();
    }

    private IEnumerator MovePlayerToPosition(Vector3 target, float duration)
    {
        Vector3 start = playerTransform.position;
        float elapsed = 0f;

        playerCC.enabled = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerTransform.position = Vector3.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        playerTransform.position = target;
        playerCC.enabled = true;
    }
}
