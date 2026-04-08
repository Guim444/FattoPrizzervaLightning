using TMPro;
using UnityEngine;


/// <summary>
/// Responsabilidad única: gestionar la física de movimiento del jugador.
/// Incluye gravedad, detección de suelo, inercia de dirección y utilidades de velocidad.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    // ==================================================
    // MOVEMENT SPEEDS
    // ==================================================
    [Header("Game Design: Movement Speeds")]
    public float walkingSpeed = 3f;

    public float runningBaseSpeed = 6f;
    public float tiredSpeed = 1f;
    public float glidingSpeed = 3f;
    public float punchingSpeed = 2f;
    public float punchRunningBaseSpeed = 6f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    // ==================================================
    // TURN SPEEDS
    // ==================================================
    [Header("Game Design: Turn Speeds")]
    public float runningTurnSpeedNormal = 4f;

    public float runningTurnSpeedPhase3 = 2f;
    public float walkingTurnSpeed = 5f;
    public float tiredTurnSpeed = 10f;
    public float glidingTurnSpeed = 5f;
    public float punchRunningTurnSpeed = 4f;

    // ==================================================
    // RUNNING THRUST SYSTEM
    // ==================================================
    [Header("Game Design: Running Thrust System")]
    public float thrustPhaseThreshold = 1.5f;

    // ==================================================
    // GRAVITY
    // ==================================================
    [Header("Gravity")]
    public float gravity = -20f;

    public float groundCheckDistance = 0.2f;
    public LayerMask groundMask;

    // ==================================================
    // SPEED DEBUG DISPLAY
    // ==================================================
    [Header("Debug")]
    public TextMeshProUGUI speedDisplay;
    private float _smoothedSpeed = 0f;
    private float _updateInterval = 0.1f; // actualiza cada 100ms

    private float _timer;

    // ==================================================
    // RUNTIME STATE
    // ==================================================
    public Vector3 CurrentSpeed { get; set; }
    public Vector3 LastDirection { get; set; } = Vector3.zero;
    public Vector3 LastFacingDirection { get; private set; } = Vector3.forward;

    private float _verticalVelocity = 0f;
    private CharacterController _cc;
    private PlayerInputHandler _input;

    // ==================================================
    // INITIALIZATION
    // ==================================================

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputHandler>();
        _verticalVelocity = -2f;
    }

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Convierte el input 2D del jugador en una dirección 3D normalizada.
    /// Retorna Vector3.zero si el input es menor al umbral mínimo.
    /// </summary>
    public Vector3 GetDirectionalInput()
    {
        if (_input.MoveInput.magnitude < 0.1f)
            return Vector3.zero;

        return new Vector3(_input.MoveInput.x, 0f, _input.MoveInput.y).normalized;
    }

    /// <summary>
    /// Aplica inercia suave a la dirección de movimiento.
    /// Usa MoveTowards para una transición fluida entre direcciones.
    /// </summary>
    // Reemplaza el ApplyInertia actual por este
    public Vector3 ApplyInertia(Vector3 inputDir, float deltaTime, float turnSpeed)
    {
        if (inputDir.magnitude > 0.1f)
        {
            LastFacingDirection = inputDir.normalized;
            UpdateSpriteFlip(LastFacingDirection.z);
        }

        var dir = Vector3.MoveTowards(LastDirection, inputDir, turnSpeed * deltaTime);

        if (dir.magnitude < 0.01f)
            dir = Vector3.zero;

        LastDirection = dir;
        return LastDirection;
    }

    private void UpdateSpriteFlip(float directionZ)
    {
        if (spriteRenderer == null) return;
        if (Mathf.Abs(directionZ) < 0.1f) return; // solo flip en movimiento horizontal

        Vector3 scale = transform.localScale;
        scale.x = directionZ < 0
            ? Mathf.Abs(scale.x)
            : -Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    /// <summary>
    /// Aplica gravedad y mueve el CharacterController en el eje Y.
    /// Llamar una vez por Update(), después de cualquier movimiento horizontal.
    /// </summary>
    public void ApplyGravity()
    {
        bool grounded = IsGrounded();
        GetComponent<PlayerController>().isGrounded = grounded;

        if (grounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
        else
            _verticalVelocity += gravity * Time.deltaTime;

        if (_cc.enabled)
            _cc.Move(new Vector3(0, _verticalVelocity * Time.deltaTime, 0));
    }

    /// <summary>
    /// Comprueba si el jugador está sobre el suelo usando un SphereCast.
    /// </summary>
    public bool IsGrounded()
    {
        Vector3 bottom = transform.position + _cc.center
                         - Vector3.up * (_cc.height / 2f - _cc.radius);

        return Physics.SphereCast(
            bottom,
            _cc.radius * 0.9f,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundMask);
    }

    /// <summary>
    /// Actualiza el display de velocidad en pantalla (solo si está asignado).
    /// </summary>
    public void SpeedManagement()
    {
        float realSpeed = _cc.velocity.magnitude;

        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, realSpeed, Time.deltaTime * 15f);

        if (speedDisplay != null)
            speedDisplay.text = _smoothedSpeed.ToString("F1");
    }
}
