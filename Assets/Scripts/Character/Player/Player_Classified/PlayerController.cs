using UnityEngine;

/// <summary>
/// Main player orchestrator.
/// Single responsibility: maintain references and coordinate the lifecycle (Awake/Update).
/// All specific logic lives in the specialized components.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerStaminaManager))]
[RequireComponent(typeof(PlayerKnockbackHandler))]
[RequireComponent(typeof(CombatAttackHandler))]
[RequireComponent(typeof(PlayerInteractor))]
[RequireComponent(typeof(PlayerDepthScaler))]
[RequireComponent(typeof(PlayerStateMachineController))]
public class PlayerController : MonoBehaviour
{
    [Header("Component References")]
    public CharacterController cc;

    public Animator animator;
    public Camera myCamera;
    public PlayerStaminaManager staminaManager;

    [HideInInspector] public PlayerInputHandler inputHandler;
    [HideInInspector] public PlayerMovement movement;
    [HideInInspector] public PlayerCombat combat;
    [HideInInspector] public PlayerKnockbackHandler knockbackHandler;
    [HideInInspector] public PlayerInteractor interactor;
    [HideInInspector] public PlayerDepthScaler depthScaler;
    [HideInInspector] public PlayerStateMachineController stateMachineController;
    [HideInInspector] public CombatAttackHandler combatAttackHandler;

    public State currentState =>
        stateMachineController != null
            ? stateMachineController.CurrentState
            : State.Idle;

    public bool canMove = true;
    public bool isGrounded;
    public bool isPunching;
    public bool isInRun;
    public bool canAttack = true;

    [Header("Ring System")]
    public bool isInsideRing = false;

    // TODO: reconnect with RingSlopeHandler when slope system is implemented
    public bool isOnSlope => false;
    public Transform ringCenter => null;

    public void EnterSlopeZone(Transform center)
    {
    }

    public void ExitSlopeZone()
    {
    }

    public Vector3 RestrictToSlopeDirection(Vector3 inputDirection) => inputDirection;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        staminaManager = GetComponent<PlayerStaminaManager>();
        inputHandler = GetComponent<PlayerInputHandler>();
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        knockbackHandler = GetComponent<PlayerKnockbackHandler>();
        interactor = GetComponent<PlayerInteractor>();
        depthScaler = GetComponent<PlayerDepthScaler>();
        stateMachineController = GetComponent<PlayerStateMachineController>();
        combatAttackHandler = GetComponent<CombatAttackHandler>();

        combat.Initialize(this);
        stateMachineController.Initialize(this);
    }

    void Update()
    {
        depthScaler.UpdateScaleBasedOnZ();
        isGrounded = cc.isGrounded;

        if (combat.HP > 0)
        {
            knockbackHandler.HandleKnockback();
            combat.UpdatePunchCooldown();
            stateMachineController.HandleStateTransitions();
            stateMachineController.UpdateCurrentState();
            movement.ApplyGravity();
            movement.SpeedManagement();
        }

        inputHandler.ConsumeFrameInputs();
    }

    public void ResetToIdle()
    {
        movement.ResetMotion();
        knockbackHandler.CancelKnockback();
        combatAttackHandler.CancelPreparedAttack();
        combat.ClearEnduranceOverride();
        combat.normalPunchTimer = 0f;
        combat.damageBoost = 0;
        canMove = true;
        canAttack = true;
        stateMachineController.TransitionTo(State.Idle, forceReenter: true);
    }

    // Called by DeenergizedState after its hardcoded exit duration.
    // It also remains compatible with a future Animation Event.
    public void OnDeenergizedExitDone()
    {
        if (currentState != State.Deenergized)
            return;

        canMove = true;
        stateMachineController.TransitionTo(State.Idle);
    }
}
