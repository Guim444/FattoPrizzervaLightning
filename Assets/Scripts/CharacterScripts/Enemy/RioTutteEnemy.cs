using UnityEngine;
using System.Collections;
using UnityEditor.AnimatedValues;

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
    public int endurance = 0;
    public int damage = 1;

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

    public float groundFriction = 9f;
    public float airFriction = 1.5f;
    public float stopThreshold = 0.15f;

    // --------------------------------------------------------
    //  ATTACK
    // --------------------------------------------------------

    [Header("Game Design: Attack")]
    public float attackRange = 1.5f;

    public float attackCooldown = 2f;
    public LayerMask playerLayer;

    // --------------------------------------------------------
    //  RUNTIME STATE
    // --------------------------------------------------------

    public bool IsMoving => _cc != null && _cc.velocity.magnitude > 0.05f;

    private CharacterController _cc;
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
        _verticalVelocity = -2f;

        _body = KnockbackPhysicsBody.Default;
        _body.groundFriction = groundFriction;
        _body.airFriction = airFriction;
        _body.stopThreshold = stopThreshold;
    }

    void Start()
    {
        _body.mass = KnockbackPhysicsBody.MassFromEndurance(endurance);

        var playerObj = FindObjectOfType<PlayerController>();
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerController = playerObj;
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

    void MoveTowardPlayer()
    {
        Vector3 dir = _playerTransform.position - transform.position;
        dir.y = 0f;
        if (dir.magnitude < attackRange * 0.9f) return;
        dir.Normalize();
        if (_cc.enabled) _cc.Move(dir * walkSpeed * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (_cc == null) return;
        if (_cc.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
        else _verticalVelocity += gravity * Time.deltaTime;
        if (_cc.enabled) _cc.Move(new Vector3(0f, _verticalVelocity * Time.deltaTime, 0f));
    }

    // RioTutteEnemy.cs — método TryAttackPlayer completo
    void TryAttackPlayer()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange, playerLayer);
        foreach (var hit in hits)
        {
            //Activar luego de Fase 2
            // if (!hit.TryGetComponent<IDamageable>(out var target)) continue;
            //
            // target.TakeDamage(damage);
            //
            Vector3 dir = (hit.transform.position - transform.position).normalized;
            if (hit.TryGetComponent<IKnockbackable>(out var kb))
            {
                // RioTutte usa su propio endurance como si fuera "el player"
                // en la tabla — cuanto mayor su endurance, más empuja al player
                KnockbackResult result = KnockbackResolver.Resolve(
                    AttackType.Punch,
                    endurance
                );
                kb.ReceiveKnockback(dir, result.ForceOnTarget);
                _anim.SetTrigger("Punch");
            }

            _attackTimer = attackCooldown;
            Debug.Log($"[RioTutte] Ataque al player | daño: {damage}");
            break;
        }
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
        _body.mass = KnockbackPhysicsBody.MassFromEndurance(endurance);
        _body.Receive(direction, force);
        Debug.Log($"[RioTutte] Knockback | fuerza:{force:F1} masa:{_body.mass:F2}");
    }

    public void ApplyClashKnockback(Vector3 direction, float force, float _duration)
        => ReceiveKnockback(direction, force);
}
