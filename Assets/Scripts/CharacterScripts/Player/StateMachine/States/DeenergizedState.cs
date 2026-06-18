using System.Collections;
using UnityEngine;

public class DeenergizedState : IStateActions
{
    // EnterDeenergized lasts 1.0416666 s. The design waits for two cycles
    // before stamina recovery starts; keep this stable during transitions.
    private const float StaminaDropDelay = 2.0833332f;

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

        savedEndurance = player.combat.endurance;
        player.combat.endurance = -2;

        player.movement.RefreshSpriteFlip();
        player.animator.SetBool("isDeenergized", true);

        stamina.StopAllRegenDrain();
        _staminaCoroutine = player.StartCoroutine(StartStaminaDrop());
    }

    public void Update()
    {
        var movement = player.movement;

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
        player.combat.endurance = savedEndurance;
        player.animator.SetBool("isDeenergized", false);
        if (_staminaCoroutine != null)
        {
            player.StopCoroutine(_staminaCoroutine);
            _staminaCoroutine = null;
        }
    }

    IEnumerator StartStaminaDrop()
    {
        yield return new WaitForSeconds(StaminaDropDelay);
        stamina.SetTired();
    }
}
