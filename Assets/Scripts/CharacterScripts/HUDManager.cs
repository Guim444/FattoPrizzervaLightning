using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    private static readonly int IdleFrontHash = Animator.StringToHash("IdleFront");

    [SerializeField] private GameObject enemy;
    [SerializeField] private GameObject enemyRio;
    [SerializeField] private PlayerBoundaryClamp playerBoundary;
    [SerializeField] GameObject _canvasMain;
    [SerializeField] GameObject _canvasUIStats;

    [Header("UI")] [SerializeField] private GameObject uiRoot;
    [SerializeField] private GameObject selectionPanel;

    [Header("References")] [SerializeField]
    private GameObject OutsideChurch;

    [SerializeField] private Transform playerTransform;

    [SerializeField] private PlayableAreaManager playableAreaManager;
    [SerializeField] private IntroSequenceManager introSequenceManager;

    [SerializeField] private TutorialRingManager ringManager;

    // [SerializeField] GameObject outSide;
    [SerializeField] private GameObject playerTransformFront;
    [SerializeField] private Animator playerAnimator;

    [Header("Configuration — Player")] [SerializeField]
    private float startPositionX = 0f;

    private float distanceToStart = 100f; //Esta variable cuando se elimine la seccion de test se debería eliminar

    [Header("Configuration — Test Mode")] [SerializeField]
    private float testPositionX = 0f;

    [SerializeField] private float testPositionZ = -2f;

