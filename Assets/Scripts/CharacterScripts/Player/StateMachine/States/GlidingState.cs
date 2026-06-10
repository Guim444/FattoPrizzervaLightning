using UnityEngine;

public class GlidingState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;

    public GlidingState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        stamina.SetWalking();
    }

    public void Update()
    {
        if (player == null || controller == null) return;

        var movement = player.Movement;

        Vector3 input  = movement.GetDirectionalInput();
        Vector3 toMove = movement.ApplyInertia(input, Time.deltaTime, movement.glidingTurnSpeed);

        // TODO: RestrictToSlopeDirection cuando se implemente el slope system
        toMove = player.RestrictToSlopeDirection(toMove);

        if (toMove != Vector3.zero && controller.enabled)
            controller.Move(toMove * movement.glidingSpeed * Time.deltaTime);
    }

    public void Exit() { }
}
