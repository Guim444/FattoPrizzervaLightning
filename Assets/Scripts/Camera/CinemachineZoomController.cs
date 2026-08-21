using Cinemachine;
using UnityEngine;

/// <summary>
/// Controls zoom by physically moving the camera (CameraDistance) based on
/// the player's X depth. By not changing the FOV, the background avoids
/// perspective distortion.
/// </summary>
public class CinemachineZoomController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private Transform player;

    [Header("Depth Zones (player X axis)")]
    [Tooltip("Below this X the player is 'close' to the camera")]
    [SerializeField] private float xTransitionMin = 6f;

    [Tooltip("Above this X the player is 'far' from the camera")]
    [SerializeField] private float xTransitionMax = 10f;

    [Header("Camera Distance")]
    [Tooltip("Distance when the player is close (zoom in)")]
    [SerializeField] private float distanceClose = 7f;

    [Tooltip("Distance when the player is far (zoom out)")]
    [SerializeField] private float distanceFar = 13f;

    [Tooltip("Transition speed")]
    [SerializeField] private float smoothTime = 0.3f;

    private CinemachineFramingTransposer _transposer;
    private float _distanceVelocity;

    private void Awake()
    {
        if (virtualCamera == null)
        {
            Debug.LogError("[CinemachineZoomController] Virtual Camera not assigned.");
            enabled = false;
            return;
        }

        _transposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (_transposer == null)
        {
            Debug.LogError("[CinemachineZoomController] CinemachineFramingTransposer not found.");
            enabled = false;
            return;
        }

        _transposer.m_CameraDistance = GetTargetDistance();
    }

    private void Update()
    {
        float target  = GetTargetDistance();
        float current = _transposer.m_CameraDistance;
        _transposer.m_CameraDistance = Mathf.SmoothDamp(current, target, ref _distanceVelocity, smoothTime);
    }

    private float GetTargetDistance()
    {
        float x = player.position.x;

        if (x <= xTransitionMin) return distanceClose;
        if (x >= xTransitionMax) return distanceFar;

        float t = (x - xTransitionMin) / (xTransitionMax - xTransitionMin);
        return Mathf.Lerp(distanceClose, distanceFar, t);
    }
}
