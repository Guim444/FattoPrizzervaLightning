using UnityEngine;

/// <summary>
/// Running state with 3 thrust phases.
///
/// No conflict with the new combat system:
/// its only responsibility is to manage acceleration and expose
/// combat.damageBoost = currentThrustPhase - 1 so PunchRunningState
/// and CombatAttackHandler read the correct phase when executing the hit.
/// </summary>
public class RunningState : IStateActions
{
    public PlayerController      player;
    public CharacterController   controller;
    public PlayerStaminaManager  stamina;

    public float baseSpeed;
    public float actualSpeed;
    public float staminaCostPerSecond;

    private int _currentThrustPhase = 1;

    public float[] topSpeed            = { 9f,       11f,      13.5f    };
    public float[] currentAcceleration = { 0.0035f,  0.0045f,  0.006f  };

    public RunningState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        _currentThrustPhase   = 1;
        player.animator.speed = 1;
        actualSpeed           = baseSpeed;
        stamina.SetRunning(staminaCostPerSecond);
    }

    public void Update()
    {
        var   movement = player.movement;
        var   combat   = player.combat;
        float dt       = Time.deltaTime;

        Vector3 input = movement.GetDirectionalInput();
        HandleTurn(input, movement, combat);

        float inertiaTurnSpeed = _currentThrustPhase == 3
            ? movement.runningTurnSpeedPhase3
            : movement.runningTurnSpeedNormal;

        Vector3 toMove = movement.ApplyInertia(input, dt, inertiaTurnSpeed);
        toMove.y = 0;

        if (controller.enabled && toMove != Vector3.zero)
        {
            float moveSpeed = actualSpeed + (_currentThrustPhase - 1);
            controller.Move(toMove * moveSpeed * dt);
        }

        // Fuente de verdad para la fase de thrust: PunchRunningState la lee en Enter()
        combat.damageBoost = _currentThrustPhase - 1;
    }

    public void Exit()
    {
        _currentThrustPhase = 1;
    }

    // --------------------------------------------------------
    //  PRIVATE
    // --------------------------------------------------------

    void HandleTurn(Vector3 input, PlayerMovement movement, PlayerCombat combat)
    {
        Vector3 previousDir = movement.LastDirection;
        bool sharpTurn = previousDir.magnitude > 0.01f
                         && Vector3.Angle(previousDir, input) > 90f;

        if (sharpTurn)
        {
            _currentThrustPhase   = 1;
            combat.damageBoost    = 0;
            player.animator.speed = 1;
        }
        else
        {
            HandleSpeed();
        }
    }

    void HandleSpeed()
    {
        var stateMachine = player.stateMachineController;

        if (actualSpeed < topSpeed[_currentThrustPhase - 1])
        {
            actualSpeed += currentAcceleration[_currentThrustPhase - 1];
        }
        else if (_currentThrustPhase < 3)
        {
            _currentThrustPhase++;
            staminaCostPerSecond = stateMachine.runningStaminaCost[_currentThrustPhase - 1];
            stamina.SetRunning(staminaCostPerSecond);
        }
    }
}