using UnityEngine;

/// <summary>
/// Determines the correct player state based on current input and conditions.
/// Separated from PlayerController for better organization.
///
/// 2.5D change: reads input from PlayerInputHandler and movement data from
/// PlayerMovement instead of accessing PlayerController fields directly.
/// </summary>
public static class PlayerStateHelper
{
    /// <summary>
    /// Determines the appropriate state based on input and player conditions.
    /// Called every frame from PlayerStateMachineController.HandleStateTransitions().
    /// </summary>
    public static State DetermineState(
        PlayerController player,
        PlayerStaminaManager staminaManager,
        bool isOnSlope,
        State currentState)
    {
        // GLIDING SYSTEM (COMMENTED OUT - TO BE ENABLED AFTER TELEPORTATION TESTING)
        // Don't change state if on slope (GlidingState handles it)
        // if (isOnSlope && currentState == State.Gliding)
        //     return State.Gliding;

        var input    = player.inputHandler;
        var movement = player.movement;

        bool hasMovementInput = movement.GetDirectionalInput().magnitude > 0.1f;

        if (hasMovementInput)
        {
            if (staminaManager.isTired)
                return State.Tired;

            // Run input held AND enough stamina para correr
            if (input.IsRunInputHeld && staminaManager.currentStamina > 0)
            {
                // Punch presionado mientras corre → PunchRunning (solo en press, no en hold)
                if (input.IsPunchInputPressed)
                    return State.PunchRunning;

                return State.Running;
            }
            else
            {
                // Punch presionado mientras camina → Punching
                if (input.IsPunchInputPressed)
                    return State.Punching;

                return State.Moving;
            }
        }
        else
        {
            // Sin input de movimiento
            if (staminaManager.isTired)
                return State.Tired;

            if (input.IsPunchInputPressed)
                return State.Punching;

            return State.Idle;
        }
    }

    /// <summary>
    /// Determines the correct state when exiting a slope zone.
    /// </summary>
    public static State DetermineStateFromInput(PlayerStaminaManager staminaManager)
    {
        if (staminaManager.isTired)
            return State.Tired;

        return State.Idle;
    }
}