using Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public sealed class DialogueCameraController : CinemachineExtension
{
    [Header("Dialogue Shot")]
    [Tooltip("Altura mundial fija de la cámara durante el diálogo.")]
    [SerializeField] private float worldHeight = 2.5f;
    [SerializeField, Range(1f, 179f)] private float fieldOfView = 15f;
    [SerializeField] private Vector3 worldRotation = new Vector3(-6f, 89.006f, -0.108f);
    [SerializeField] private int cameraPriority = 100;

    [Header("Group Framing")]
    [Tooltip("Porcentaje de pantalla ocupado por el jugador y RioTutte.")]
    [SerializeField, Range(0.1f, 1f)] private float framingSize = 0.95f;
    [SerializeField, Min(0f)] private float targetRadius = 0.5f;
    [SerializeField, Min(0.01f)] private float initialDistance = 6f;
    [SerializeField, Min(0.01f)] private float minimumDistance = 1f;
    [SerializeField, Min(0.01f)] private float maximumDistance = 30f;
    [Tooltip("Valor positivo acerca la cámara después del encuadre automático.")]
    [SerializeField] private float distanceOffset;
    [SerializeField, Min(0f)] private float positionDamping = 0.15f;

    [Header("Transition")]
    [SerializeField, Min(0f)] private float blendDuration = 2f;

    private CinemachineVirtualCamera _virtualCamera;
    private CinemachineFramingTransposer _framingTransposer;
    private CinemachineTargetGroup _targetGroup;
    private Transform _playerTarget;
    private Transform _rioTutteTarget;

    public void Configure(
        float configuredWorldHeight,
        float configuredFieldOfView,
        Vector3 configuredWorldRotation,
        int configuredPriority,
        float configuredFramingSize,
        float configuredTargetRadius,
        float configuredInitialDistance,
        float configuredMinimumDistance,
        float configuredMaximumDistance,
        float configuredDistanceOffset,
        float configuredPositionDamping,
        float configuredBlendDuration)
    {
        worldHeight = configuredWorldHeight;
        fieldOfView = Mathf.Clamp(configuredFieldOfView, 1f, 179f);
        worldRotation = configuredWorldRotation;
        cameraPriority = configuredPriority;
        framingSize = Mathf.Clamp(configuredFramingSize, 0.1f, 1f);
        targetRadius = Mathf.Max(0f, configuredTargetRadius);
        initialDistance = Mathf.Max(0.01f, configuredInitialDistance);
        minimumDistance = Mathf.Max(0.01f, configuredMinimumDistance);
        maximumDistance = Mathf.Max(minimumDistance, configuredMaximumDistance);
        distanceOffset = configuredDistanceOffset;
        positionDamping = Mathf.Max(0f, configuredPositionDamping);
        blendDuration = Mathf.Max(0f, configuredBlendDuration);

        if (gameObject.activeInHierarchy)
            CacheAndConfigureCamera();
    }

    protected override void Awake()
    {
        base.Awake();
        CacheAndConfigureCamera();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        CacheAndConfigureCamera();
    }

    public bool ActivateCamera(Transform playerTarget, Transform rioTutteTarget)
    {
        if (playerTarget == null || rioTutteTarget == null)
        {
            Debug.LogError(
                $"[{nameof(DialogueCameraController)}] El jugador y RioTutte deben estar asignados.",
                this);
            return false;
        }

        _playerTarget = playerTarget;
        _rioTutteTarget = rioTutteTarget;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        CacheAndConfigureCamera();
        ConfigureTargetGroup();
        PlaceCameraAtInitialPosition();
        _virtualCamera.PreviousStateIsValid = false;
        return true;
    }

    public void DeactivateCamera()
    {
        gameObject.SetActive(false);
    }

    public override void PrePipelineMutateCameraStateCallback(
        CinemachineVirtualCameraBase vcam,
        ref CameraState state,
        float deltaTime)
    {
        ApplyShotSettings(ref state);
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage,
        ref CameraState state,
        float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize)
            return;

        ApplyShotSettings(ref state);

        Vector3 cameraPosition = state.RawPosition;
        cameraPosition += state.RawOrientation * Vector3.forward * distanceOffset;
        cameraPosition.y = worldHeight;
        state.RawPosition = cameraPosition;
    }

    private void OnValidate()
    {
        fieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
        framingSize = Mathf.Clamp(framingSize, 0.1f, 1f);
        targetRadius = Mathf.Max(0f, targetRadius);
        initialDistance = Mathf.Max(0.01f, initialDistance);
        minimumDistance = Mathf.Max(0.01f, minimumDistance);
        maximumDistance = Mathf.Max(minimumDistance, maximumDistance);
        positionDamping = Mathf.Max(0f, positionDamping);
        blendDuration = Mathf.Max(0f, blendDuration);

        if (Application.isPlaying && gameObject.activeInHierarchy)
            CacheAndConfigureCamera();
    }

    protected override void OnDestroy()
    {
        if (_targetGroup != null)
        {
            if (Application.isPlaying)
                Destroy(_targetGroup.gameObject);
            else
                DestroyImmediate(_targetGroup.gameObject);
        }

        base.OnDestroy();
    }

    private void CacheAndConfigureCamera()
    {
        if (_virtualCamera == null)
            _virtualCamera = GetComponent<CinemachineVirtualCamera>();

        if (_virtualCamera == null)
            return;

        _virtualCamera.Priority = cameraPriority;
        _virtualCamera.m_Lens.FieldOfView = fieldOfView;
        transform.rotation = Quaternion.Euler(worldRotation);

        _framingTransposer =
            _virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();

        if (_framingTransposer == null)
            _framingTransposer =
                _virtualCamera.AddCinemachineComponent<CinemachineFramingTransposer>();

        _framingTransposer.m_GroupFramingMode =
            CinemachineFramingTransposer.FramingMode.HorizontalAndVertical;
        _framingTransposer.m_AdjustmentMode =
            CinemachineFramingTransposer.AdjustmentMode.DollyOnly;
        _framingTransposer.m_GroupFramingSize = framingSize;
        _framingTransposer.m_CameraDistance = initialDistance;
        _framingTransposer.m_MinimumDistance = minimumDistance;
        _framingTransposer.m_MaximumDistance = Mathf.Max(minimumDistance, maximumDistance);
        _framingTransposer.m_XDamping = positionDamping;
        _framingTransposer.m_YDamping = positionDamping;
        _framingTransposer.m_ZDamping = positionDamping;
        _framingTransposer.m_TrackedObjectOffset = Vector3.zero;
    }

    private void ConfigureTargetGroup()
    {
        if (_targetGroup == null)
        {
            GameObject groupObject = new GameObject("DialogueCameraTargetGroup");
            groupObject.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(groupObject, gameObject.scene);
            _targetGroup = groupObject.AddComponent<CinemachineTargetGroup>();
        }

        _targetGroup.m_PositionMode = CinemachineTargetGroup.PositionMode.GroupCenter;
        _targetGroup.m_RotationMode = CinemachineTargetGroup.RotationMode.Manual;
        _targetGroup.m_UpdateMethod = CinemachineTargetGroup.UpdateMethod.LateUpdate;
        _targetGroup.m_Targets = new[]
        {
            new CinemachineTargetGroup.Target
            {
                target = _playerTarget,
                weight = 1f,
                radius = targetRadius
            },
            new CinemachineTargetGroup.Target
            {
                target = _rioTutteTarget,
                weight = 1f,
                radius = targetRadius
            }
        };

        _targetGroup.DoUpdate();
        _virtualCamera.Follow = _targetGroup.transform;
        _virtualCamera.LookAt = null;
    }

    private void PlaceCameraAtInitialPosition()
    {
        Quaternion rotation = Quaternion.Euler(worldRotation);
        Vector3 groupCenter = _targetGroup.transform.position;
        Vector3 cameraPosition =
            groupCenter - rotation * Vector3.forward * initialDistance;
        cameraPosition.y = worldHeight;

        transform.SetPositionAndRotation(cameraPosition, rotation);
    }

    private void ApplyShotSettings(ref CameraState state)
    {
        state.RawOrientation = Quaternion.Euler(worldRotation);

        LensSettings lens = state.Lens;
        lens.FieldOfView = fieldOfView;
        state.Lens = lens;
    }
}
