using Cinemachine;
using UnityEngine;

/// <summary>
/// Controla el zoom (FOV) de la virtual camera según la profundidad del jugador en X.
/// Cuando el jugador se acerca a la cámara (X menor) el FOV disminuye (zoom in).
/// Cuando se aleja (X mayor) el FOV aumenta (zoom out).
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

    [Header("FOV")]
    [Tooltip("FOV cuando el jugador está cerca (X pequeña)")]
    [SerializeField] private float fovClose = 45f;

    [Tooltip("FOV cuando el jugador está lejos (X grande)")]
    [SerializeField] private float fovFar = 65f;

    [Tooltip("Velocidad de la transición de FOV")]
    [SerializeField] private float smoothTime = 0.3f;

    private float _fovVelocity;

    private void Awake()
    {
        if (virtualCamera == null)
        {
            Debug.LogError("[CinemachineZoomController] Virtual Camera no asignada.");
            enabled = false;
            return;
        }

        virtualCamera.m_Lens.FieldOfView = GetTargetFOV();
    }

    private void Update()
    {
        float target  = GetTargetFOV();
        float current = virtualCamera.m_Lens.FieldOfView;
        virtualCamera.m_Lens.FieldOfView = Mathf.SmoothDamp(current, target, ref _fovVelocity, smoothTime);
    }

    private float GetTargetFOV()
    {
        float x = player.position.x;

        if (x <= xTransitionMin) return fovClose;
        if (x >= xTransitionMax) return fovFar;

        float t = (x - xTransitionMin) / (xTransitionMax - xTransitionMin);
        return Mathf.Lerp(fovClose, fovFar, t);
    }
}