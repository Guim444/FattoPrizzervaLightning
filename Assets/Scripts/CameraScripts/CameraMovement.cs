using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Target")]
    public Transform player;
    public Transform center;

    float xZoom;
    public bool zoomed;

    [Header("Smoothness")]
    public Vector3 smoothSpeed;
    public float smoothTime;

    [Header("Clamps")]
    public float minZ;
    public float maxZ;
    public float zoomDistance;

    private void Awake()
    {
        xZoom = transform.position.x;
    }
    void Update()
    {
        float targetZ = Mathf.Clamp(player.position.z, minZ, maxZ);

        Vector3 targetPos = new Vector3(xZoom, transform.position.y, targetZ);

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref smoothSpeed, smoothTime);
    }
    private void LateUpdate()
    {
        if (player.position.x > center.position.x + 0.5f && !zoomed)
        {
            Zoom(1);
        }
        else if (player.position.x < center.position.x - 0.5f && zoomed)
        {
            Zoom(-1);
        }
    }

    public void Zoom(int zoom)
    {
        xZoom = transform.position.x + zoomDistance * zoom;
        zoomed = zoom == 1;
    }
}