using System.Collections.Generic;
using UnityEngine;

public class StateMachine
{
    private readonly Dictionary<State, IStateActions> _states = new();
    public IStateActions CurrentState { get; private set; }
    public State CurrentKey { get; private set; }


    public void Reset()
    {
        _states.Clear();
        CurrentState = null;
    }
    public void AddState(State key, IStateActions state)
    {
        _states[key] = state;
    }

    public void RequestState(State newState)
    {
        if (!CanTransition(newState))
            return;

        SetState(newState);
    }

    public void SetState(State newState)
    {
        if (!_states.ContainsKey(newState))
        {
            Debug.LogWarning($"State {newState} not registered");
            return;
        }

        if (CurrentState == _states[newState]) 
            return;

        CurrentState?.Exit();
       
        CurrentState = _states[newState];
        CurrentKey = newState;

        CurrentState.Enter();
    }

    public void Update()
    {
        CurrentState?.Update();
    }

    public bool CanTransition(State newState)
    {
        if (!_states.ContainsKey(newState))
            return false;

        if (CurrentState == _states[newState])
            return false;

        return true;
    }
}
