using Cinemachine;
using UnityEngine;

/// <summary>
/// Controla el zoom moviendo físicamente la cámara (CameraDistance) según la
/// profundidad del jugador en X. Al no cambiar el FOV, el fondo no sufre
/// distorsión de perspectiva.
/// </summary>
public class CinemachineZoomController : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private Transform player;

    [Header("Zonas de profundidad (eje X del jugador)")]
    [Tooltip("Por debajo de este X el jugador está 'cerca' de la cámara")]
    [SerializeField] private float xTransitionMin = 6f;

    [Tooltip("Por encima de este X el jugador está 'lejos' de la cámara")]
    [SerializeField] private float xTransitionMax = 10f;

    [Header("Distancia de cámara")]
    [Tooltip("Distancia cuando el jugador está cerca (zoom in)")]
    [SerializeField] private float distanceClose = 7f;

    [Tooltip("Distancia cuando el jugador está lejos (zoom out)")]
    [SerializeField] private float distanceFar = 13f;

    [Tooltip("Velocidad de la transición")]
    [SerializeField] private float smoothTime = 0.3f;

    private CinemachineFramingTransposer _transposer;
    private float _distanceVelocity;

    private void Awake()
    {
        if (virtualCamera == null)
        {
            Debug.LogError("[CinemachineZoomController] Virtual Camera no asignada.");
            enabled = false;
            return;
        }

        _transposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        if (_transposer == null)
        {
            Debug.LogError("[CinemachineZoomController] No se encontró CinemachineFramingTransposer.");
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