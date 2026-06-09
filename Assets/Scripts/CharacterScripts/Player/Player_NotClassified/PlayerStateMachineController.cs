using UnityEngine;

/// <summary>
/// Responsabilidad única: construir la máquina de estados y gestionar sus transiciones.
/// </summary>
public class PlayerStateMachineController : MonoBehaviour
{
    [Header("Game Design: Stamina Costs (Running)")]
    public System.Collections.Generic.List<float> runningStaminaCost =
        new System.Collections.Generic.List<float> { 10, 14, 18 };

    private PlayerController _player;
    public PlayerIntentBuffer IntentBuffer { get; private set; }


    [field: SerializeField] public StateMachine _stateMachine { get; private set; }

    public State CurrentState => _stateMachine.CurrentKey;

    public void Initialize(PlayerController player)
    {
        _stateMachine = new StateMachine();
        IntentBuffer = new PlayerIntentBuffer();

        _player = player;
        InitializeStateMachine();
    }

    private void InitializeStateMachine()
    {
        _stateMachine.Reset();
        var sm = _player.staminaManager;
        var cc = _player.cc;
        var p = _player;

        // CombatAttackHandler se accede via player.combatAttackHandler en cada estado,
        // no hace falta pasarlo explícitamente.

        var idle = new IdleState(sm) { player = p, controller = cc };
        var moving = new MovingState(sm) { player = p, controller = cc };
        var running = new RunningState(sm)
        {
            player = p,
            controller = cc,
            baseSpeed = p.Movement.runningBaseSpeed,
            staminaCostPerSecond = runningStaminaCost[0]
        };
        var tired = new DeenergizedState(sm) { player = p, controller = cc };

        var punching = new PunchingState(sm)
        {
            player = p,
            controller = cc,
            // punchCollider = p.GetComponent<SphereCollider>()
        };

        var punchRunning = new PunchRunningState(sm)
        {
            player = p,
            controller = cc,
        };

        var knockedback = new KnockedbackState() { player = p, controller = cc };
        var gliding = new GlidingState(sm) { player = p, controller = cc };
        var jumping = new JumpingState();
        var falling = new FallingState();
        var interacting = new InteractingState();

        _stateMachine.AddState(State.Idle, idle);
        _stateMachine.AddState(State.Moving, moving);
        _stateMachine.AddState(State.Running, running);
        _stateMachine.AddState(State.Deenergized, tired);
        _stateMachine.AddState(State.Punching, punching);
        _stateMachine.AddState(State.PunchRunning, punchRunning);
        _stateMachine.AddState(State.Knockedback, knockedback);
        _stateMachine.AddState(State.Gliding, gliding);
        _stateMachine.AddState(State.Jumping, jumping);
        _stateMachine.AddState(State.Falling, falling);
        _stateMachine.AddState(State.Interacting, interacting);

        _stateMachine.SetState(State.Idle);
    }

    public void HandleStateTransitions()
    {
        if (!_player.canMove) return;

        var intents = IntentBuffer;

        if (intents.Has(PlayerStateRequest.Knockback))
        {
            _stateMachine.RequestState(State.Knockedback);
            intents.Clear();
            return;
        }

        if (intents.Has(PlayerStateRequest.Interact))
        {
            _stateMachine.RequestState(State.Interacting);
            intents.Clear();
            return;
        }

        if (intents.Has(PlayerStateRequest.Attack))
        {
            var attackState =
                _player.isInsideRing ? State.PunchRunning : State.Punching;

            _stateMachine.RequestState(attackState);
            intents.Clear();
            return;
        }

        State newState = PlayerStateHelper.DetermineState(
            _player,
            _player.staminaManager,
            _player.isOnSlope,
            _stateMachine.CurrentKey
        );

        _stateMachine.RequestState(newState);



        /*if (_player.combat.normalPunchTimer > 0) return;
        if (!_player.canMove) return;

        State newState = PlayerStateHelper.DetermineState(
            _player,
            _player.staminaManager,
            _player.isOnSlope,
            _player.currentState);

        if (newState == _player.currentState) return;

        //_player.currentState = newState;
        //_stateMachine.SetState(_player.currentState);

        if (_player.currentState != State.Running &&
            _player.currentState != State.PunchRunning)
        {
            _player.combat.damageBoost = 0;
        }
        */
    }

    public string GetDebugStateInfo()
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"STATE: {_stateMachine.CurrentKey}");

        sb.AppendLine($"INTENTS: {IntentBuffer.Current}");

        return sb.ToString();
    }
}
