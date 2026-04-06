using UnityEngine;

/// <summary>
/// Orquestador principal del jugador.
/// Responsabilidad única: mantener referencias y coordinar el ciclo de vida (Awake/Update).
/// Toda la lógica específica vive en los componentes especializados.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(PlayerKnockbackHandler))]
[RequireComponent(typeof(PlayerInteractor))]
[RequireComponent(typeof(PlayerDepthScaler))]
[RequireComponent(typeof(PlayerStateMachineController))]
public class PlayerController : MonoBehaviour
{
    // ==================================================
    // COMPONENT REFERENCES
    // ==================================================
    [Header("Component References")]
    public CharacterController cc;

    public Animator animator;
    public Camera myCamera;
    public PlayerStaminaManager staminaManager;

    // ==================================================
    // SPECIALIZED COMPONENTS (auto-resolved in Awake)
    // ==================================================
    [HideInInspector] public PlayerInputHandler inputHandler;
    [HideInInspector] public PlayerMovement movement;
    [HideInInspector] public PlayerCombat combat;
    [HideInInspector] public PlayerKnockbackHandler knockbackHandler;
    [HideInInspector] public PlayerInteractor interactor;
    [HideInInspector] public PlayerDepthScaler depthScaler;
    [HideInInspector] public PlayerStateMachineController stateMachineController;
    [HideInInspector] public CombatAttackHandler combatAttackHandler;

    // ==================================================
    // SHARED STATE (leído por múltiples componentes)
    // ==================================================
    internal State currentState = State.Idle;
    public bool canMove = true;
    public bool isGrounded;
    public bool isPunching;
    public bool isInRun;
    public bool canAttack = true;

    // ==================================================
    // RING SYSTEM
    // ==================================================
    [Header("Ring System")]
    public bool isInsideRing = false;

    // TODO: reconectar con RingSlopeHandler cuando se implemente el slope system
    public bool isOnSlope => false;
    public Transform ringCenter => null;

    public void EnterSlopeZone(Transform center)
    {
    }

    public void ExitSlopeZone()
    {
    }

    public Vector3 RestrictToSlopeDirection(Vector3 inputDirection) => inputDirection;

    // ==================================================
    // INITIALIZATION
    // ==================================================

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

    // ==================================================
    // UPDATE LOOP
    // ==================================================

    void Update()
    {
        depthScaler.UpdateScaleBasedOnZ();
        isGrounded = cc.isGrounded;

        if (combat.HP > 0)
        {
            knockbackHandler.HandleKnockback();
            combat.UpdatePunchCooldown();
            stateMachineController.HandleStateTransitions();
            movement.SpeedManagement();
            StateMachine.Update();
            movement.ApplyGravity();
        }

        inputHandler.ConsumeFrameInputs();
    }
}
