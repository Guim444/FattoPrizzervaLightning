using UnityEngine;

public class DeenergizedState : IStateActions
{
    // EnterDeenergized lasts 1.0416666 s. Preserve the current design delay
    // of two cycles before recovery starts.
    private const float RecoveryDelay = 2.0833332f;

    // IdleDeenergized -> ExitDeenergized blend (0.25 s) + exit clip (0.9166667 s).
    private const float ExitDuration = 1.1666667f;

    public PlayerController player;
    public CharacterController controller;
    public PlayerStaminaManager stamina;

    private float _recoveryDelayRemaining;
    private float _exitTimeRemaining;
    private bool _recoveryStarted;
    private bool _isExiting;

    public DeenergizedState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        _recoveryDelayRemaining = RecoveryDelay;
        _exitTimeRemaining = 0f;
        _recoveryStarted = false;
        _isExiting = false;

        player.combat.SetEnduranceOverride(-2);
        player.movement.RefreshSpriteFlip();
        player.animator.SetBool("isDeenergized", true);
        stamina.StopAllRegenDrain();
    }

    public void Update()
    {
        if (_isExiting)
        {
            UpdateExit();
            return;
        }

        var movement = player.movement;

        Vector3 input = movement.GetDirectionalInput();
        Vector3 toMove = movement.ApplyInertia(input, Time.deltaTime, movement.tiredTurnSpeed);

        if (toMove != Vector3.zero && controller.enabled)
            controller.Move(toMove * movement.tiredSpeed * Time.deltaTime);

        if (!_recoveryStarted)
        {
            _recoveryDelayRemaining -= Time.deltaTime;

            if (_recoveryDelayRemaining <= 0f)
            {
                _recoveryStarted = true;
                stamina.SetTired();
            }
        }

        if (_recoveryStarted && !stamina.isTired)
            BeginExit();
    }

    public void Exit()
    {
        player.combat.ClearEnduranceOverride();
        player.animator.SetBool("isDeenergized", false);
        _isExiting = false;
    }

    private void BeginExit()
    {
        _isExiting = true;
        _exitTimeRemaining = ExitDuration;
        player.canMove = false;
        player.movement.LastDirection = Vector3.zero;
        player.movement.CurrentSpeed = Vector3.zero;
        player.animator.SetBool("isDeenergized", false);
    }

    private void UpdateExit()
    {
        _exitTimeRemaining -= Time.deltaTime;

        if (_exitTimeRemaining > 0f)
            return;

        _exitTimeRemaining = float.PositiveInfinity;
        player.OnDeenergizedExitDone();
    }
}
