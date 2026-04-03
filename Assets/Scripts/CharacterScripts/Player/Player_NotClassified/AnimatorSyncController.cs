// AnimatorSyncController.cs — receptor de Animation Events del player

using UnityEngine;

/// <summary>
/// Receptor de Animation Events del Animator del player.
/// Unity llama a estos métodos directamente desde los clips de animación.
/// Este componente debe estar en el mismo GameObject que el Animator.
/// </summary>
public class AnimatorSyncController : MonoBehaviour
{
    private PlayerController _player;

    void Awake()
    {
        _player = GetComponent<PlayerController>();
    }

    /// <summary>
    /// Llamado desde el Animation Event del clip de Punch
    /// en el frame exacto del impacto.
    /// </summary>
    public void OnPunchHit()
    {
        if (_player.canAttack)
        {
            _player.combatAttackHandler.ExecuteAttack();
            _player.animator.ResetTrigger("isPunching");
        }
    }

    /// <summary>
    /// Llamado desde el Animation Event del clip de PunchRunning
    /// en el frame exacto del impacto.
    /// </summary>
    public void OnPunchRunningHit()
    {
        if (_player.canAttack)
        {
            _player.combatAttackHandler.ExecuteAttack();
            _player.animator.ResetTrigger("isPunching");
        }
    }
}
