using System.Collections;
using UnityEngine;

public class DeenergizedState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;
    private Coroutine _staminaCoroutine;

    private int savedEndurance;
    private bool _exitTriggered;

    public DeenergizedState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        _exitTriggered = false;

        savedEndurance = player.Combat.endurance;
        player.Combat.endurance = -2;

        player.Movement.RefreshSpriteFlip();
        player.animator.SetBool("isDeenergized", true);

        stamina.StopAllRegenDrain();
        float length = player.animator.GetCurrentAnimatorStateInfo(0).length;
        player.StartCoroutine(StartStaminaDrop(length));
    }

    public void Update()
    {
        var movement = player.Movement;

        Vector3 input = movement.GetDirectionalInput();
        Vector3 toMove = movement.ApplyInertia(input, Time.deltaTime, movement.tiredTurnSpeed);

        if (toMove != Vector3.zero && controller.enabled)
            controller.Move(toMove * movement.tiredSpeed * Time.deltaTime);

        // Cuando la stamina se recuperó, dispara la animación de salida y bloquea
        // la state machine hasta que el Animation Event llame a OnDeenergizedExitDone()
        if (!_exitTriggered && !stamina.isTired)
        {
            _exitTriggered = true;
            player.animator.SetBool("isDeenergized", false);
            player.canMove = false;
        }
    }

    public void Exit()
    {
        player.Combat.endurance = savedEndurance;
        player.animator.SetBool("isDeenergized", false);
        if (_staminaCoroutine != null)
        {
            player.StopCoroutine(_staminaCoroutine);
            _staminaCoroutine = null;
        }
    }

    IEnumerator StartStaminaDrop(float length)
    {
        yield return new WaitForSeconds(length * 2f);
        stamina.SetTired();
    }
}
