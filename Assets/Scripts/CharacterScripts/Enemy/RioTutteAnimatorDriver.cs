using UnityEngine;

/// <summary>
/// Controlador de animaciones específico de RioTutte.
/// Extiende EnemyAnimatorDriver añadiendo los parámetros propios de este enemigo.
///
/// PARÁMETROS QUE GESTIONA (además de los comunes en EnemyAnimatorDriver):
///   dashGrab      (bool) — durante el dash de agarre
///   isGrabbing    (bool) — sosteniendo al player
///   superDash     (bool) — durante el super dash
///   knockDownFront (bool) — caído al suelo de frente (fase 2)
///   knockDownBack  (bool) — caído al suelo de espaldas (fase 2)
///   phase         (int)  — fase actual (0-3)
///
/// CONVENCIÓN:
///   Solo bools/ints de estado continuo. Los SetTrigger puntuales
///   (GrabPunch, Hit, Punch) se lanzan desde el código del evento.
/// </summary>
public class RioTutteAnimatorDriver : EnemyAnimatorDriver
{
    private RioTutteEnemy   _rioTutte;
    private RioTutteAttacks _attacks;

    protected override void Awake()
    {
        base.Awake();
        _rioTutte = GetComponent<RioTutteEnemy>();
        _attacks  = GetComponent<RioTutteAttacks>();
    }

    protected override void DriveSpecificParams()
    {
        _anim.SetBool("dashGrab",      _attacks.IsUsingDashGrab);
        _anim.SetBool("isGrabbing",    _attacks.IsGrabbing);
        _anim.SetBool("superDash",     _attacks.IsUsingSuperDash);
        _anim.SetBool("knockDownFront", _rioTutte.groundedTimer > 0f &&  _rioTutte.fallDirection);
        _anim.SetBool("knockDownBack",  _rioTutte.groundedTimer > 0f && !_rioTutte.fallDirection);
        _anim.SetInteger("phase",      _rioTutte.CurrentPhase);
    }
}
