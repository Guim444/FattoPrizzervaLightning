using UnityEngine;

/// <summary>
/// Running state with 3 thrust phases.
///
/// 2.5D change: removed CameraFollow.instance reference (camera system is different).
/// Everything else works as-is porque GetDirectionalInput() retorna X/Z correctamente.
/// </summary>
public class RunningState : IStateActions
{
    public PlayerController player;          // corregido: era PlayerStateMachineController
    public CharacterController controller;
    public PlayerStaminaManager stamina;

    public float baseSpeed, actualSpeed;
    public float staminaCostPerSecond;

    private int currentThrustPhase = 1;

    public float[] topSpeed            = { 9f, 11f, 13.5f };
    public float[] currentAcceleration = { 0.0035f, 0.0045f, 0.006f };

    public RunningState(PlayerStaminaManager staminaManager)
    {
        stamina = staminaManager;
    }

    public void Enter()
    {
        currentThrustPhase = 1;

        // 2.5D: CameraFollow reference removed. CameraMovement gestiona
        // su propio smoothing via el parámetro smoothTime del Inspector.

        player.animator.speed = 1;
        actualSpeed           = baseSpeed;
        stamina.SetRunning(staminaCostPerSecond);
    }

    public void Update()
    {
        var movement = player.movement;
        var combat   = player.combat;
        float dt     = Time.deltaTime;

        Vector3 input = movement.GetDirectionalInput();
        HandleTurn(input, movement, combat);

        float inertiaTurnSpeed = currentThrustPhase == 3
            ? movement.runningTurnSpeedPhase3
            : movement.runningTurnSpeedNormal;

        Vector3 toMove = movement.ApplyInertia(input, dt, inertiaTurnSpeed);
        toMove.y = 0;

        if (controller.enabled && toMove != Vector3.zero)
        {
            float moveSpeed = actualSpeed + (currentThrustPhase - 1);
            controller.Move(toMove * moveSpeed * dt);
        }

        // Expone la fase de thrust a PlayerCombat para cálculos de daño
        combat.damageBoost = currentThrustPhase - 1;
    }

    public void Exit()
    {
        currentThrustPhase = 1;
    }

    // ==================================================
    // PRIVATE – THRUST LOGIC
    // ==================================================

    void HandleTurn(Vector3 input, PlayerMovement movement, PlayerCombat combat)
    {
        Vector3 previousDir = movement.LastDirection;
        bool sharpTurn = previousDir.magnitude > 0.01f
                         && Vector3.Angle(previousDir, input) > 90f;

        if (sharpTurn)
        {
            currentThrustPhase    = 1;
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

        if (actualSpeed < topSpeed[currentThrustPhase - 1])
        {
            actualSpeed += currentAcceleration[currentThrustPhase - 1];
        }
        else if (currentThrustPhase < 3)
        {
            currentThrustPhase++;
            staminaCostPerSecond = stateMachine.runningStaminaCost[currentThrustPhase - 1];
            stamina.SetRunning(staminaCostPerSecond);
        }
    }
}
