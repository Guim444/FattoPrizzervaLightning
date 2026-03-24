using System.Collections;
using UnityEngine;

public class PunchingState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;
    public SphereCollider punchCollider;

    private bool punchExecuted = false;

    public PunchingState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        if (punchExecuted) return;
        punchExecuted = true;

        var combat   = player.combat;
        var movement = player.movement;

        stamina.ModifyStamina(-combat.punchStaminaCostPhase0);

        combat.normalPunchTimer = player.animator
            .GetCurrentAnimatorStateInfo(0).length;

        player.StartCoroutine(Punch(combat.normalPunchTimer));
        combat.playerDamage = combat.basicPunchDamage;
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

    IEnumerator Punch(float duration)
    {
        // Activa el collider aproximadamente a los 2/3 de la animación
        yield return new WaitForSeconds(20 * duration / 30f);

        if (player.canAttack)
        {
            player.combat.endurance += 1;
            if (punchCollider != null) punchCollider.enabled = true;

            yield return new WaitForSeconds(24 * duration / 30f);

            player.combat.endurance -= 1;
            if (punchCollider != null) punchCollider.enabled = false;
            player.canAttack = false;
        }
    }

    public void Exit()
    {
        if (punchCollider != null) punchCollider.enabled = false;
        player.combat.playerDamage = 0;
        player.canAttack           = true;
        punchExecuted              = false;
    }
}
