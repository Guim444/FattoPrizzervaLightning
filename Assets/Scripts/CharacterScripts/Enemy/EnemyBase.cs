using UnityEngine;

/// <summary>
/// Abstract base class for all enemies in the game.
///
/// RESPONSIBILITIES (SRP):
///   - Shared stats: HP, endurance, walkSpeed.
///   - Physics: own gravity + KnockbackPhysicsBody system.
///   - 2.5D depth: scale by Z position.
///   - Orientation: sprite flip by X direction.
///   - AI cycle: attack timer tick, blocking during knockback.
///
/// EXTENSION (OCP):
///   Derived classes implement FollowPlayerLogic(), ExecuteAttack()
///   and OnDamagedBehaviour() without modifying this file.
///
/// APPLIED PRINCIPLES:
///   IDamageable / IKnockbackable — project interfaces, no direct coupling.
///   PlayerController referenced here because it is the only player type in the prototype.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public abstract class EnemyBase : MonoBehaviour, IDamageable, IKnockbackable
{
    // ── STATS ────────────────────────────────────────────────────
    [Header("Stats")]
    public float HP { get; set; } = 10f;
    public int endurance;

    // ── MOVEMENT ─────────────────────────────────────────────────
    [Header("Movement")]
    public float walkSpeed = 2.5f;
    public bool  activeAI  = true;

    // ── PHYSICS ──────────────────────────────────────────────────
    [Header("Physics")]
    public float gravity        = -20f;
    public float groundFriction = 4f;
    public float airFriction    = 1.5f;
    public float stopThreshold  = 0.15f;
    public float mass           = 1.5f;

    // ── DEPTH SCALE (2.5D) ───────────────────────────────────────
    [Header("Depth Scale")]
    public float minZ     = 0f;
    public float maxZ     = 100f;
    public float minScale = 0.3f;
    public float maxScale = 1f;

    // ── ATTACK TIMER ─────────────────────────────────────────────
    [Header("Attack Cooldown")]
    public float attackCooldownMin = 2f;
    public float attackCooldownMax = 3f;
    [Tooltip("Melee attack range.")]
    public float attackRange = 1.5f;

    // ── PUBLIC STATE (read by EnemyAnimatorDriver) ──────────────
    public bool IsMoving     { get; protected set; }
    public bool IsAttacking  { get; protected set; }
    public bool HasKnockback => _body.IsActive;

    // ── PROTECTED (accessible by derived classes) ────────────────
    protected CharacterController _cc;
    protected KnockbackPhysicsBody _body;
    protected PlayerController _player;
    protected float _attackTimer;

    private float _verticalVelocity;

    // ────────────────────────────────────────────────────────────
    //  INIT
    // ────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _cc              = GetComponent<CharacterController>();
        _verticalVelocity = -2f;

        _body = KnockbackPhysicsBody.Default;
        _body.mass           = mass;
        _body.groundFriction = groundFriction;
        _body.airFriction    = airFriction;
        _body.stopThreshold  = stopThreshold;
    }

    protected virtual void Start()
    {
        var playerObj = FindFirstObjectByType<PlayerController>();
        if (playerObj != null)
            _player = playerObj;
        else
            Debug.LogWarning($"[{GetType().Name}] PlayerController not found in scene.");
    }

    // ────────────────────────────────────────────────────────────
    //  UPDATE
    // ────────────────────────────────────────────────────────────

    protected virtual void Update()
    {
        ApplyGravity();
        UpdateDepthScale();

        if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

        // Knockback takes priority: blocks AI while active
        if (_body.IsActive)
        {
            Vector3 delta = _body.Tick(_cc.isGrounded, Time.deltaTime);
            if (_cc.enabled) _cc.Move(delta);
            IsMoving = false;
            return;
        }

        if (!activeAI || _player == null) return;

        FollowPlayerLogic();

        if (!IsAttacking && _attackTimer <= 0f)
            ExecuteAttack();
    }

    // ────────────────────────────────────────────────────────────
    //  SHARED UTILITIES
    // ────────────────────────────────────────────────────────────

    void ApplyGravity()
    {
        if (_cc.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;
        else
            _verticalVelocity += gravity * Time.deltaTime;

        if (_cc.enabled)
            _cc.Move(new Vector3(0f, _verticalVelocity * Time.deltaTime, 0f));
    }

    void UpdateDepthScale()
    {
        float z           = transform.position.z;
        float t           = Mathf.Clamp01(Mathf.InverseLerp(minZ, maxZ, z));
        float scaleFactor = Mathf.Lerp(maxScale, minScale, t);
        float signX       = Mathf.Sign(transform.localScale.x);
        transform.localScale = new Vector3(scaleFactor * signX, scaleFactor, scaleFactor);
    }

    /// <summary>
    /// Flips the X scale to change the sprite orientation.
    /// Safe to use alongside UpdateDepthScale: scaling preserves the sign of X.
    /// </summary>
    public void FlipCharacter(Vector3 direction)
    {
        if (Mathf.Abs(direction.x) < 0.01f) return;

        float currentSign = Mathf.Sign(transform.localScale.x);
        float desiredSign = Mathf.Sign(direction.x);

        if (!Mathf.Approximately(currentSign, desiredSign))
            transform.localScale = new Vector3(
                -transform.localScale.x,
                 transform.localScale.y,
                 transform.localScale.z
            );
    }

    /// <summary>
    /// Resets the attack cooldown with random variation.
    /// Also called by external attack components (e.g.: RioTutteAttacks).
    /// </summary>
    public void ResetAttackCooldown()
        => _attackTimer = Random.Range(attackCooldownMin, attackCooldownMax);

    // ────────────────────────────────────────────────────────────
    //  ABSTRACT
    // ────────────────────────────────────────────────────────────

    /// <summary>AI movement logic towards the target.</summary>
    protected abstract void FollowPlayerLogic();

    /// <summary>Decides and launches the attack for the current phase.</summary>
    protected abstract void ExecuteAttack();

    /// <summary>Enemy-specific reaction to receiving damage.</summary>
    protected abstract void OnDamagedBehaviour(int dmg);

    // ────────────────────────────────────────────────────────────
    //  IDamageable
    // ────────────────────────────────────────────────────────────

    public void TakeDamage(int dmg)
    {
        OnDamagedBehaviour(dmg);
    }

    public abstract void Die();

    // ────────────────────────────────────────────────────────────
    //  IKnockbackable
    // ────────────────────────────────────────────────────────────

    public virtual void ReceiveKnockback(Vector3 direction, float force)
    {
        _body.Receive(direction, force);
        float velEfectiva = _body.mass > 0f ? force / _body.mass : force;
        Debug.Log($"[{GetType().Name}] Knockback | force:{force:F1} mass:{_body.mass:F2} → vel:{velEfectiva:F1} u/s");
    }

    public void ApplyClashKnockback(Vector3 direction, float force, float _duration)
        => ReceiveKnockback(direction, force);
}
