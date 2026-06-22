using System.Collections;
using UnityEngine;

public class PunchRunningState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;
    public BoxCollider punchCollider;

    // RunningPunch clip: 1.9583333 s. Impact event: 1.625 s.
    private const float PunchRunDuration = 1.9583333f;

    private float staminaCost;
    private float actualSpeed;
    private bool  punchExecuted = false;

    public PunchRunningState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        if (punchExecuted) return;
        punchExecuted = true;

        var combat   = player.combat;
        var movement = player.movement;

        int damageBoost = Mathf.Clamp(combat.damageBoost, 0, 2);
        actualSpeed = movement.punchRunningBaseSpeed;

        // El endurance sube temporalmente según la fase de carrera:
        // más damageBoost → golpe más poderoso (tabla KnockbackResolver)
        switch (damageBoost)
        {
            case 0: staminaCost = combat.punchRunningStaminaCostPhase1; break;
            case 1: staminaCost = combat.punchRunningStaminaCostPhase2; break;
            default: staminaCost = combat.punchRunningStaminaCostPhase3; break;
        }

        combat.SetEnduranceOverride(combat.endurance + damageBoost + 1);
        stamina.ModifyStamina(-staminaCost);
        combat.normalPunchTimer = PunchRunDuration;
        CalcSpeed(damageBoost, movement.punchRunningBaseSpeed);

        player.combatAttackHandler.PrepareAttack(AttackType.PunchRunning);
        player.animator.SetTrigger("isPunching");
    }

    public void Update()
    {
        var movement = player.movement;

        if (!player.inputHandler.IsRunInputHeld)
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
        if (player.canAttack)
            player.combatAttackHandler.CancelPreparedAttack();

        player.combat.ClearEnduranceOverride();
        player.canAttack          = true;
        player.combat.damageBoost = 0;
        punchExecuted             = false;
    }
}
