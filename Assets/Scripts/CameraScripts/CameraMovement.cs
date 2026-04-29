using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [HideInInspector] public bool freeMoveMode = false;
    [HideInInspector] public bool followPlayerX = false;

    [Header("Target")]
    public Transform player;

    public Transform center;

    [Header("Smoothness")]
    public float smoothTime = 0.3f;

    [Header("Zoom X")]
    public float zoomDistance;

    [Header("Zonas Z")]
    public float zTransitionMin = 6f;

    public float zTransitionMax = 10f;
    public float zTargetInside = 4f;
    public float zTargetOutside = 14f;

    [Header("Posición Y")]
    public float combatY = 1.5f;

    public bool followPlayerY = false;
    public float yOffset = 1.5f;
    public float ySmoothTime = 0.4f;

    [Header("Cámara")]
    [SerializeField] float farClipPlane = 40f;

    [SerializeField] float fieldOfView = 40f;

    public bool zoomed;

    private float _combatX;
    [HideInInspector] public float xOffset;
    private float xZoomTarget;
    private float xVel;
    private float zVel;
    private float _yVel;
    private Camera _cam;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam != null)
        {
            _cam.farClipPlane = farClipPlane;
            _cam.fieldOfView = fieldOfView;
        }

        Vector3 pos = transform.position;
        if (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z))
        {
            Debug.LogWarning("[CameraMovement] Posición inicial inválida — reseteando a origen.");
            transform.position = Vector3.zero;
        }

        _combatX = transform.position.x;
        xZoomTarget = _combatX;
        xOffset = transform.position.x - player.position.x;
        xVel = 0f;
    }

    public void ResetToCombatX()
    {
        xZoomTarget = _combatX;
        xVel = 0f;
        zoomed = false;
    }

    void Update()
    {
        if (followPlayerX)
            xZoomTarget = player.position.x + xOffset;

        float targetZ;
        if (player.position.z < zTransitionMin)
            targetZ = zTargetInside;
        else if (player.position.z > zTransitionMax)
            targetZ = zTargetOutside;
        else
            targetZ = player.position.z;

        float targetY = followPlayerY ? player.position.y + yOffset : combatY;

        float st = Mathf.Max(smoothTime, 0.01f);
        float sty = Mathf.Max(ySmoothTime, 0.01f);

        float newX = Mathf.SmoothDamp(transform.position.x, xZoomTarget, ref xVel, st);
        float newY = Mathf.SmoothDamp(transform.position.y, targetY, ref _yVel, sty);
        float newZ = Mathf.SmoothDamp(transform.position.z, targetZ, ref zVel, st);

        if (float.IsNaN(newX) || float.IsNaN(newY) || float.IsNaN(newZ))
        {
            xVel = 0f;
            zVel = 0f;
            _yVel = 0f;
            return;
        }

        transform.position = new Vector3(newX, newY, newZ);
    }

    private void LateUpdate()
    {
        if (player.position.x > center.position.x + 0.5f && !zoomed)
            Zoom(1);
        else if (player.position.x < center.position.x - 0.5f && zoomed)
            Zoom(-1);
    }

    public void Zoom(int zoom)
    {
        xZoomTarget += zoomDistance * zoom;
        zoomed = zoom == 1;
    }

    public void RefreshCombatX()
    {
        xOffset = -5f;
        xZoomTarget = player.position.x + xOffset;
        zoomed = false;
    }

    /// <summary>
    /// Inicializa el seguimiento de cámara desde las posiciones actuales de escena,
    /// sin hardcodear ningún offset. Usar cuando cámara y jugador ya están donde deben.
    /// </summary>
    public void RefreshFromCurrentPositions()
    {
        xOffset = transform.position.x - player.position.x;
        xZoomTarget = transform.position.x;
        xVel = 0f;
        zoomed = false;
    }

    /// <summary>
    /// Teleporta la cámara a una posición exacta e inicializa todo el estado
    /// de SmoothDamp para que no arrastre desde la posición anterior.
    /// </summary>
    public void TeleportTo(Vector3 position)
    {
        transform.position = position;
        _combatX = position.x;
        xZoomTarget = position.x;
        xVel = 0f;
        zVel = 0f;
        _yVel = 0f;
        combatY = position.y;
        zoomed = false;
    }
}
