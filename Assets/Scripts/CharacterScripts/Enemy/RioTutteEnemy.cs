using UnityEngine;
/// <summary>
/// Enemigo tutorial RioTutte — versión refactorizada.
///
/// CAMBIOS respecto a la versión anterior:
///   - ReceiveKnockback delega al KnockbackResolver (fórmula unificada).
///   - Física de knockback gestionada por KnockbackPhysicsBody (mismo sistema que el player).
///   - ApplyClashKnockback() recibe fuerza directa desde ClashHandler.
///   - Eliminada la coroutine SustainedKnockback: reemplazada por el Tick() del physics body.
///   - La masa se auto-configura desde endurance en Start().
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class RioTutteEnemy : MonoBehaviour, IDamageable, IKnockbackable
{
    // --------------------------------------------------------
    //  STATS
    // --------------------------------------------------------

    [Header("Stats")]
    public float HP { get; set; } = 10f;

    [SerializeField]
    Animator _anim;

    // --------------------------------------------------------
    //  MOVEMENT (IA)
    // --------------------------------------------------------

    [Header("Game Design: Movement")]
    public float walkSpeed = 2.5f;

    public bool activeAI = true;

    // --------------------------------------------------------
    //  PHYSICS
    // --------------------------------------------------------

    [Header("Physics")]
    public float gravity = -20f;

    public float groundFriction = 4f;
    public float airFriction = 1.5f;
    public float stopThreshold = 0.15f;

    // --------------------------------------------------------
    //  ATTACK
    // --------------------------------------------------------

    [Header("Game Design: Attack")]
    public float attackRange = 1.5f;

    public float attackCooldown = 2f;

    // --------------------------------------------------------
    //  RUNTIME STATE
    // --------------------------------------------------------

    public bool IsMoving => _cc != null && _cc.velocity.magnitude > 0.05f;

    private CharacterController _cc;
    private CharacterController _playerCC;
    private Transform _playerTransform;
    private PlayerController _playerController;
    private float _attackTimer;
    private float _verticalVelocity;
    private KnockbackPhysicsBody _body;

    // --------------------------------------------------------
    //  INIT
    // --------------------------------------------------------

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        if (_anim == null) _anim = GetComponentInChildren<Animator>();
        _verticalVelocity = -2f;

        _body = KnockbackPhysicsBody.Default;
        _body.groundFriction = groundFriction;
        _body.airFriction = airFriction;
        _body.stopThreshold = stopThreshold;
    }

    void Start()
    {

        var playerObj = FindObjectOfType<PlayerController>();
        if (playerObj != null)
        {
            _playerTransform  = playerObj.transform;
            _playerController = playerObj;
            _playerCC         = playerObj.GetComponent<CharacterController>();
            _body.mass        = KnockbackPhysicsBody.MassFromEndurance(_playerController.combat.endurance);
        }
        else
        {
            Debug.LogWarning("[RioTutte] No se encontró PlayerController en la escena.");
        }
    }

    // --------------------------------------------------------
    //  UPDATE
    // --------------------------------------------------------

    void Update()
    {
        ApplyGravity();

        if (_attackTimer > 0f) _attackTimer -= Time.deltaTime;

        // Knockback tick
        if (_body.IsActive)
        {
            Vector3 delta = _body.Tick(_cc.isGrounded, Time.deltaTime);
            if (_cc.enabled) _cc.Move(delta);
            return; // No IA durante knockback
        }

        if (activeAI && _playerTransform != null)
        {
            MoveTowardPlayer();
            if (_attackTimer <= 0f) TryAttackPlayer();
        }
    }

    // --------------------------------------------------------
    //  AI
    // --------------------------------------------------------

    float MinSeparation => _playerCC != null
        ? _cc.radius + _playerCC.radius + _cc.skinWidth + _playerCC.skinWidth
        : attackRange * 0.5f;

    void MoveTowardPlayer()
    {
        Vector3 dir = _playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.magnitude < MinSeparation) return;
        dir.Normalize();
        if (_cc.enabled) _cc.Move(dir * walkSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (_playerTransform == null || _cc == null) return;

        Vector3 delta = transform.position - _playerTransform.position;
        delta.y = 0f;
        float dist = delta.magnitude;
        float minDist = MinSeparation;

        if (dist < minDist && dist > 0.001f)
        {
            // Empuja a RioTutte fuera del player la distancia de penetración
            Vector3 push = delta.normalized * (minDist - dist);
            _cc.enabled = false;
            transform.position += push;
            _cc.enabled = true;
        }
    }

    void ApplyGravity()
    {
        if (_cc == null) return;
        if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
        else _verticalVelocity += gravity * Time.deltaTime;
        if (_cc.enabled) _cc.Move(new Vector3(0f, _verticalVelocity * Time.deltaTime, 0f));
    }

    void TryAttackPlayer()
    {
        // Re-busca el player si la referencia se perdió (multi-escena)
        if (_playerController == null)
        {
            var obj = FindObjectOfType<PlayerController>();
            if (obj == null) return;
            _playerTransform = obj.transform;
            _playerController = obj;
        }

        Vector3 toPlayer = _playerTransform.position - transform.position;
        toPlayer.y = 0f;
        float dist = toPlayer.magnitude;
        float effectiveRange = Mathf.Max(attackRange, MinSeparation);
        if (dist > effectiveRange) return;

        Vector3 dir = dist > 0.01f ? toPlayer.normalized : transform.forward;

        KnockbackResult result = KnockbackResolver.Resolve(AttackType.Punch, _playerController.combat.endurance);
        _playerController.knockbackHandler.ReceiveKnockback(dir, result.ForceOnTarget);

        // TODO Fase 2: activar daño real
        // _playerController.combat.TakeDamage(damage);

        _anim.SetTrigger("Punch");
        _attackTimer = attackCooldown;

        Debug.Log($"[RioTutte] Ataque al player | fuerza:{result.ForceOnTarget:F1} dist:{dist:F2}");
    }

    // --------------------------------------------------------
    //  IDamageable
    // --------------------------------------------------------

    public void TakeDamage(int dmg)
    {
        // TODO: activar cuando se implemente la fase 2.
        // HP -= dmg;
        _anim.SetTrigger("Hit");
        Debug.Log($"[RioTutte] Recibió {dmg} daño. HP restante: {HP}");
        // if (HP <= 0) Die();
    }

    public void Die()
    {
        Debug.Log("[RioTutte] Derrotado.");
        // TODO: activar animación de derrota en fase 2.
        // activeAI = false;
        // _body.Cancel();
        // gameObject.SetActive(false);
    }

    // --------------------------------------------------------
    //  IKnockbackable + CLASH API
    // --------------------------------------------------------

    /// <summary>Fuerza ya resuelta por KnockbackResolver o ClashHandler.</summary>
    public void ReceiveKnockback(Vector3 direction, float force)
    {
        _body.mass = KnockbackPhysicsBody.MassFromEndurance(_playerController.combat.endurance);
        _body.Receive(direction, force);
        Debug.Log($"[RioTutte] Knockback | fuerza:{force:F1} masa:{_body.mass:F2}");
    }

    public void ApplyClashKnockback(Vector3 direction, float force, float _duration)
        => ReceiveKnockback(direction, force);
}
