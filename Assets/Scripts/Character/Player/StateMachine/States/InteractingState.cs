using UnityEngine;

public class InteractingState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;

    public void Enter()  { }
    public void Update() { }
    public void Exit()   { }
}
