using System.Collections;
using UnityEngine;

public class TiredState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;

    private int savedEndurance;

    public TiredState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        // Fuerza al jugador a ser empujado con la máxima facilidad mientras está cansado
        savedEndurance           = player.combat.endurance;
        player.combat.endurance  = -2;

        stamina.StopAllRegenDrain();
        float length = player.animator.GetCurrentAnimatorStateInfo(0).length;
        player.StartCoroutine(StartStaminaDrop(length));
    }

    public void Update()
    {
        var movement = player.movement;

        Vector3 input  = movement.GetDirectionalInput();
        Vector3 toMove = movement.ApplyInertia(input, Time.deltaTime, movement.tiredTurnSpeed);

        if (toMove != Vector3.zero && controller.enabled)
            controller.Move(toMove * movement.tiredSpeed * Time.deltaTime);
    }

    public void Exit()
    {
        player.combat.endurance = savedEndurance;
    }

    IEnumerator StartStaminaDrop(float length)
    {
        yield return new WaitForSeconds(length * 2f);
        stamina.SetTired();
    }
}
