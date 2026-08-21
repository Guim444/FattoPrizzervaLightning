using System.Collections.Generic;
using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
    private static readonly int IsGlidingHash = Animator.StringToHash("isGliding");
    private static readonly int IsKnockedbackHash = Animator.StringToHash("isKnockedback");
    private static readonly int IsDeadHash = Animator.StringToHash("isDead");
    private static readonly int IdleFrontHash = Animator.StringToHash("IdleFront");

    public Animator animator;
    public PlayerController player;

    public bool pauseAnimatorControl = false;

    [Header("Character Controller: Idle Front")]
    [SerializeField] private bool lockControllerCenterX = true;
    [SerializeField] private float idleFrontCenterX = 0f;
    [SerializeField] private float defaultCenterX = -1f;

    // BLEND TREE THRESHOLDS 
    [Header("Blend Tree: Speed Thresholds")]
    public float walkThreshold   = 4.4f;
    public float runThreshold    = 9f;
    public float runMidThreshold = 11f;
    public float runMaxThreshold = 13.5f;

    // ACCELERATION PER SEGMENT (units/second with Time.deltaTime)
    [Header("Blend Tree: Acceleration per segment (u/s)")]
    public float accelToWalk     = 8f;
    public float accelWalkToRun  = 3.5f;
    public float accelRunToMid   = 4.5f;
    public float accelMidToMax   = 6.0f;

    public float BlendSpeed => _blendSpeed;
    private float _blendSpeed;
    private CharacterController _characterController;
    private RuntimeAnimatorController _cachedAnimatorController;
    private readonly Dictionary<int, AnimatorControllerParameterType> _animatorParameterTypes =
        new Dictionary<int, AnimatorControllerParameterType>();

    void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        if (player == null || animator == null) return;
        if (pauseAnimatorControl) return;

        UpdateBlendSpeed();

        SetFloatIfAvailable(SpeedHash, _blendSpeed);
        SetBoolIfAvailable(IsMovingHash, player.currentState == State.Moving
                                      || player.currentState == State.Running
                                      || player.currentState == State.Deenergized);
        SetBoolIfAvailable(IsRunningHash, player.currentState == State.Running
                                       || player.currentState == State.PunchRunning);
        SetBoolIfAvailable(IsGlidingHash, player.currentState == State.Gliding);
        SetBoolIfAvailable(IsKnockedbackHash, player.knockbackHandler.ShowKnockbackAnim);
        SetBoolIfAvailable(IsDeadHash, player.combat.HP <= 0);
        bool isIdleFront = player.currentState == State.Idle
                        && Mathf.Abs(player.movement.LastFacingDirection.x) > 0.1f;

        SetBoolIfAvailable(IdleFrontHash, isIdleFront);
        UpdateCharacterControllerCenter(IsAnimatorInIdleFront());
        EnforceSpriteFlip();
    }

    void OnDidApplyAnimationProperties()
    {
        if (player == null || animator == null) return;
        if (pauseAnimatorControl) return;

        EnforceSpriteFlip();
    }

    bool IsAnimatorInIdleFront()
    {
        const int baseLayer = 0;

        if (animator.GetCurrentAnimatorStateInfo(baseLayer).IsName("Idle_Front"))
            return true;

        return animator.IsInTransition(baseLayer)
            && animator.GetNextAnimatorStateInfo(baseLayer).IsName("Idle_Front");
    }

    void UpdateCharacterControllerCenter(bool isIdleFront)
    {
        if (_characterController == null) return;
        if (lockControllerCenterX) return;

        float baseTargetX;
        if (isIdleFront)
            baseTargetX = idleFrontCenterX;
        else
        {
            bool isMoving = player.currentState == State.Moving
                         || player.currentState == State.Running
                         || player.currentState == State.PunchRunning;
            baseTargetX = isMoving ? idleFrontCenterX : defaultCenterX;
        }

        float targetX = player.movement != null
            ? player.movement.GetFacingCompensatedCenterX(baseTargetX)
            : baseTargetX;

        if (Mathf.Approximately(_characterController.center.x, targetX)) return;

        Vector3 center = _characterController.center;
        center.x = targetX;
        _characterController.center = center;
    }

    void EnforceSpriteFlip()
    {
        if (player == null || player.movement == null) return;

        var state = player.currentState;

        if (state == State.Moving || state == State.Running || state == State.Idle)
            player.movement.EnforceInvertedSpriteFlip();
        else if (state == State.PunchRunning)
            player.movement.RefreshSpriteFlip();
    }

    void UpdateBlendSpeed()
    {
        bool isWalking = player.currentState == State.Moving;
        bool isRunning = player.currentState == State.Running
                      || player.currentState == State.PunchRunning;

        if (!isWalking && !isRunning)
        {
            _blendSpeed = 0f; // immediate stop
            return;
        }

        // No run key: smoothly accelerate to walkThreshold
        if (isWalking)
        {
            _blendSpeed = Mathf.MoveTowards(_blendSpeed, walkThreshold, accelToWalk * Time.deltaTime);
            return;
        }

        // Run key held: immediate jump to walk if coming from stopped
        if (_blendSpeed < walkThreshold)
            _blendSpeed = walkThreshold;

        // Progressive acceleration per segment
        float accel = _blendSpeed < runThreshold    ? accelWalkToRun
                    : _blendSpeed < runMidThreshold  ? accelRunToMid
                    :                                  accelMidToMax;

        _blendSpeed = Mathf.Min(_blendSpeed + accel * Time.deltaTime, runMaxThreshold);
    }

    private void SetFloatIfAvailable(int parameterHash, float value)
    {
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Float))
            animator.SetFloat(parameterHash, value);
    }

    private void SetBoolIfAvailable(int parameterHash, bool value)
    {
        if (HasAnimatorParameter(parameterHash, AnimatorControllerParameterType.Bool))
            animator.SetBool(parameterHash, value);
    }

    private bool HasAnimatorParameter(int parameterHash, AnimatorControllerParameterType expectedType)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        if (_cachedAnimatorController != animator.runtimeAnimatorController)
        {
            _cachedAnimatorController = animator.runtimeAnimatorController;
            _animatorParameterTypes.Clear();

            foreach (AnimatorControllerParameter parameter in animator.parameters)
                _animatorParameterTypes[parameter.nameHash] = parameter.type;
        }

        return _animatorParameterTypes.TryGetValue(parameterHash, out AnimatorControllerParameterType actualType)
            && actualType == expectedType;
    }
}
