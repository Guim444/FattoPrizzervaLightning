using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [HideInInspector] public bool freeMoveMode = false;

    [Header("Target")]
    public Transform player;

    public Transform center;

    private float xZoomTarget; 
    private float xVel;
    private float zVel;
    public bool zoomed;

    [Header("Smoothness")]
    public float smoothTime = 0.3f;

    [Header("Zoom X")]
    public float zoomDistance;

    [Header("Zonas Z")]
    public float zThreshold = 8f;

    public float zTargetInside = 4f;
    public float zTargetOutside = 14f;

    [Header("Posición de combate")]
    public float combatY = 1.5f;

    private void Awake()
    {
        xZoomTarget = transform.position.x;
    }

    void Update()
    {
        // Z siempre estático — sin importar el modo
        float targetZ = player.position.z > zThreshold ? zTargetOutside : zTargetInside;

        float newX = Mathf.SmoothDamp(transform.position.x, xZoomTarget, ref xVel, smoothTime);
        float newZ = Mathf.SmoothDamp(transform.position.z, targetZ, ref zVel, smoothTime);

        transform.position = new Vector3(newX, combatY, newZ);
    }

    private void LateUpdate()
    {
        // Sin return — zoom activo en ambos modos
        if (player.position.x > center.position.x + 0.5f && !zoomed)
            Zoom(1);
        else if (player.position.x < center.position.x - 0.5f && zoomed)
            Zoom(-1);
    }

    public void Zoom(int zoom)
    {
        xZoomTarget += zoomDistance * zoom; // ← acumula sobre el target, no sobre transform
        zoomed = zoom == 1;
    }
}
