using UnityEngine;

/// <summary>
/// Clase base abstracta para todos los enemigos del juego.
///
/// RESPONSABILIDADES (SRP):
///   - Estadísticas compartidas: HP, endurance, walkSpeed.
///   - Física: gravedad propia + sistema KnockbackPhysicsBody.
///   - Profundidad 2.5D: escala por posición Z.
///   - Orientación: flip del sprite por dirección X.
///   - Ciclo IA: tick del timer de ataque, bloqueo durante knockback.
///
/// EXTENSIÓN (OCP):
///   Las clases derivadas implementan FollowPlayerLogic(), ExecuteAttack()
///   y OnDamagedBehaviour() sin modificar este archivo.
///
/// PRINCIPIOS APLICADOS:
///   IDamageable / IKnockbackable — interfaces del proyecto, no acoplamiento directo.
///   PlayerController referenciado aquí porque es el único tipo de jugador en el prototipo.
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
    [Tooltip("Distancia de ataque cuerpo a cuerpo.")]
    public float attackRange = 1.5f;

    // ── PUBLIC STATE (leído por EnemyAnimatorDriver) ──────────────
    public bool IsMoving     { get; protected set; }
    public bool IsAttacking  { get; protected set; }
    public bool HasKnockback => _body.IsActive;

    // ── PROTECTED (accesibles por clases derivadas) ───────────────
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
            Debug.LogWarning($"[{GetType().Name}] No se encontró PlayerController en la escena.");
    }

    // ────────────────────────────────────────────────────────────
    //  UPDATE
    // ────────────────────────────────────────────────────────────

    protected virtual void Update()
    {
        ApplyGravity();
        UpdateDepthScale();

        if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

        // Knockback tiene prioridad: bloquea la IA mientras dura
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
    //  UTILIDADES COMPARTIDAS
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
    /// Invierte la escala X para cambiar la orientación del sprite.
    /// Seguro de usar junto a UpdateDepthScale: el escalado preserva el signo de X.
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
    /// Reinicia el cooldown de ataque con variación aleatoria.
    /// Llamado también por componentes de ataque externos (ej: RioTutteAttacks).
    /// </summary>
    public void ResetAttackCooldown()
        => _attackTimer = Random.Range(attackCooldownMin, attackCooldownMax);

    // ────────────────────────────────────────────────────────────
    //  ABSTRACT
    // ────────────────────────────────────────────────────────────

    /// <summary>IA de movimiento hacia el objetivo.</summary>
    protected abstract void FollowPlayerLogic();

    /// <summary>Decide y lanza el ataque correspondiente a la fase actual.</summary>
    protected abstract void ExecuteAttack();

    /// <summary>Reacción específica del enemigo al recibir daño.</summary>
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

    public void ReceiveKnockback(Vector3 direction, float force)
    {
        _body.Receive(direction, force);
        float velEfectiva = _body.mass > 0f ? force / _body.mass : force;
        Debug.Log($"[{GetType().Name}] Knockback | fuerza:{force:F1} masa:{_body.mass:F2} → vel:{velEfectiva:F1} u/s");
    }

    public void ApplyClashKnockback(Vector3 direction, float force, float _duration)
        => ReceiveKnockback(direction, force);
}
