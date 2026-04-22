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

    public bool zoomed;

    private float _combatX;
    private float xOffset;
    private float xZoomTarget;
    private float xVel;
    private float zVel;
    private float _yVel;
    private Camera _cam;

    private void Awake()
    {
        _cam            = GetComponent<Camera>();
        if (_cam != null)
            _cam.farClipPlane = farClipPlane;

        _combatX    = transform.position.x;
        xZoomTarget = _combatX;
        xOffset     = transform.position.x - player.position.x;
        xVel        = 0f;
    }

    public void ResetToCombatX()
    {
        xZoomTarget = _combatX;
        xVel        = 0f;
        zoomed      = false;
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

        float newX = Mathf.SmoothDamp(transform.position.x, xZoomTarget, ref xVel, smoothTime);
        float newY = Mathf.SmoothDamp(transform.position.y, targetY,     ref _yVel, ySmoothTime);
        float newZ = Mathf.SmoothDamp(transform.position.z, targetZ,     ref zVel,  smoothTime);

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
        xOffset     = -5f;
        xZoomTarget = player.position.x + xOffset;
        zoomed      = false;
    }
}
