// KnockedbackState.cs
using UnityEngine;

public class KnockedbackState : IStateActions
{
    public PlayerController player;
    public CharacterController controller;

    public void Enter()
    {
        // Bloquea controles. La física y la duración las gestiona
        // PlayerKnockbackHandler — él llama SetState(Idle) cuando para.
        player.canMove   = false;
        player.canAttack = false;
    }

    public void Update() { }

    public void Exit()
    {
        // Restaura controles sin importar cómo se salió del estado.
        player.canMove   = true;
        player.canAttack = true;
    }
}
