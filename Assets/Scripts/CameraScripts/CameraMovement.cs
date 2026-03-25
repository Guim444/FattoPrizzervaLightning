using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Target")]
    public Transform player;
    [Header("Offsets")]
    public float yOffset = 2f;
    [Header("Smoothness")]
    public float smoothTime;
    private Vector3 velocity = Vector3.zero;

    [Header("Clamps")]
    public float minZ;
    public float maxZ;
    [Header("Zoom")]
    public float xOffset = -3f;    // distancia fija de la cámara al jugador en X
    public float xMin;             // límite mínimo de X de la cámara
    public float xMax;   
    

    private void LateUpdate()
    {
        float targetX = Mathf.Clamp(player.position.x + xOffset, xMin, xMax);
        float targetZ = Mathf.Clamp(player.position.z, minZ, maxZ);

        Vector3 targetPos = new Vector3(targetX, player.position.y + yOffset, targetZ);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity, smoothTime);
    }

}
