using System.Collections;
using UnityEngine;

public class PunchingState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;
    public SphereCollider punchCollider;

    // Duración total del estado (hardcodeada, no .length — ver convenciones)
    private const float PunchDuration = 1.0f;

    private bool punchExecuted = false;

    public PunchingState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        if (punchExecuted) return;
        punchExecuted = true;

        player.animator.SetTrigger("isPunching");

        var combat = player.combat;
        stamina.ModifyStamina(-combat.punchStaminaCostPhase0);
        combat.normalPunchTimer = PunchDuration;
    }

    public void Update()
    {
        var movement = player.movement;
        movement.RefreshSpriteFlip();

        bool isMoving = movement.GetDirectionalInput().magnitude > 0.1f;

        if (isMoving)
        {
            stamina.SetWalking();
            Vector3 input = movement.GetDirectionalInput();
            if (controller.enabled)
                controller.Move(input * movement.punchingSpeed * Time.deltaTime);
        }
        else
        {
            stamina.SetIdle();
            if (controller.enabled)
                controller.Move(Vector3.zero);
        }
    }

    public void Exit()
    {
        player.canAttack = true;
        punchExecuted = false;
    }
}
