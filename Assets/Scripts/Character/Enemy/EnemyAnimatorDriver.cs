using UnityEngine;

/// <summary>
/// Abstract base class for enemy animator controllers.
///
/// SINGLE RESPONSIBILITY (SRP):
///   Reads EnemyBase state and pushes common parameters
///   to the Animator each frame. Contains no gameplay logic.
///
/// EXTENSION (OCP):
///   Each concrete enemy implements DriveSpecificParams() to
///   write its own parameters without modifying this class.
///
/// PROJECT CONVENTION:
///   Only write continuous-state bools/ints here.
///   SetTrigger (point-in-time events) are called from the code
///   that generates the event (e.g.: RioTutteAttacks, RioTutteEnemy).
/// </summary>
public abstract class EnemyAnimatorDriver : MonoBehaviour
{
    protected Animator  _anim;
    protected EnemyBase _enemy;

    protected virtual void Awake()
    {
        _anim  = GetComponentInChildren<Animator>();
        _enemy = GetComponent<EnemyBase>();
    }

    protected virtual void Update()
    {
        if (_enemy == null) return;

        _anim.SetBool("isMoving",    _enemy.IsMoving);
        _anim.SetBool("isAttacking", _enemy.IsAttacking);
        _anim.SetBool("hasKnockback", _enemy.HasKnockback);

        DriveSpecificParams();
    }

    /// <summary>
    /// Writes the Animator parameters specific to this enemy.
    /// Called every frame after the common parameters.
    /// </summary>
    protected abstract void DriveSpecificParams();
}
