using System.Collections;
using UnityEngine;

public class PunchRunningState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;
    public BoxCollider punchCollider;

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

        var combat   = player.combat;
        var movement = player.movement;

        savedEndurance = combat.endurance;
        actualSpeed    = movement.punchRunningBaseSpeed;

        // El coste de stamina y el boost de endurance escalan con damageBoost (fase de thrust)
        switch (combat.damageBoost)
        {
            case 0:
                staminaCost       = combat.punchRunningStaminaCostPhase1;
                combat.endurance += 1;
                break;
            case 1:
                staminaCost       = combat.punchRunningStaminaCostPhase2;
                combat.endurance += 2;
                break;
            case 2:
                staminaCost       = combat.punchRunningStaminaCostPhase3;
                combat.endurance += 3;
                break;
        }

        if (player.staminaManager.currentStamina - staminaCost >= 0)
        {
            stamina.ModifyStamina(-staminaCost);
            combat.normalPunchTimer = player.animator
                .GetCurrentAnimatorStateInfo(0).length;
            CalcSpeed(combat.damageBoost, movement.punchRunningBaseSpeed);
            player.StartCoroutine(Punch(combat.normalPunchTimer));
        }
    }

    public void Update()
    {
        var movement = player.movement;

        // Mantiene la dirección previa mientras dura el golpe
        Vector3 input  = movement.LastDirection;
        Vector3 toMove = movement.ApplyInertia(input, Time.deltaTime, movement.punchRunningTurnSpeed);
        toMove.y = 0;

        if (controller.enabled)
            controller.Move(new Vector3(toMove.x, 0, SprintZ(movement)) * actualSpeed * Time.deltaTime);
    }

    float SprintZ(PlayerMovement movement)
    {
        // Mantiene el momentum en Z durante el punch-run
        return movement.LastDirection.z;
    }

    void CalcSpeed(int damageBoost, float baseSpeed)
    {
        switch (damageBoost)
        {
            case 0: actualSpeed = baseSpeed * 1.2f; break;
            case 1: actualSpeed = baseSpeed * 1.4f; break;
            case 2: actualSpeed = baseSpeed * 1.8f; break;
        }
    }

    IEnumerator Punch(float duration)
    {
        yield return new WaitForSeconds(20 * duration / 30f);

        if (player.canAttack)
        {
            if (punchCollider != null) punchCollider.enabled = true;

            yield return new WaitForSeconds(24 * duration / 30f);

            if (punchCollider != null) punchCollider.enabled = false;
            player.canAttack = false;
        }
    }

    public void Exit()
    {
        if (punchCollider != null) punchCollider.enabled = false;
        player.combat.endurance   = savedEndurance;
        player.canAttack           = true;
        player.combat.damageBoost  = 0;
        punchExecuted              = false;
    }
}
