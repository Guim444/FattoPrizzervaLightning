using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    public static PlayerAnimations instance;
    public Animator animator;
    public PlayerController player;

    public bool pauseAnimatorControl = false;

    [Header("Character Controller: Idle Front")]
    [SerializeField] private float idleFrontCenterX = 0f;
    [SerializeField] private float defaultCenterX = -1f;

    // ── BLEND TREE THRESHOLDS ────────────────────────────────────
    [Header("Blend Tree: Speed Thresholds")]
    public float walkThreshold   = 4.4f;
    public float runThreshold    = 9f;
    public float runMidThreshold = 11f;
    public float runMaxThreshold = 13.5f;

    // ── ACCELERATION PER SEGMENT (units/second with Time.deltaTime) ──
    [Header("Blend Tree: Acceleration per segment (u/s)")]
    public float accelToWalk     = 8f;
    public float accelWalkToRun  = 3.5f;
    public float accelRunToMid   = 4.5f;
    public float accelMidToMax   = 6.0f;

    public float BlendSpeed => _blendSpeed;
    private float _blendSpeed;
    private CharacterController _characterController;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        _characterController = GetComponent<CharacterController>();
    }

    void LateUpdate()
    {
        if (player == null || animator == null) return;
        if (pauseAnimatorControl) return;

        UpdateBlendSpeed();

        animator.SetFloat("Speed",        _blendSpeed);
<<<<<<< HEAD
        animator.SetBool("isMoving",      player.currentState == State.Moving
                                       || player.currentState == State.Running
                                       || player.currentState == State.Deenergized);
        animator.SetBool("isRunning",     player.currentState == State.Running
                                       || player.currentState == State.PunchRunning);
        animator.SetBool("isGliding",     player.currentState == State.Gliding);
        animator.SetBool("isKnockedback", player.knockbackHandler.ShowKnockbackAnim);
        animator.SetBool("isDead",        player.combat.HP <= 0);
        bool isIdleFront = player.currentState == State.Idle
                        && Mathf.Abs(player.movement.LastFacingDirection.x) > 0.1f;

        animator.SetBool("IdleFront", isIdleFront);
        UpdateCharacterControllerCenter(IsAnimatorInIdleFront());
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

        float targetX;
        if (isIdleFront)
            targetX = idleFrontCenterX;
        else
        {
            bool isMoving = player.currentState == State.Moving
                         || player.currentState == State.Running
                         || player.currentState == State.PunchRunning;
            targetX = isMoving ? idleFrontCenterX : defaultCenterX;
        }

        if (Mathf.Approximately(_characterController.center.x, targetX)) return;

        Vector3 center = _characterController.center;
        center.x = targetX;
        _characterController.center = center;
    }

    void EnforceSpriteFlip()
    {
        var state = player.currentState;

        if (state == State.Moving || state == State.Running || state == State.Idle)
            player.movement.EnforceInvertedSpriteFlip();
        else if (state == State.PunchRunning)
            player.movement.RefreshSpriteFlip();
=======
        animator.SetBool("isMoving",      player.StateMachineController.CurrentState == State.Moving
                                       || player.StateMachineController.CurrentState == State.Running
                                       || player.StateMachineController.CurrentState == State.Deenergized);
        animator.SetBool("isRunning",     player.StateMachineController.CurrentState == State.Running
                                       || player.StateMachineController.CurrentState == State.PunchRunning);
        animator.SetBool("isGliding",     player.StateMachineController.CurrentState == State.Gliding);
        animator.SetBool("isKnockedback", player.KnockbackHandler.ShowKnockbackAnim);
        animator.SetBool("isDead",        player.Combat.HP <= 0);
        animator.SetBool("IdleFront",     player.StateMachineController.CurrentState == State.Idle
                                       && Mathf.Abs(player.Movement.LastDirection.x) > 0.1f);
>>>>>>> origin/Albert_Branch
    }

    void UpdateBlendSpeed()
    {
        bool isWalking = player.StateMachineController.CurrentState == State.Moving;
        bool isRunning = player.StateMachineController.CurrentState == State.Running
                      || player.StateMachineController.CurrentState == State.PunchRunning;

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
}
