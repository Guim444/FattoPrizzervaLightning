using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public Transform player;
    public float smoothSpeed = 5f;

    public float minZ;
    public float maxZ;

    void LateUpdate()
    {
        if (player == null) return;

        float targetZ = Mathf.Clamp(player.position.z, minZ, maxZ);

        Vector3 targetPos = new Vector3(transform.position.x, transform.position.y, targetZ);

        transform.position = Vector3.Lerp(transform.position, targetPos, smoothSpeed * Time.deltaTime);
    }
}