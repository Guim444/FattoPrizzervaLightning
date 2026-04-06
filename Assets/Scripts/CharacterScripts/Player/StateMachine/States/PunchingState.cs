using System.Collections;
using UnityEngine;

public class PunchingState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;
    public SphereCollider punchCollider;
    float punchDuration = 1.5f;
    private bool punchExecuted = false;

    public PunchingState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    // PunchingState.cs — Enter() completo (el resto queda igual)
    public void Enter()
    {
        if (punchExecuted) return;
        punchExecuted = true;
           player.animator.SetTrigger("isPunching");

        var combat = player.combat;
        stamina.ModifyStamina(-combat.punchStaminaCostPhase0);
        combat.normalPunchTimer = punchDuration;
    }

    public void Update()
    {
        var movement = player.movement;
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
