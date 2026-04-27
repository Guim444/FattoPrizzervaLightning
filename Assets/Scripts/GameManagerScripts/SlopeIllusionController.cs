using UnityEngine;

public class SlopeIllusionController : MonoBehaviour
{
    [Header("Plano")]
    [SerializeField] Transform groundPlane;

    [SerializeField] Transform player;
    [SerializeField] float startTiltAngle;
    [SerializeField] float endTiltAngle = 10f;
    [SerializeField] float playerStartX = -90f;
    [SerializeField] float walkDistance = 90f;
    [SerializeField] [Range(0f, 1f)] float rotationStartProgress = 0.75f;


    [Header("Cámara")]
    [SerializeField] CameraMovement cameraMovement;

    [SerializeField] float yOffset = 1.5f;
    [SerializeField] float introCamXOffset = -15f;

    private float _originalXOffset;

    private void Start()
    {
        groundPlane.localRotation = Quaternion.Euler(0f, 0f, -startTiltAngle);

        if (cameraMovement != null)
        {
            _originalXOffset = cameraMovement.xOffset;
            cameraMovement.xOffset = introCamXOffset;
            cameraMovement.yOffset = yOffset;
            cameraMovement.followPlayerY = true;
        }
    }


    private void OnDisable()
    {
        if (cameraMovement != null)
        {
            cameraMovement.xOffset = _originalXOffset;
            cameraMovement.followPlayerY = false;
        }
    }

    private void Update()
    {
        float progress = Mathf.Clamp01((player.position.x - playerStartX) / walkDistance);
        float rotationProgress = Mathf.Clamp01((progress - rotationStartProgress) / (1f - rotationStartProgress));
        float angle = Mathf.Lerp(startTiltAngle, endTiltAngle, rotationProgress);
        groundPlane.localRotation = Quaternion.Euler(0f, 0f, -angle);
        if (cameraMovement != null)
            cameraMovement.xOffset = introCamXOffset;
    }
}
