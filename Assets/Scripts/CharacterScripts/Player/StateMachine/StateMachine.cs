using System.Collections.Generic;
using UnityEngine;

public static class StateMachine
{
    private static Dictionary<State, IStateActions> _states = new Dictionary<State, IStateActions>();
    private static IStateActions _currentState;
    public static void Reset()
    {
        _states.Clear();
        _currentState = null;
    }
    public static void AddState(State key, IStateActions state)
    {
        _states[key] = state;
    }

    public static void SetState(State newState)
    {
        _currentState?.Exit();
        _currentState = _states[newState];
        _currentState.Enter();
    }

    public static void Update()
    {
        _currentState?.Update();
    }
}
