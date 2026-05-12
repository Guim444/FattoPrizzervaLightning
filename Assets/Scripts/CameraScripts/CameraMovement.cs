using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Posición inicial")]
    [Tooltip("Offset en X respecto al jugador al activarse (ej: -11 pone la cámara a X = jugador.x - 11)")]
    [SerializeField] private float startXOffset = -11f;

    [Header("Smoothness")]
    public float smoothTime  = 0.3f;
    public float ySmoothTime = 0.4f;

    [Header("Zoom X")]
    public float zoomDistance;

    [Header("Zonas Z")]
    public float zTransitionMin  = 6f;
    public float zTransitionMax  = 10f;
    public float zTargetInside   = 4f;
    public float zTargetOutside  = 14f;

    [Header("Posición Y")]
    public float combatY      = 1.5f;
    public bool  followPlayerY = false;
    public float yOffset      = 1.5f;

    [Header("Cámara")]
    [SerializeField] private float farClipPlane = 40f;

    public bool zoomed;

    private float _baseX;       // X de referencia desde donde opera el zoom
    private float xZoomTarget;
    private float xVel;
    private float zVel;
    private float _yVel;
    private Camera _cam;

    // ── Ciclo de vida ────────────────────────────────────────────────────────

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam != null)
            _cam.farClipPlane = farClipPlane;
    }

    private void OnEnable()
    {
        if (player == null) return;

        // Snap inmediato a la posición relativa al jugador
        float targetX = player.position.x + startXOffset;
        float targetY = followPlayerY ? player.position.y + yOffset : combatY;
        float targetZ = GetTargetZ();

        transform.position = new Vector3(targetX, targetY, targetZ);

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
    }

    private void LateUpdate()
    {
        if (player.position.x > 0.5f && !zoomed)
            Zoom(1);
        else if (player.position.x < -0.5f && zoomed)
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