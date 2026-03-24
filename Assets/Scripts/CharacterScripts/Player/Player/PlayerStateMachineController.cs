using UnityEngine;

/// <summary>
/// Responsabilidad única: construir la máquina de estados y gestionar sus transiciones.
/// La lógica de qué estado corresponde a cada situación la encapsula PlayerStateHelper,
/// este componente solo coordina cuándo y cómo se producen los cambios de estado.
/// </summary>
public class PlayerStateMachineController : MonoBehaviour
{
    // ==================================================
    // STAMINA COSTS (Running)
    // Viven aquí porque se pasan a los estados en Initialize()
    // ==================================================
    [Header("Game Design: Stamina Costs (Running)")]
    public System.Collections.Generic.List<float> runningStaminaCost =
        new System.Collections.Generic.List<float> { 10, 14, 18 };

    // ==================================================
    // PRIVATE REFS
    // ==================================================
    private PlayerController _player;

    // ==================================================
    // INITIALIZATION
    // ==================================================

    /// <summary>
    /// Construye y registra todos los estados en la StateMachine.
    /// Debe llamarse desde PlayerController.Awake() después de resolver componentes.
    /// </summary>
    public void Initialize(PlayerController player)
    {
        _player = player;
        InitializeStateMachine();
    }

    private void InitializeStateMachine()
    {
        var sm       = _player.staminaManager;
        var cc       = _player.cc;
        var p        = _player;

        var idle = new IdleState(sm)          { player = p, controller = cc };
        var moving = new MovingState(sm)      { player = p, controller = cc };
        var running = new RunningState(sm)    { player = p, controller = cc };
        var tired = new TiredState(sm)        { player = p, controller = cc };

        var punching = new PunchingState(sm)
        {
            player        = p,
            controller    = cc,
            punchCollider = p.GetComponent<SphereCollider>()
        };

        var punchRunning = new PunchRunningState(sm)
        {
            player        = p,
            controller    = cc,
            punchCollider = p.GetComponent<BoxCollider>()
        };

        var knockedback  = new KnockedbackState() { player = p, controller = cc };
        var gliding      = new GlidingState(sm)   { player = p, controller = cc };
        var jumping      = new JumpingState();
        var falling      = new FallingState();
        var interacting  = new InteractingState();

        StateMachine.AddState(State.Idle,         idle);
        StateMachine.AddState(State.Moving,       moving);
        StateMachine.AddState(State.Running,      running);
        StateMachine.AddState(State.Tired,        tired);
        StateMachine.AddState(State.Punching,     punching);
        StateMachine.AddState(State.PunchRunning, punchRunning);
        StateMachine.AddState(State.Knockedback,  knockedback);
        StateMachine.AddState(State.Gliding,      gliding);
        StateMachine.AddState(State.Jumping,      jumping);
        StateMachine.AddState(State.Falling,      falling);
        StateMachine.AddState(State.Interacting,  interacting);

        StateMachine.SetState(State.Idle);
    }

    // ==================================================
    // FRAME UPDATE
    // ==================================================

    /// <summary>
    /// Evalúa si el estado actual debe cambiar y aplica la transición si es necesario.
    /// Debe llamarse cada frame desde PlayerController.Update().
    /// </summary>
    public void HandleStateTransitions()
    {
        if (_player.combat.normalPunchTimer > 0) return;
        if (!_player.canMove) return;

        State newState = PlayerStateHelper.DetermineState(
            _player,
            _player.staminaManager,
            _player.isOnSlope,
            _player.currentState);

        if (newState == _player.currentState) return;

        _player.currentState = newState;
        StateMachine.SetState(_player.currentState);

        // El damageBoost solo persiste en estados de carrera
        if (_player.currentState != State.Running &&
            _player.currentState != State.PunchRunning)
        {
            _player.combat.damageBoost = 0;
        }
    }
}
