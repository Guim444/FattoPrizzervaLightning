using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// State machine owned by one player instance.
/// It stores both the active state key and its behaviour so they cannot diverge.
/// </summary>
public sealed class StateMachine
{
    private readonly Dictionary<State, IStateActions> _states =
        new Dictionary<State, IStateActions>();

    private IStateActions _currentStateActions;

    public State CurrentState { get; private set; } = State.Idle;
    public bool HasCurrentState { get; private set; }

    public void Reset()
    {
        _states.Clear();
        _currentStateActions = null;
        CurrentState = State.Idle;
        HasCurrentState = false;
    }

    public void AddState(State key, IStateActions state)
    {
        _states[key] = state;
    }

    /// <summary>
    /// Changes state and returns true only when Enter/Exit were executed.
    /// forceReenter is reserved for full gameplay resets.
    /// </summary>
    public bool SetState(State newState, bool forceReenter = false)
    {
        if (!_states.TryGetValue(newState, out IStateActions nextState))
        {
            Debug.LogError($"Player state '{newState}' is not registered.");
            return false;
        }

        if (!forceReenter && HasCurrentState && CurrentState == newState)
            return false;

        _currentStateActions?.Exit();
        _currentStateActions = nextState;
        CurrentState = newState;
        HasCurrentState = true;
        _currentStateActions.Enter();
        return true;
    }

    public void Update()
    {
        _currentStateActions?.Update();
    }
}
