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

    private void Start()
    {
        groundPlane.localRotation = Quaternion.Euler(0f, 0f, -startTiltAngle);
    }

    private void Update()
    {
        float progress         = Mathf.Clamp01((player.position.x - playerStartX) / walkDistance);
        float rotationProgress = Mathf.Clamp01((progress - rotationStartProgress) / (1f - rotationStartProgress));
        float angle            = Mathf.Lerp(startTiltAngle, endTiltAngle, rotationProgress);
        groundPlane.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }
}