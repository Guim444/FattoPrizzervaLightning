using UnityEngine;

/// <summary>
/// RioTutte — enemigo tutorial de Fatto Prizzerva Lightning.
///
/// RESPONSABILIDAD (SRP):
///   Orquestar el comportamiento de RioTutte: IA de seguimiento,
///   selección de ataque por fase, reacción al daño y cambio de fase.
///   La física de los ataques vive en RioTutteAttacks.
///   Las animaciones viven en RioTutteAnimatorDriver.
///
/// FASES:
///   0 → Solo camina y golpea simple. Sin ataques especiales.
///   1 → Activa DashGrab (agarre). Transición al recibir suficiente daño.
///   2 → Añade SuperDash. +1 endurance. Puede caer al suelo tras knockback fuerte.
///   3 → Fase final (cinemática — ver GDD).
///
/// COLISIONES:
///   OnTriggerEnter          — contacto con el player; inicia GrabPlayer si procede.
///   OnControllerColliderHit — choque con pared o player durante DashGrab.
///   LateUpdate              — resolución de penetración física player↔RioTutte.
/// </summary>
[RequireComponent(typeof(RioTutteAttacks))]
[RequireComponent(typeof(RioTutteAnimatorDriver))]
public class RioTutteEnemy : EnemyBase, IPhaseChangeHandler
{
    // ── ATTACK ───────────────────────────────────────────────────
    [Header("Attack: Base Punch")]
    [Tooltip("Fuerza base del golpe simple (fase 0) y del golpe de agarre (×2).")]
    public float attackKnockbackBase = 5f;

    // ── PHASE ────────────────────────────────────────────────────
    [Header("Phase")]
    [Tooltip("Número de RunningPunches que el player debe conectar en fase 1 para avanzar a fase 2.")]
    public int runningPunchsToAdvance = 4;

    // ── PHASE TRANSITION ─────────────────────────────────────────
    [Header("Phase Transition")]
    [Tooltip("Segundos que RioTutte se queda quieto al entrar en una nueva fase (da peso a la transición).")]
    public float phaseEntrancePause = 1.5f;

    // ── FALL STATE (fase 2) ──────────────────────────────────────
    [Header("Fase 2: Fall")]
    [Tooltip("Segundos que RioTutte permanece caído en el suelo.")]
    public float groundedTimer;

    [Tooltip("True = caída frontal. False = caída trasera.")]
    public bool fallDirection;

    // ── IPhaseChangeHandler ──────────────────────────────────────
    public int CurrentPhase { get; private set; }

    // ── PRIVATE ──────────────────────────────────────────────────
    private RioTutteAttacks _attacks;
    private Animator        _anim;
    private Vector3         _lastMoveDirection;
    private int             _runningPunchHits;

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

    // Cuando true, RioTutte se orienta cada frame hacia el player por eje Z (modo libre / fase 2+).
    public bool facePlayerByZ;

    protected override void Update()
    {
        // Sincroniza IsAttacking con el componente de ataques antes de que base.Update() lo lea
        IsAttacking = _attacks.IsAttacking;

        base.Update();

        if (groundedTimer > 0f) groundedTimer -= Time.deltaTime;

        if (facePlayerByZ) FlipTowardPlayerZ();
    }

    void LateUpdate()
    {
        // Resolución de penetración física player↔RioTutte
        if (_player == null) return;

        Vector3 delta = transform.position - _player.transform.position;
        delta.y = 0f;
        float dist    = delta.magnitude;
        float minDist = MinSeparation;

        if (dist >= minDist || dist < 0.001f) return;

        Vector3 pushDir     = delta.normalized;
        float   penetration = minDist - dist;
        int     playerEnd   = _player.combat.endurance;

        if (_attacks.IsUsingDashGrab)
        {
            // Durante DashGrab no resolver penetración: necesitamos el solapamiento
            // para que OnTriggerEnter / OnControllerColliderHit detecten el contacto y ejecuten GrabPlayer().
            return;
        }

        if (_player.currentState == State.PunchRunning)
        {
            // PunchRun del player gana: RioTutte cede completamente
            _cc.enabled = false;
            transform.position += pushDir * penetration;
            _cc.enabled = true;
        }
        else if (playerEnd > 0)
        {
            // Player más fuerte (adrenaline): cesión proporcional al endurance
            // +1 → 33% RioTutte cede  |  +3 → 100% RioTutte cede
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
            // endurance <= 0: RioTutte actúa como pared; el player rebota
            _player.cc.enabled = false;
            _player.transform.position -= pushDir * penetration;
            _player.cc.enabled = true;
        }
    }

    // ────────────────────────────────────────────────────────────
    //  EnemyBase: IMPLEMENTACIONES ABSTRACTAS
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

            default: // fase 2+
                float dist     = Vector3.Distance(transform.position, _player.transform.position);
                // Lejos → DashGrab (cierra distancia). Cerca → 50/50 entre DashGrab y SuperDash.
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

        // No interrumpir la animación de caída con Hit
        if (groundedTimer <= 0f)
            _anim.SetTrigger("Hit");

        // Si recibe knockback durante DashGrab, lo cancela
        if (_attacks.IsUsingDashGrab)
            _attacks.CancelAttack();

        switch (CurrentPhase)
        {
            case 1:
                if (_player.currentState == State.PunchRunning)
                {
                    _runningPunchHits++;
                    Debug.Log($"[RioTutte] RunningPunch conectado: {_runningPunchHits}/{runningPunchsToAdvance}");
                    if (groundedTimer <= 0f) // solo inicia la caída si no está ya cayendo
                    {
                        fallDirection = false;
                        groundedTimer = 3f;
                    }
                    if (_runningPunchHits >= runningPunchsToAdvance)
                        AdvancePhase();
                }
                break;

            case 2:
                // Knockbacks muy fuertes pueden tumbar a RioTutte en fase 2
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

    public override void Die()
    {
        Debug.Log("[RioTutte] Derrotado.");
        activeAI = false;
        _body.Cancel();
        _attacks.CancelAttack();
        // TODO: animación de derrota + notificar TutorialRingManager
    }

    /// <summary>
    /// Resetea contadores internos de fase sin cambiar CurrentPhase.
    /// Llamado por TutorialRingManager en cada retry.
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
        Debug.Log($"[RioTutte] Fase → {CurrentPhase}");

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
                // TODO: cinemática de transición fase 1→2 (TutorialRingManager)
                break;

            case 3:
                // TODO: cinemática fase final
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
        _player.knockbackHandler.ReceiveEnemyKnockback(dir, attackKnockbackBase);
        _anim.SetTrigger("Punch");
        ResetAttackCooldown();
    }

    void FlipTowardPlayerZ()
    {
        if (_player == null) return;
        float dz = _player.transform.position.z - transform.position.z;
        if (Mathf.Abs(dz) < 0.1f) return;

        // Misma convención que el player: Z > 0 → derecha → scaleX negativo
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

        // TODO: activar daño de contacto cuando se implemente fase 2
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
