using UnityEngine;

public class MovingState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;

    public MovingState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        stamina.SetWalking();
    }

    public void Update()
    {
        var movement = player.movement;

        Vector3 input  = movement.GetDirectionalInput();
        Vector3 toMove = movement.ApplyInertia(input, Time.deltaTime, movement.walkingTurnSpeed);

        if (toMove != Vector3.zero && controller.enabled)
            controller.Move(toMove * movement.walkingSpeed * Time.deltaTime);
    }

    public void Exit() { }
}
