using UnityEngine;

public class SlopeIllusionController : MonoBehaviour
{
    [Header("Plano")]
    [SerializeField] Transform groundPlane;
    [SerializeField] Transform player;
    [SerializeField] float startTiltAngle = 20f;
    [SerializeField] float walkDistance   = 90f;

    [Header("Cámara Y")]
    [SerializeField] CameraMovement cameraMovement;
    [SerializeField] float cameraYStart = 8f;
    [SerializeField] float cameraYEnd   = 3f;

    private float _startX;

    private void Start()
    {
        _startX = player.position.x;
        groundPlane.localRotation      = Quaternion.Euler(0f, 0f, -startTiltAngle);
        if (cameraMovement != null)
            cameraMovement.combatY = cameraYStart;
    }

    private void Update()
    {
        float progress = Mathf.Clamp01((player.position.x - _startX) / walkDistance);

        groundPlane.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Lerp(startTiltAngle, 0f, progress));

        if (cameraMovement != null)
            cameraMovement.combatY = Mathf.Lerp(cameraYStart, cameraYEnd, progress);
    }
}
