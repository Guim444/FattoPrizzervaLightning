using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Initial Position")]
    [Tooltip("X offset relative to player on activation (e.g.: -11 sets camera to X = player.x - 11)")]
    [SerializeField] private float startXOffset = -11f;

    [Header("Smoothness")]
    public float smoothTime  = 0.3f;
    public float ySmoothTime = 0.4f;

    [Header("Zoom X")]
    public float zoomDistance;
    [SerializeField] private float zoomInThreshold  =  0.5f;
    [SerializeField] private float zoomOutThreshold = -0.5f;

    [Header("Z Zones")]
    public float zTransitionMin  = 6f;
    public float zTransitionMax  = 10f;
    public float zTargetInside   = 4f;
    public float zTargetOutside  = 14f;

    [Header("Y Position")]
    public float combatY      = 1.5f;
    public bool  followPlayerY = false;
    public float yOffset      = 1.5f;

    [Header("Camera")]
    [SerializeField] private float farClipPlane = 40f;
    [SerializeField] private float rotationX = 6.234f;
    [SerializeField] private float rotationY = 89.006f;
    [SerializeField] private float rotationZ = 359.892f;

    public bool zoomed;

    private float _baseX;       // reference X from which the zoom operates
    private float xZoomTarget;
    private float xVel;
    private float zVel;
    private float _yVel;
    private Camera _cam;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam != null)
            _cam.farClipPlane = farClipPlane;
    }

    private void OnEnable()
    {
        if (player == null) return;

        // Immediate snap to position relative to player
        float targetX = player.position.x + startXOffset;
        float targetY = followPlayerY ? player.position.y + yOffset : combatY;
        float targetZ = GetTargetZ();

        transform.position = new Vector3(targetX, targetY, targetZ);
        transform.rotation = Quaternion.Euler(rotationX, rotationY, rotationZ);

        _baseX      = targetX;
        xZoomTarget = targetX;
        xVel  = 0f;
        zVel  = 0f;
        _yVel = 0f;
        zoomed = false;
    }

    // ── Update ───────────────────────────────────────────────────────────────

    private void Update()
    {
        float targetY = followPlayerY ? player.position.y + yOffset : combatY;

        float st  = Mathf.Max(smoothTime,  0.01f);
        float sty = Mathf.Max(ySmoothTime, 0.01f);

        float newX = Mathf.SmoothDamp(transform.position.x, xZoomTarget,  ref xVel,  st);
        float newY = Mathf.SmoothDamp(transform.position.y, targetY,       ref _yVel, sty);
        float newZ = Mathf.SmoothDamp(transform.position.z, GetTargetZ(),  ref zVel,  st);

        if (float.IsNaN(newX) || float.IsNaN(newY) || float.IsNaN(newZ))
        {
            xVel = 0f; zVel = 0f; _yVel = 0f;
            return;
        }

        transform.position = new Vector3(newX, newY, newZ);
        transform.rotation = Quaternion.Euler(rotationX, rotationY, rotationZ);
    }

    private void LateUpdate()
    {
        if (player.position.x > zoomInThreshold && !zoomed)
            Zoom(1);
        else if (player.position.x < zoomOutThreshold && zoomed)
            Zoom(-1);
    }

    // ── API ──────────────────────────────────────────────────────────────────

    public void Zoom(int zoom)
    {
        xZoomTarget += zoomDistance * zoom;
        zoomed = zoom == 1;
    }

    public void TeleportTo(Vector3 position)
    {
        transform.position = position;
        _baseX      = position.x;
        xZoomTarget = position.x;
        xVel  = 0f;
        zVel  = 0f;
        _yVel = 0f;
        combatY = position.y;
        zoomed  = false;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private float GetTargetZ()
    {
        float z = player.position.z;
        if (z < zTransitionMin) return zTargetInside;
        if (z > zTransitionMax) return zTargetOutside;
        return z;
    }
}
