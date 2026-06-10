using UnityEngine;

/// <summary>
/// RioTutte — tutorial enemy in Fatto Prizzerva Lightning.
///
/// RESPONSIBILITY (SRP):
///   Orchestrates RioTutte's behaviour: follow AI,
///   attack selection by phase, damage reaction, and phase change.
///   Attack physics live in RioTutteAttacks.
///   Animations live in RioTutteAnimatorDriver.
///
/// PHASES:
///   0 → Walks and simple punch only. No special attacks.
///   1 → Activates DashGrab (grab). Transitions on enough damage received.
///   2 → Adds SuperDash. +1 endurance. Can fall after strong knockback.
///   3 → Final phase (cinematic — see GDD).
///
/// COLLISIONS:
///   OnTriggerEnter          — contact with player; starts GrabPlayer if applicable.
///   OnControllerColliderHit — collision with wall or player during DashGrab.
///   LateUpdate              — physical penetration resolution player↔RioTutte.
/// </summary>
[RequireComponent(typeof(RioTutteAttacks))]
[RequireComponent(typeof(RioTutteAnimatorDriver))]
public class RioTutteEnemy : EnemyBase, IPhaseChangeHandler
{
    // ── ATTACK ───────────────────────────────────────────────────
    [Header("Attack: Base Punch")]
    [Tooltip("Base force of the simple punch (phase 0) and grab hit (×2).")]
    public float attackKnockbackBase = 5f;

    // ── PHASE ────────────────────────────────────────────────────
    [Header("Phase")]
    [Tooltip("Number of RunningPunches the player must land in phase 1 to advance to phase 2.")]
    public int runningPunchsToAdvance = 4;

    // ── PHASE TRANSITION ─────────────────────────────────────────
    [Header("Phase Transition")]
    [Tooltip("Seconds RioTutte stays still when entering a new phase (gives weight to the transition).")]
    public float phaseEntrancePause = 1.5f;

    // ── FALL STATE (phase 2) ─────────────────────────────────────
    [Header("Phase 2: Fall")]
    [Tooltip("Seconds RioTutte remains grounded.")]
    public float groundedTimer;

    [Tooltip("True = fall forward. False = fall backward.")]
    public bool fallDirection;

    // ── IPhaseChangeHandler ──────────────────────────────────────
    public int CurrentPhase { get; private set; }

    // ── PRIVATE ──────────────────────────────────────────────────
    private RioTutteAttacks _attacks;
    private Animator        _anim;
    private Vector3         _lastMoveDirection;
    private int             _runningPunchHits;
    private bool            _punchRunImpactPending;

    float MinSeparation => _player != null
        ? _cc.radius + _player.cc.radius + _cc.skinWidth + _player.cc.skinWidth
        : attackRange * 0.5f;

