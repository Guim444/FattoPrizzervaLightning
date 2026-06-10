using UnityEngine;

public class IdleState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;

    public IdleState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        stamina.SetIdle();
        player.animator.speed = 1f;
        player.movement.EnforceInvertedSpriteFlip();
    }

    public void Update()
    {
        var movement = player.movement;

        movement.EnforceInvertedSpriteFlip();

        // Hereda la inercia del movimiento anterior y decelera hasta cero
        float currentVelocity = movement.CurrentSpeed.magnitude * movement.walkingSpeed;
        float turnSpeed = Mathf.Lerp(2f, 10f, currentVelocity / movement.walkingSpeed);
        Vector3 inertia = movement.ApplyInertia(Vector3.zero, Time.deltaTime, turnSpeed);

        if (inertia != Vector3.zero && controller.enabled)
            controller.Move(inertia * movement.walkingSpeed * Time.deltaTime);
    }


    public void Exit()
    {
        player.movement.CurrentSpeed = Vector3.zero;
    }
}
