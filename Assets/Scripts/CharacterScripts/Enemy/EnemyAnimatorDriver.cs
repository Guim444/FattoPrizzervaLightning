using UnityEngine;

/// <summary>
/// Clase base abstracta del controlador de animaciones para enemigos.
///
/// RESPONSABILIDAD ÚNICA (SRP):
///   Leer el estado de EnemyBase y volcar los parámetros comunes
///   al Animator cada frame. No contiene lógica de juego.
///
/// EXTENSIÓN (OCP):
///   Cada enemigo concreto implementa DriveSpecificParams() para
///   escribir sus propios parámetros sin modificar esta clase.
///
/// CONVENCIÓN DEL PROYECTO:
///   Solo escribe bools/ints de estado continuo aquí.
///   Los SetTrigger (eventos puntuales) se llaman desde el código
///   que genera el evento (ej: RioTutteAttacks, RioTutteEnemy).
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
    /// Escribe los parámetros del Animator específicos de este enemigo.
    /// Llamado cada frame después de los parámetros comunes.
    /// </summary>
    protected abstract void DriveSpecificParams();
}