// Will be removed
    private float startPositionY = 1.8f;
    private float combatStartPositionX = 0f;
    private float combatStartPositionY = 1f;
    private float combatStartPositionZ = -2f;

    [Header("Configuration — Camera")] [SerializeField]
    private float freeMoveStartCameraY = 1.5f;

    [SerializeField] private CinemachineZoomController cinemachineZoomController;
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField] private CameraMovement cameraMovement;

    [Header("Startup Loading")] [SerializeField]
    private bool requireStartupLoading = true;

    [SerializeField, Min(0f)] private float startupLoadingMinimumSeconds = 2f;
    [SerializeField, Min(0f)] private float particlePrewarmSeconds = 0.35f;
    [SerializeField, Min(0f)] private float alembicPrewarmSampleTime = 0.05f;
    [SerializeField] private Vector2 loadingBarSize = new Vector2(420f, 18f);
    [SerializeField] private Color loadingFillColor = new Color(0.9f, 0.18f, 0.1f, 1f);
    [SerializeField] private bool prewarmLightmaps = true;
    [SerializeField] private bool prewarmBlizzardVideos = true;
    [SerializeField] private bool prewarmAlembicTrees = true;
    [SerializeField] private bool prewarmAnimators = true;
    [SerializeField] private bool prewarmParticles = true;
    [SerializeField] private bool prewarmShaders = false;

    private CanvasGroup _selectionCanvasGroup;
    private Selectable[] _selectionSelectables;
    private GameObject _loadingRoot;
    private Image _loadingFill;
    private Text _loadingText;
    private bool _startupLoadingComplete;
    private Coroutine _startupLoadingCoroutine;


    private void Awake()
    {
        ActivateUiForPlayMode();
        CacheSelectionControls();
        BuildStartupLoadingUi();
        Time.timeScale = 0f;

        if (requireStartupLoading)
            SetSelectionInteractable(false);
        else
            CompleteStartupLoading(hideLoadingUi: true);
    }

    private void Start()
    {
        if (requireStartupLoading && _startupLoadingCoroutine == null)
            _startupLoadingCoroutine = StartCoroutine(StartupLoadingCoroutine());
    }

    private void ActivateUiForPlayMode()
    {
        if (uiRoot != null) uiRoot.SetActive(true);
        if (_canvasMain != null) _canvasMain.SetActive(true);
    }

    private void ActivateCinemachine()
    {
        cameraMovement.enabled = false;
        if (cinemachineBrain != null) cinemachineBrain.enabled = true;
        cinemachineZoomController?.gameObject.SetActive(true);
    }

    private void ActivateCameraMovement()
    {
        cinemachineZoomController?.gameObject.SetActive(false);
        if (cinemachineBrain != null) cinemachineBrain.enabled = false;
        cameraMovement.enabled = true;
    }

    public void OnClickCombatPosition()
    {
        if (!CanUseModeSelection()) return;

        ActivateGameplayBlizzard();
        introSequenceManager?.RestoreOriginalAnimator();
        Time.timeScale = 1f;
        ActivateCinemachine();
        playerBoundary.enabled = false;
        enemy.SetActive(false);
        playerTransformFront.SetActive(false);
        enemyRio.SetActive(true);
        MovePlayerToX(combatStartPositionX);
        playableAreaManager.SetActive(true);
        ringManager?.RefreshStartPositions();
        ringManager.enabled = true;
        HidePanel();
    }

    public void OnEnterCombatFromTrigger()
    {
        OnClickCombatPosition();
    }

    public void OnClickStartHere()
    {
        if (!CanUseModeSelection()) return;

        _canvasUIStats.SetActive(false);
        playableAreaManager.SetActive(false);
        ActivateCinemachine();
        introSequenceManager?.ShowBlackScreen();
        Time.timeScale = 1f;
        playerBoundary.enabled = false;
        OutsideChurch.SetActive(false);
        enemy.SetActive(false);
        enemyRio.SetActive(false);
        playerTransformFront.SetActive(false);
        MoveStartPlayerToX(startPositionX - distanceToStart);
        ringManager.enabled = false;
        SetPlayerIdleFrontIfAvailable(true);
        HidePanel();
        introSequenceManager?.StartIntro();
        playerBoundary.enabled = true;
    }

    // Called from TutorialRingManager.onEnemyOut.
    // Deactivates the ring and switches to free mode keeping RioTutte active (next phase).
    public void OnRingPhaseComplete()
    {
        ringManager.enabled = false;
        playerBoundary.enabled = false;
        var rioTutte = enemyRio.GetComponent<RioTutteEnemy>();
        rioTutte.AdvancePhase(); // 0 → 1: activates DashGrab
        rioTutte.facePlayerByZ = true;
    }

    public void OnClickFreeMove()
    {
        if (!CanUseModeSelection()) return;

        ActivateGameplayBlizzard();
        introSequenceManager?.RestoreOriginalAnimator();
        Time.timeScale = 1f;
        cameraMovement.combatY = freeMoveStartCameraY;
        ActivateCameraMovement();
        playerTransformFront.SetActive(true);
        ringManager.enabled = false;
        playerBoundary.enabled = false;
        enemy.SetActive(true);
        OutsideChurch.SetActive(true);
        enemyRio.SetActive(false);
        MoveTestPlayerToPosition(testPositionX, testPositionZ);
        HidePanel();
    }

    private void ActivateGameplayBlizzard()
    {
        int activatedManagers = WindStateManager.ActivateAllVideoPlayersRoots();
        if (activatedManagers == 0)
            Debug.LogWarning("[HUDManager] No se encontró ningún WindStateManager para activar la ventisca.", this);
    }

    private IEnumerator StartupLoadingCoroutine()
    {
        SetSelectionInteractable(false);
        SetLoadingVisible(true);
        SetLoadingProgress(0f, "Preparando escena");

        float loadingStartedAt = Time.realtimeSinceStartup;
        yield return null;

        if (prewarmLightmaps)
            yield return StartCoroutine(PrewarmLightmapStates());
        else
            SetLoadingProgress(0.25f, "Bakes omitidos");

        if (prewarmBlizzardVideos)
        {
            WindStateManager.PrewarmAllVideoPlayersHidden();
            SetLoadingProgress(0.45f, "Preparando ventisca");
            yield return null;
        }

        if (prewarmAlembicTrees)
            yield return StartCoroutine(PrewarmAlembicTreePlayers());

        if (prewarmAnimators)
            yield return StartCoroutine(PrewarmSceneAnimators());

        if (prewarmParticles)
            yield return StartCoroutine(PrewarmSceneParticles());

        if (prewarmShaders)
        {
            Debug.LogWarning(
                "[HUDManager] Shader warmup global desactivado: Shader.WarmupAllShaders puede generar asserts de keyword space con shaders de partículas URP. Usar ShaderVariantCollection si hace falta un warmup fino.",
                this);
            SetLoadingProgress(0.9f, "Shaders omitidos");
            yield return null;
        }

        while (Time.realtimeSinceStartup - loadingStartedAt < startupLoadingMinimumSeconds)
        {
            float elapsed = Time.realtimeSinceStartup - loadingStartedAt;
            float t = startupLoadingMinimumSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / startupLoadingMinimumSeconds);
            SetLoadingProgress(Mathf.Lerp(0.92f, 0.99f, t), "Estabilizando primer frame");
            yield return null;
        }

        SetLoadingProgress(1f, "Listo");
        yield return new WaitForSecondsRealtime(0.2f);
        CompleteStartupLoading(hideLoadingUi: true);
        _startupLoadingCoroutine = null;
    }

    private IEnumerator PrewarmLightmapStates()
    {
        LightmapStateManager[] managers = Object.FindObjectsByType<LightmapStateManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (managers.Length == 0)
        {
            SetLoadingProgress(0.25f, "Sin bakes runtime");
            yield return null;
            yield break;
        }

        foreach (LightmapStateManager manager in managers)
            manager.ApplyWarm1();
        SetLoadingProgress(0.12f, "Bake warm 1");
        yield return null;

        foreach (LightmapStateManager manager in managers)
            manager.ApplyWarm2();
        SetLoadingProgress(0.2f, "Bake warm 2");
        yield return null;

        foreach (LightmapStateManager manager in managers)
            manager.ApplyBlue();
        SetLoadingProgress(0.25f, "Bake azul");
        yield return null;

        foreach (LightmapStateManager manager in managers)
            manager.ApplyWarm1();
    }

    private IEnumerator PrewarmAlembicTreePlayers()
    {
        AlembicTreeWindController[] trees = Object.FindObjectsByType<AlembicTreeWindController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int processed = 0;
        foreach (AlembicTreeWindController tree in trees)
        {
            if (tree == null || !tree.HasAvailablePlayers) continue;

            tree.PrewarmPlayers(alembicPrewarmSampleTime);
            processed++;

            if (processed % 12 == 0)
            {
                SetLoadingProgress(Mathf.Lerp(0.45f, 0.62f, processed / Mathf.Max(1f, trees.Length)),
                    "Preparando árboles");
                yield return null;
            }
        }

        SetLoadingProgress(0.62f, "Árboles preparados");
        yield return null;
    }

    private IEnumerator PrewarmSceneAnimators()
    {
        Animator[] animators = Object.FindObjectsByType<Animator>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int processed = 0;
        foreach (Animator animator in animators)
        {
            if (animator == null || !animator.enabled || !animator.gameObject.activeInHierarchy)
                continue;

            animator.Update(0f);
            processed++;

            if (processed % 16 == 0)
            {
                SetLoadingProgress(Mathf.Lerp(0.62f, 0.72f, processed / Mathf.Max(1f, animators.Length)),
                    "Preparando animaciones");
                yield return null;
            }
        }

        SetLoadingProgress(0.72f, "Animaciones preparadas");
        yield return null;
    }

    private IEnumerator PrewarmSceneParticles()
    {
        ParticleSystem[] particles = Object.FindObjectsByType<ParticleSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int processed = 0;
        foreach (ParticleSystem particle in particles)
        {
            if (particle == null || !particle.gameObject.activeInHierarchy)
                continue;

            bool wasPlaying = particle.isPlaying;
            if (particlePrewarmSeconds > 0f)
                particle.Simulate(particlePrewarmSeconds, true, true, false);

            if (wasPlaying)
                particle.Play(true);
            else
                particle.Clear(true);

            processed++;

            if (processed % 12 == 0)
            {
                SetLoadingProgress(Mathf.Lerp(0.72f, 0.84f, processed / Mathf.Max(1f, particles.Length)),
                    "Preparando partículas");
                yield return null;
            }
        }

        SetLoadingProgress(0.84f, "Partículas preparadas");
        yield return null;
    }

    private bool CanUseModeSelection()
    {
        if (_startupLoadingComplete)
            return true;

        Debug.Log("[HUDManager] La selección de modo está bloqueada hasta completar la carga inicial.", this);
        return false;
    }

    private void CompleteStartupLoading(bool hideLoadingUi)
    {
        _startupLoadingComplete = true;
        SetSelectionInteractable(true);
        SetLoadingProgress(1f, "Listo");

        if (hideLoadingUi)
            SetLoadingVisible(false);
    }

    private void CacheSelectionControls()
    {
        GameObject controlsRoot = selectionPanel != null ? selectionPanel : _canvasMain;
        if (controlsRoot == null) return;

        _selectionCanvasGroup = controlsRoot.GetComponent<CanvasGroup>();
        if (_selectionCanvasGroup == null)
            _selectionCanvasGroup = controlsRoot.AddComponent<CanvasGroup>();

        _selectionSelectables = controlsRoot.GetComponentsInChildren<Selectable>(true);
    }

    private void SetSelectionInteractable(bool interactable)
    {
        if (_selectionCanvasGroup != null)
        {
            _selectionCanvasGroup.interactable = interactable;
            _selectionCanvasGroup.blocksRaycasts = true;
        }

        if (_selectionSelectables == null) return;

        foreach (Selectable selectable in _selectionSelectables)
        {
            if (selectable != null)
                selectable.interactable = interactable;
        }
    }

    private void BuildStartupLoadingUi()
    {
        if (_loadingRoot != null)
            return;

        Transform loadingParent = uiRoot != null
            ? uiRoot.transform
            : (_canvasMain != null ? _canvasMain.transform : transform);

        _loadingRoot = new GameObject(
            "StartupLoadingCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        _loadingRoot.transform.SetParent(loadingParent, false);
        _loadingRoot.transform.localPosition = Vector3.zero;
        _loadingRoot.transform.localRotation = Quaternion.identity;
        _loadingRoot.transform.localScale = Vector3.one;

        Canvas loadingCanvas = _loadingRoot.GetComponent<Canvas>();
        loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        loadingCanvas.sortingOrder = 5000;

        CanvasScaler canvasScaler = _loadingRoot.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        RectTransform rootRect = _loadingRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject blockerObject = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
        blockerObject.transform.SetParent(_loadingRoot.transform, false);
        RectTransform blockerRect = blockerObject.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.offsetMin = Vector2.zero;
        blockerRect.offsetMax = Vector2.zero;

        Image blocker = blockerObject.GetComponent<Image>();
        blocker.color = new Color(0.05f, 0.05f, 0.05f, 0.45f);
        blocker.raycastTarget = true;

        GameObject label = new GameObject("Label");
        label.transform.SetParent(_loadingRoot.transform, false);
        _loadingText = label.AddComponent<Text>();
        _loadingText.alignment = TextAnchor.MiddleCenter;
        _loadingText.color = new Color(0.82f, 0.82f, 0.82f, 1f);
        _loadingText.font = ResolveBuiltinFont();
        _loadingText.fontSize = 18;
        _loadingText.text = "Preparando escena";

        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(loadingBarSize.x, 28f);
        labelRect.anchoredPosition = new Vector2(0f, -148f);

        GameObject bar = new GameObject("Bar");
        bar.transform.SetParent(_loadingRoot.transform, false);
        Image barImage = bar.AddComponent<Image>();
        barImage.color = new Color(0.11f, 0.11f, 0.11f, 0.95f);

        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0.5f, 0.5f);
        barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.sizeDelta = loadingBarSize;
        barRect.anchoredPosition = new Vector2(0f, -178f);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(bar.transform, false);
        _loadingFill = fill.AddComponent<Image>();
        _loadingFill.color = loadingFillColor;

        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        SetLoadingProgress(0f, "Preparando escena");
    }

    private Font ResolveBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private void SetLoadingVisible(bool visible)
    {
        if (_loadingRoot != null)
        {
            _loadingRoot.SetActive(visible);
            if (visible)
                _loadingRoot.transform.SetAsLastSibling();
        }
    }

    private void SetLoadingProgress(float progress, string label)
    {
        float clamped = Mathf.Clamp01(progress);

        if (_loadingFill != null)
        {
            RectTransform fillRect = _loadingFill.rectTransform;
            fillRect.anchorMax = new Vector2(clamped, 1f);
            fillRect.offsetMax = Vector2.zero;
        }

        if (_loadingText != null)
            _loadingText.text = $"{label}  {Mathf.RoundToInt(clamped * 100f)}%";
    }

    private void MovePlayerToX(float x)
    {
        if (playerTransform == null)
        {
            Debug.LogError("[HUDManager] playerTransform is not assigned in the Inspector.");
            return;
        }

        Debug.Log(
            $"[HUDManager] MovePlayerToX — before: {playerTransform.position}  →  target X:{x} Y:{combatStartPositionY} Z:{combatStartPositionZ}");

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerTransform.position = new Vector3(x, combatStartPositionY, combatStartPositionZ);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        Debug.Log($"[HUDManager] MovePlayerToX — after: {playerTransform.position}");
    }

    private void MoveStartPlayerToX(float x)
    {
        if (playerTransform == null)
        {
            Debug.LogError("[HUDManager] playerTransform is not assigned in the Inspector.");
            return;
        }

        float startZ = introSequenceManager != null ? introSequenceManager.IntroPlayerZ : 0f;

        Debug.Log(
            $"[HUDManager] MoveStartPlayerToX — before: {playerTransform.position}  →  target X:{x} Y:{startPositionY} Z:{startZ}");

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerTransform.position = new Vector3(x, startPositionY, startZ);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        Debug.Log($"[HUDManager] MoveStartPlayerToX — after: {playerTransform.position}");
    }

    private void MoveTestPlayerToPosition(float x, float z)
    {
        if (playerTransform == null)
        {
            Debug.LogError("[HUDManager] playerTransform is not assigned in the Inspector.");
            return;
        }

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerTransform.position = new Vector3(x, combatStartPositionY, z);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;
    }

    private void HidePanel()
    {
        _canvasMain.SetActive(false);
    }

    private void SetPlayerIdleFrontIfAvailable(bool value)
    {
        if (playerAnimator == null || playerAnimator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter in playerAnimator.parameters)
        {
            if (parameter.nameHash != IdleFrontHash ||
                parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            playerAnimator.SetBool(IdleFrontHash, value);
            return;
        }
    }
}