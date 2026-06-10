using System.Collections;
using UnityEngine;

public class PunchRunningState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;
    public BoxCollider punchCollider;

    // Duración total del estado (hardcodeada, no .length — ver convenciones)
    private const float PunchRunDuration = 1.2f;

    private float staminaCost;
    private float actualSpeed;
    private bool  punchExecuted = false;
    private int   savedEndurance;

    public PunchRunningState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        if (punchExecuted) return;
        punchExecuted = true;

<<<<<<< HEAD
=======
        player.Movement.EnforceInvertedSpriteFlip();
>>>>>>> origin/Albert_Branch
        player.animator.SetTrigger("isPunching");

        var combat   = player.Combat;
        var movement = player.Movement;

        savedEndurance = combat.endurance;
        actualSpeed    = movement.punchRunningBaseSpeed;

        // El endurance sube temporalmente según la fase de carrera:
        // más damageBoost → golpe más poderoso (tabla KnockbackResolver)
        switch (combat.damageBoost)
        {
            case 0: staminaCost = combat.punchRunningStaminaCostPhase1; combat.endurance += 1; break;
            case 1: staminaCost = combat.punchRunningStaminaCostPhase2; combat.endurance += 2; break;
            case 2: staminaCost = combat.punchRunningStaminaCostPhase3; combat.endurance += 3; break;
        }

        if (player.staminaManager.currentStamina - staminaCost >= 0)
        {
            stamina.ModifyStamina(-staminaCost);
            combat.normalPunchTimer = PunchRunDuration;
            CalcSpeed(combat.damageBoost, movement.punchRunningBaseSpeed);
        }
    }

    public void Update()
    {
        var movement = player.Movement;

        if (!player.InputHandler.IsRunInputHeld)
        {
            if (controller.enabled)
                controller.Move(Vector3.zero);
            return;
        }

        Vector3 input  = movement.LastDirection;
        Vector3 toMove = movement.ApplyInertia(input, Time.deltaTime, movement.punchRunningTurnSpeed);
        toMove.y = 0;

        if (controller.enabled)
            controller.Move(new Vector3(toMove.x, 0, movement.LastDirection.z) * actualSpeed * Time.deltaTime);
    }

    private void CalcSpeed(int damageBoost, float baseSpeed)
    {
        switch (damageBoost)
        {
            case 0: actualSpeed = baseSpeed * 1.2f; break;
            case 1: actualSpeed = baseSpeed * 1.4f; break;
            case 2: actualSpeed = baseSpeed * 1.8f; break;
        }
    }

    public void Exit()
    {
        player.Combat.endurance   = savedEndurance; // Restaura el endurance temporal
        player.canAttack          = true;
        player.Combat.damageBoost = 0;
        punchExecuted             = false;
    }
}