using UnityEngine;

/// <summary>
/// Single responsibility: build the state machine and manage its transitions.
/// </summary>
public class PlayerStateMachineController : MonoBehaviour
{
    [Header("Game Design: Stamina Costs (Running)")]
    public System.Collections.Generic.List<float> runningStaminaCost =
        new System.Collections.Generic.List<float> { 10, 14, 18 };

    private PlayerController _player;
    private StateMachine _stateMachine;

    public State CurrentState =>
        _stateMachine != null && _stateMachine.HasCurrentState
            ? _stateMachine.CurrentState
            : State.Idle;

    public void Initialize(PlayerController player)
    {
        _player = player;
        _stateMachine = new StateMachine();
        InitializeStateMachine();
    }

    private void InitializeStateMachine()
    {
        _stateMachine.Reset();
        var sm = _player.staminaManager;
        var cc = _player.cc;
        var p = _player;

        // CombatAttackHandler is accessed via player.combatAttackHandler in each state,
        // no need to pass it explicitly.

        var idle = new IdleState(sm) { player = p, controller = cc };
        var moving = new MovingState(sm) { player = p, controller = cc };
        var running = new RunningState(sm)
        {
            player = p,
            controller = cc,
            baseSpeed = p.movement.runningBaseSpeed,
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
        var jumping = new JumpingState() { player = p, controller = cc };
        var falling = new FallingState() { player = p, controller = cc };
        var interacting = new InteractingState() { player = p, controller = cc };

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

        TransitionTo(State.Idle);
    }

    public void HandleStateTransitions()
    {
        // Deenergized owns its recovery and exit sequence.
        if (CurrentState == State.Deenergized) return;
        if (_player.combat.normalPunchTimer > 0) return;
        if (!_player.canMove) return;

        State newState = PlayerStateHelper.DetermineState(
            _player,
            _player.staminaManager,
            _player.isOnSlope,
            CurrentState);

        TransitionTo(newState);
    }

    /// <summary>
    /// Single entry point for every player state transition.
    /// </summary>
    public bool TransitionTo(State newState, bool forceReenter = false)
    {
        if (_stateMachine == null)
        {
            Debug.LogError("PlayerStateMachineController has not been initialized.", this);
            return false;
        }

        bool changed = _stateMachine.SetState(newState, forceReenter);

        if (changed &&
            newState != State.Running &&
            newState != State.PunchRunning)
        {
            _player.combat.damageBoost = 0;
        }

        return changed;
    }

    public void UpdateCurrentState()
    {
        _stateMachine?.Update();
    }
}
