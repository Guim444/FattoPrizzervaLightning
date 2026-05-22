// KnockedbackState.cs
using UnityEngine;

public class KnockedbackState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;

    public void Enter()
    {
        // Blocks controls. Physics and duration are managed by
        // PlayerKnockbackHandler — it calls SetState(Idle) when it stops.
        player.canMove   = false;
        player.canAttack = false;
    }

    public void Update() { }

    public void Exit()
    {
        // Restores controls regardless of how the state was exited.
        player.canMove   = true;
        player.canAttack = true;
    }
}
