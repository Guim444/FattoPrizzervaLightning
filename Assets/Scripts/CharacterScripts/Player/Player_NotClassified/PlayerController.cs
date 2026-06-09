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
    public PlayerInputHandler InputHandler {  get; private set; }
    public PlayerMovement Movement {  get; private set; }
    public PlayerCombat Combat {  get; private set; }
    public PlayerKnockbackHandler KnockbackHandler {  get; private set; }
    public PlayerInteractor Interactor {  get; private set; }
    public PlayerDepthScaler DepthScaler {  get; private set; }
    public PlayerStateMachineController StateMachineController {  get; private set; }
    public CombatAttackHandler CombatAttackHandler {  get; private set; }

    // ==================================================
    // SHARED STATE (leído por múltiples componentes)
    // ==================================================
    //internal State currentState = State.Idle;
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
        
        InputHandler = GetComponent<PlayerInputHandler>();
        Movement = GetComponent<PlayerMovement>();
        Combat = GetComponent<PlayerCombat>();
        KnockbackHandler = GetComponent<PlayerKnockbackHandler>();
        Interactor = GetComponent<PlayerInteractor>();
        DepthScaler = GetComponent<PlayerDepthScaler>();
        StateMachineController = GetComponent<PlayerStateMachineController>();
        CombatAttackHandler = GetComponent<CombatAttackHandler>();

        Combat.Initialize(this);
        StateMachineController.Initialize(this);
    }

    // ==================================================
    // UPDATE LOOP
    // ==================================================

    void Update()
    {
        DepthScaler.UpdateScaleBasedOnZ();
        isGrounded = cc.isGrounded;

        if (Combat.HP > 0)
        {
            KnockbackHandler.HandleKnockback();
            Combat.UpdatePunchCooldown();
            StateMachineController.HandleStateTransitions();
            StateMachineController._stateMachine.Update();
            Movement.ApplyGravity();
            Movement.SpeedManagement();
        }

        InputHandler.ConsumeFrameInputs();
    }

    // Llamado por Animation Event al final de ExitDeenergized
    public void OnDeenergizedExitDone()
    {
        canMove = true;
    }
}
