using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Target")]
    public Transform player;      // Assign player in Inspector
    public Transform center;

    [Header("Smoothness")]
    public float smoothSpeed = 5f;

    [Header("Clamps")]
    public float minZ = 5f;
    public float maxZ = -5f;

    [Header("Dead Zone Margins")]
    public float marginZ = 2f;    // How far player can move up/down before camera moves

    private Vector3 initialOffset;
    void Awake()
    {
        if (player != null)
            initialOffset = transform.position - center.position;
    }


    void Update()
    {
        if (player == null) return;

        Vector3 targetPos = transform.position; // Start with current camera pos
        Vector3 desiredPos = player.position + initialOffset;

        // --- Vertical check (Z axis) ---
        if (Mathf.Abs(desiredPos.z - transform.position.z) > marginZ)
            targetPos.z = Mathf.Clamp(desiredPos.z, minZ, maxZ);

        // --- Keep Y fixed (camera height) ---
        targetPos.y = transform.position.y;

        // Smoothly move towards target
        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
}