    // ────────────────────────────────────────────────────────────
    //  INIT
    // ────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _attacks = GetComponent<RioTutteAttacks>();
        _anim    = GetComponentInChildren<Animator>();
    }

    // ────────────────────────────────────────────────────────────
    //  UPDATE
    // ────────────────────────────────────────────────────────────

    // When true, RioTutte orients each frame towards the player by Z axis (free mode / phase 2+).
    public bool facePlayerByZ;

    protected override void Update()
    {
        // Sync IsAttacking with the attacks component before base.Update() reads it
        IsAttacking = _attacks.IsAttacking;

        base.Update();

        if (groundedTimer > 0f) groundedTimer -= Time.deltaTime;

        if (facePlayerByZ) FlipTowardPlayerZ();
    }

    void LateUpdate()
    {
        // Physical penetration resolution player↔RioTutte
        if (_player == null) return;

        Vector3 delta = transform.position - _player.transform.position;
        delta.y = 0f;
        float dist    = delta.magnitude;
        float minDist = MinSeparation;

        if (dist >= minDist || dist < 0.001f) return;

        Vector3 pushDir     = delta.normalized;
        float   penetration = minDist - dist;
        int     playerEnd   = _player.Combat.endurance;

        if (_attacks.IsUsingDashGrab)
        {
            // During DashGrab do not resolve penetration: we need the overlap
            // so OnTriggerEnter / OnControllerColliderHit can detect contact and call GrabPlayer().
            return;
        }

        if (_punchRunImpactPending)
        if (_player.StateMachineController.CurrentState == State.PunchRunning)
        {
            // Fully yield only on the exact frame of the PunchRunning impact
            _punchRunImpactPending = false;
            _cc.enabled = false;
            transform.position += pushDir * penetration;
            _cc.enabled = true;
        }
        else if (playerEnd > 0)
        {
            // Player stronger (adrenaline): yield proportional to endurance
            // +1 → 33% RioTutte yields  |  +3 → 100% RioTutte yields
            float yieldRatio = Mathf.Clamp01(playerEnd / 3f);

            _cc.enabled = false;
            transform.position += pushDir * penetration * yieldRatio;
            _cc.enabled = true;

            _player.cc.enabled = false;
            _player.transform.position -= pushDir * penetration * (1f - yieldRatio);
            _player.cc.enabled = true;
        }
        else
        {
            // endurance <= 0: RioTutte acts as a wall; player bounces
            _player.cc.enabled = false;
            _player.transform.position -= pushDir * penetration;
            _player.cc.enabled = true;
        }
    }

    // ────────────────────────────────────────────────────────────
    //  EnemyBase: ABSTRACT IMPLEMENTATIONS
    // ────────────────────────────────────────────────────────────

    protected override void FollowPlayerLogic()
    {
        if (_attacks.IsAttacking || groundedTimer > 0f)
        {
            IsMoving = false;
            return;
        }

        Vector3 dir = _player.transform.position - transform.position;
        dir.y = 0f;

        if (dir.magnitude < MinSeparation)
        {
            IsMoving = false;
            return;
        }

        dir.Normalize();
        _lastMoveDirection = dir;
        FlipCharacter(dir);

        if (_cc.enabled) _cc.Move(dir * walkSpeed * Time.deltaTime);
        IsMoving = true;
    }

    protected override void ExecuteAttack()
    {
        switch (CurrentPhase)
        {
            case 0:
                SimplePunch();
                break;

            case 1:
                _attacks.StartDashGrab();
                break;

            default: // phase 2+
                float dist     = Vector3.Distance(transform.position, _player.transform.position);
                // Far → DashGrab (closes distance). Close → 50/50 between DashGrab and SuperDash.
                bool  useSuperDash = dist <= 5f && Random.value > 0.5f;
                if (useSuperDash)
                    _attacks.StartSuperDash(_lastMoveDirection);
                else
                    _attacks.StartDashGrab();
                break;
        }
    }

    protected override void OnDamagedBehaviour(int dmg)
    {
        if (dmg <= 0) return;

        // Do not interrupt the fall animation with Hit
        if (groundedTimer <= 0f)
            _anim.SetTrigger("Hit");

        // If knockback is received during DashGrab, cancel it
        if (_attacks.IsUsingDashGrab)
            _attacks.CancelAttack();

        switch (CurrentPhase)
        {
            case 1:
                if (_player.StateMachineController.CurrentState == State.PunchRunning)
                {
                    _runningPunchHits++;
                    Debug.Log($"[RioTutte] RunningPunch connected: {_runningPunchHits}/{runningPunchsToAdvance}");
                    if (groundedTimer <= 0f) // only start the fall if not already falling
                    {
                        fallDirection = false;
                        groundedTimer = 3f;
                    }
                    if (_runningPunchHits >= runningPunchsToAdvance)
                        AdvancePhase();
                }
                break;

            case 2:
                // Very strong knockbacks can knock RioTutte down in phase 2
                if (dmg >= 30) TriggerFall();
                break;
        }

        Vector3 dir = _player.transform.position - transform.position;
        dir.y = 0f;

        if (dir.magnitude < MinSeparation)
        {
            IsMoving = false;
            return;
        }

        dir.Normalize();
        _lastMoveDirection = dir;
        FlipCharacter(dir);

        if (_cc.enabled) _cc.Move(dir * walkSpeed * Time.deltaTime);
        IsMoving = true;
    }

    public override void ReceiveKnockback(Vector3 direction, float force)
    {
        base.ReceiveKnockback(direction, force);
        if (_player != null && _player.StateMachineController.CurrentState == State.PunchRunning)
            _punchRunImpactPending = true;
    }

    public override void Die()
    {
        Debug.Log("[RioTutte] Defeated.");
        activeAI = false;
        _body.Cancel();
        _attacks.CancelAttack();
        // TODO: defeat animation + notify TutorialRingManager
    }

    /// <summary>
    /// Resets internal phase counters without changing CurrentPhase.
    /// Called by TutorialRingManager on each retry.
    /// </summary>
    public void ResetPhaseCounters()
    {
        _runningPunchHits = 0;
    }

    // ────────────────────────────────────────────────────────────
    //  IPhaseChangeHandler
    // ────────────────────────────────────────────────────────────

    public void AdvancePhase()
    {
        CurrentPhase++;
        Debug.Log($"[RioTutte] Phase → {CurrentPhase}");

        switch (CurrentPhase)
        {
            case 1:
                activeAI      = true;
                groundedTimer = phaseEntrancePause;
                break;

            case 2:
                endurance++;
                _body.mass    = mass + endurance * 0.4f;
                facePlayerByZ = true;
                groundedTimer = phaseEntrancePause;
                // TODO: phase 1→2 transition cinematic (TutorialRingManager)
                break;

            case 3:
                // TODO: final phase cinematic
                break;
        }

        ResetAttackCooldown();
    }

    // ────────────────────────────────────────────────────────────
    //  HELPERS
    // ────────────────────────────────────────────────────────────

    void SimplePunch()
    {
        if (_player == null) return;

        Vector3 toPlayer = _player.transform.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > MinSeparation * 1.5f) return;

        Vector3 dir = toPlayer.magnitude > 0.01f ? toPlayer.normalized : transform.forward;
        _player.KnockbackHandler.ReceiveEnemyKnockback(dir, attackKnockbackBase);
        _anim.SetTrigger("Punch");
        ResetAttackCooldown();
    }

    void FlipTowardPlayerZ()
    {
        if (_player == null) return;
        float dz = _player.transform.position.z - transform.position.z;
        if (Mathf.Abs(dz) < 0.1f) return;

        // Same convention as player: Z > 0 → right → scaleX negative
        float desired = dz > 0f ? -1f : 1f;
        float current = Mathf.Sign(transform.localScale.x);
        if (!Mathf.Approximately(current, desired))
            transform.localScale = new Vector3(-transform.localScale.x, transform.localScale.y, transform.localScale.z);
    }

    void TriggerFall()
    {
        groundedTimer = 3f;
        Vector3 toPlayer = (_player.transform.position - transform.position).normalized;
        fallDirection = Vector3.Dot(transform.right * Mathf.Sign(transform.localScale.x), toPlayer) < 0f;
    }

    // ────────────────────────────────────────────────────────────
    //  COLLISION EVENTS
    // ────────────────────────────────────────────────────────────

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || other.isTrigger) return;

        if (_attacks.IsUsingSuperDash) return;

        if (_attacks.IsUsingDashGrab)
        {
            _attacks.GrabPlayer();
            return;
        }

        // TODO: activate contact damage when phase 2 is implemented
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!_attacks.IsUsingDashGrab) return;

        if (hit.gameObject.CompareTag("Wall"))
        {
            _attacks.CancelAttack();
            ResetAttackCooldown();
            return;
        }

        if (hit.gameObject.CompareTag("Player") && !_attacks.IsGrabbing)
            _attacks.GrabPlayer();
    }
}
