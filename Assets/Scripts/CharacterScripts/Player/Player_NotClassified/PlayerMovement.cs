using TMPro;
using UnityEngine;


/// <summary>
/// Single responsibility: manage the player's movement physics.
/// Includes gravity, ground detection, direction inertia, and speed utilities.
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

    private Vector3 _lastPosition;

    // ==================================================
    // RUNTIME STATE
    // ==================================================
    public Vector3 CurrentSpeed { get; set; }
    public Vector3 LastDirection { get; set; } = Vector3.zero;
    public Vector3 LastFacingDirection { get; private set; } = Vector3.forward;

    private float _verticalVelocity = 0f;
    private CharacterController _cc;
    private PlayerInputHandler _input;
    private PlayerController _playerController;

    // ==================================================
    // INITIALIZATION
    // ==================================================

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputHandler>();
        _playerController = GetComponent<PlayerController>();
        _verticalVelocity = -2f;
    }

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Converts the player's 2D input into a normalized 3D direction.
    /// Returns Vector3.zero if input is below the minimum threshold.
    /// </summary>
    public Vector3 GetDirectionalInput()
    {
        if (_input.MoveInput.magnitude < 0.1f)
            return Vector3.zero;

        return new Vector3(_input.MoveInput.x, 0f, _input.MoveInput.y).normalized;
    }

    /// <summary>
    /// Applies smooth inertia to the movement direction.
    /// Uses MoveTowards for a fluid transition between directions.
    /// </summary>
    // Replaces the current ApplyInertia with this one
    public Vector3 ApplyInertia(Vector3 inputDir, float deltaTime, float turnSpeed)
    {
        if (inputDir.magnitude > 0.1f)
            LastFacingDirection = inputDir.normalized;

        var dir = Vector3.MoveTowards(LastDirection, inputDir, turnSpeed * deltaTime);

        if (dir.magnitude < 0.01f)
            dir = Vector3.zero;

        LastDirection = dir;
        return LastDirection;
    }

    public void RefreshSpriteFlip()
    {
        UpdateSpriteFlip(LastFacingDirection.z);
    }

    public void ResetMotion()
    {
        CurrentSpeed = Vector3.zero;
        LastDirection = Vector3.zero;
        _verticalVelocity = -2f;
    }

    // For sprites with naturally inverted orientation (idle, static punch, etc.)
    public void EnforceInvertedSpriteFlip()
    {
        float dirZ = LastFacingDirection.z;
        float dirX = LastFacingDirection.x;

        // IdleFront: active when the last direction is mainly in X (depth)
        _playerController?.animator?.SetBool("IdleFront",
            Mathf.Abs(dirX) > Mathf.Abs(dirZ));

        if (Mathf.Abs(dirZ) < 0.1f) return;

        Vector3 scale = transform.localScale;
        scale.x = dirZ < 0 ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    private void UpdateSpriteFlip(float directionZ)
    {
        // IdleFront: active when the player moves in depth (X), not horizontal (Z)
        _playerController?.animator?.SetBool("IdleFront",
            Mathf.Abs(LastFacingDirection.x) > Mathf.Abs(directionZ));

        if (spriteRenderer == null) return;
        if (Mathf.Abs(directionZ) < 0.1f) return;

        // In this game the camera looks along X → Z is left/right on screen.
        // Z < 0 → player faces left (positive scale)
        // Z > 0 → player faces right (negative scale)
        Vector3 scale = transform.localScale;
        scale.x = directionZ < 0
            ? Mathf.Abs(scale.x)
            : -Mathf.Abs(scale.x);
        transform.localScale = scale;
    }

    /// <summary>
    /// Applies gravity and moves the CharacterController on the Y axis.
    /// Call once per Update(), after any horizontal movement.
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
    /// Checks whether the player is on the ground using a SphereCast.
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
    /// Updates the on-screen speed display (only if assigned).
    /// </summary>
    public void SpeedManagement()
    {
        if (speedDisplay == null) return;

        float speed = PlayerAnimations.instance != null
            ? PlayerAnimations.instance.BlendSpeed
            : 0f;

        speedDisplay.text = speed > 0.15f
            ? (Mathf.Round(speed * 10f) / 10f).ToString("F1")
            : "0,0";
    }
}
