using TMPro;
using UnityEngine;


/// <summary>
/// Single responsibility: manage the player's movement physics.
/// Includes gravity, ground detection, direction inertia, and speed utilities.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    private static readonly int IdleFrontHash = Animator.StringToHash("IdleFront");

    [Header("Game Design: Movement Speeds")]
    public float walkingSpeed = 3f;

    public float runningBaseSpeed = 6f;
    public float tiredSpeed = 1f;
    public float glidingSpeed = 3f;
    public float punchingSpeed = 2f;
    public float punchRunningBaseSpeed = 6f;

    [Header("Visuals")]
    [SerializeField] private Transform visualFlipRoot;

    [Header("Game Design: Turn Speeds")]
    public float runningTurnSpeedNormal = 4f;

    public float runningTurnSpeedPhase3 = 2f;
    public float walkingTurnSpeed = 5f;
    public float tiredTurnSpeed = 10f;
    public float glidingTurnSpeed = 5f;
    public float punchRunningTurnSpeed = 4f;

    [Header("Game Design: Running Thrust System")]
    public float thrustPhaseThreshold = 1.5f;

    [Header("Gravity")]
    public float gravity = -20f;

    public float groundCheckDistance = 0.2f;
    public LayerMask groundMask;

    [Header("Debug")]
    public TextMeshProUGUI speedDisplay;

    private Vector3 _lastPosition;
    public Vector3 CurrentSpeed { get; set; }
    public Vector3 LastDirection { get; set; } = Vector3.zero;
    public Vector3 LastFacingDirection { get; private set; } = Vector3.forward;

    private float _verticalVelocity = 0f;
    private CharacterController _cc;
    private PlayerInputHandler _input;
    private PlayerController _playerController;
    private PlayerAnimations _animations;
    private SpriteRenderer _spriteRenderer;
    private RuntimeAnimatorController _cachedAnimatorController;
    private bool _cachedHasIdleFrontParameter;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputHandler>();
        _playerController = GetComponent<PlayerController>();
        _animations = GetComponent<PlayerAnimations>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        _verticalVelocity = -2f;
    }

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
        {
            LastFacingDirection = inputDir.normalized;
            ApplyCurrentStateSpriteFlip();
        }

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
        SetIdleFrontIfAvailable(Mathf.Abs(dirX) > Mathf.Abs(dirZ));

        if (Mathf.Abs(dirZ) < 0.1f) return;

        ApplyVisualFlip(directionZ: dirZ, inverted: true);
    }

    private void UpdateSpriteFlip(float directionZ)
    {
        // IdleFront: active when the player moves in depth (X), not horizontal (Z)
        SetIdleFrontIfAvailable(Mathf.Abs(LastFacingDirection.x) > Mathf.Abs(directionZ));

        if (Mathf.Abs(directionZ) < 0.1f) return;

        ApplyVisualFlip(directionZ, inverted: false);
    }

    private void ApplyVisualFlip(float directionZ, bool inverted)
    {
        float targetSign = GetFacingScaleSign(directionZ, inverted);

        if (visualFlipRoot != null)
            ApplyScaleSign(visualFlipRoot, targetSign);
        else if (_spriteRenderer != null)
            ApplySpriteRendererFlip(targetSign);
        else
            ApplyBodyScaleSign(targetSign);
    }

    public float GetFacingCompensatedCenterX(float baseCenterX)
    {
        if (visualFlipRoot != null || _spriteRenderer != null) return baseCenterX;

        return baseCenterX / GetNonZeroSign(transform.localScale.x);
    }

    private float GetFacingScaleSign(float directionZ, bool inverted)
    {
        // This preserves the original project convention: Z drives screen left/right.
        if (inverted)
            return directionZ < 0f ? -1f : 1f;

        return directionZ < 0f ? 1f : -1f;
    }

    private void ApplyScaleSign(Transform target, float sign)
    {
        Vector3 scale = target.localScale;
        float magnitude = Mathf.Abs(scale.x);
        if (Mathf.Approximately(magnitude, 0f))
            magnitude = 1f;

        scale.x = magnitude * sign;
        target.localScale = scale;
    }

    private void ApplySpriteRendererFlip(float sign)
    {
        _spriteRenderer.flipX = sign < 0f;
    }

    private void ApplyBodyScaleSign(float sign)
    {
        float currentSign = GetNonZeroSign(transform.localScale.x);
        if (Mathf.Approximately(currentSign, sign)) return;

        Vector3 previousControllerWorldCenter = _cc != null
            ? transform.TransformPoint(_cc.center)
            : Vector3.zero;

        ApplyScaleSign(transform, sign);

        if (_cc == null) return;

        Vector3 compensatedCenter = transform.InverseTransformPoint(previousControllerWorldCenter);
        Vector3 center = _cc.center;
        center.x = compensatedCenter.x;
        _cc.center = center;
    }

    private void SetIdleFrontIfAvailable(bool value)
    {
        Animator animator = _playerController != null ? _playerController.animator : null;
        if (!HasIdleFrontParameter(animator)) return;

        animator.SetBool(IdleFrontHash, value);
    }

    private bool HasIdleFrontParameter(Animator animator)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        if (_cachedAnimatorController == animator.runtimeAnimatorController)
            return _cachedHasIdleFrontParameter;

        _cachedAnimatorController = animator.runtimeAnimatorController;
        _cachedHasIdleFrontParameter = false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash != IdleFrontHash ||
                parameter.type != AnimatorControllerParameterType.Bool)
                continue;

            _cachedHasIdleFrontParameter = true;
            break;
        }

        return _cachedHasIdleFrontParameter;
    }

    private float GetNonZeroSign(float value)
    {
        float sign = Mathf.Sign(value);
        return Mathf.Approximately(sign, 0f) ? 1f : sign;
    }

    private void ApplyCurrentStateSpriteFlip()
    {
        if (_playerController == null) return;

        State state = _playerController.currentState;
        if (state == State.Moving || state == State.Running || state == State.Idle)
            EnforceInvertedSpriteFlip();
        else
            RefreshSpriteFlip();
    }

    /// <summary>
    /// Applies gravity and moves the CharacterController on the Y axis.
    /// Call once per Update(), after any horizontal movement.
    /// </summary>
    public void ApplyGravity()
    {
        bool grounded = IsGrounded();
        _playerController.isGrounded = grounded;

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

        float speed = _animations != null
            ? _animations.BlendSpeed
            : 0f;

        speedDisplay.text = speed > 0.15f
            ? (Mathf.Round(speed * 10f) / 10f).ToString("F1")
            : "0,0";
    }
}
