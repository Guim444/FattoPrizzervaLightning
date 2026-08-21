using UnityEngine;

/// <summary>
/// RioTutte-specific animation controller.
/// Extends EnemyAnimatorDriver adding parameters specific to this enemy.
///
/// PARAMETERS MANAGED (in addition to the common ones in EnemyAnimatorDriver):
///   dashGrab      (bool) — during the grab dash
///   isGrabbing    (bool) — holding the player
///   superDash     (bool) — during the super dash
///   knockDownFront (bool) — knocked down face-forward (phase 2)
///   knockDownBack  (bool) — knocked down face-backward (phase 2)
///   phase         (int)  — current phase (0-3)
///
/// CONVENTION:
///   Only continuous-state bools/ints. Punctual SetTriggers
///   (GrabPunch, Hit, Punch) are fired from event code.
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
