using UnityEngine;

/// <summary>
/// Single responsibility: execute a player attack.
/// Detects the target with Raycast/OverlapSphere, calls KnockbackResolver,
/// applies damage + knockback to the target and recoil to the player.
///
/// Replaces PlayerCombat.ExecutePunch().
/// ClashHandler remains independent (handles continuous physical collision).
///
/// SETUP: add to the same GameObject as PlayerController.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CombatAttackHandler : MonoBehaviour
{
    // --------------------------------------------------------
    //  REFERENCES
    // --------------------------------------------------------

    [Header("References")]
    [Tooltip("Optional. If null, KnockbackResolver default values are used.")]
    public KnockbackResolverConfig resolverConfig;

    // --------------------------------------------------------
    //  DESIGN PARAMETERS
    // --------------------------------------------------------

    [Header("Game Design: Attack Detection")]
    public float punchRange = 1.8f;

    public float punchRangeRunning = 2.6f; // PunchRunning has longer range
    public float edgeThresholdRatio = 0.6f; // hits above this ratio = edge (½ damage)
    public LayerMask enemyLayer;

    [Header("Game Design: Damage Values")]
    public float basicPunchDamage = 1f;

    public float runningPunchDamage = 1.5f;
    public float edgeDamageMultiplier = 0.5f;

    [Header("Game Design: Knockback Multiplier")]
    [Tooltip("Additional knockback multiplier when attacking (vs colliding).\n" +
             "Example: if ClashHandler gives 8, attacking gives 8 * attackKnockbackMultiplier")]
    public float attackKnockbackMultiplier = 1.5f;

    // --------------------------------------------------------
    //  PRIVATE
    // --------------------------------------------------------

    private PlayerController _player;
    private AttackType _preparedAttackType;
    private bool _hasPreparedAttack;

    // --------------------------------------------------------
    //  INIT
    // --------------------------------------------------------

    void Awake() => _player = GetComponent<PlayerController>();

    // --------------------------------------------------------
    //  PUBLIC API
    // --------------------------------------------------------

    /// <summary>
    /// Stores the attack type selected by the state before the animation starts.
    /// The Animation Event later consumes this exact context.
    /// </summary>
    public void PrepareAttack(AttackType attackType)
    {
        _preparedAttackType = attackType;
        _hasPreparedAttack = true;
    }

    public void CancelPreparedAttack()
    {
        _hasPreparedAttack = false;
    }

    public void ExecutePreparedAttack()
    {
        if (!_hasPreparedAttack)
        {
            Debug.LogWarning("[Attack] Animation Event received without a prepared attack.", this);
            return;
        }

        AttackType attackType = _preparedAttackType;
        _hasPreparedAttack = false;

        if (!_player.canAttack) return;
        _player.canAttack = false;

        Vector3 direction = GetAttackDirection();

        // Sphere projected forward: origin is offset half the radius
        // in the attack direction, so effective range = radius * 1.5 instead of radius.
        float radius = attackType == AttackType.PunchRunning ? punchRangeRunning : punchRange;
        Vector3 origin = transform.position + Vector3.up * 0.5f + direction * (radius * 0.5f);

        Debug.DrawLine(transform.position + Vector3.up * 0.5f, origin + direction * radius, Color.red, 0.5f);

        LayerMask mask = enemyLayer.value == 0 ? ~0 : enemyLayer;
        Collider[] all = Physics.OverlapSphere(origin, radius, mask);

        // Only colliders from other GameObjects that are in the attack direction
        Collider[] hits = System.Array.FindAll(all, c =>
            c.gameObject != gameObject &&
            Vector3.Dot((c.transform.position - transform.position).normalized, direction) > 0.3f
        );

        if (hits.Length == 0)
        {
            Debug.Log($"[Attack] No hit — origin:{origin} radius:{radius} dir:{direction}");
            return;
        }

        Collider closest = GetClosest(hits);
        float damage = CalculateDamage(attackType, true);

        // Enemy endurance no longer exists — only the player's matters
        KnockbackResult result = KnockbackResolver.Resolve(
            attackType,
            _player.combat.EffectiveEndurance,
            resolverConfig
        );

        if (closest.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage((int)damage);

        if (closest.TryGetComponent<IKnockbackable>(out var kb))
            kb.ReceiveKnockback(direction, result.ForceOnTarget);

        if (result.ForceOnSelf > 0.01f)
            _player.knockbackHandler.ReceiveKnockback(-direction, result.ForceOnSelf);
        _player.movement.LastDirection = Vector3.zero;
        _player.movement.CurrentSpeed = Vector3.zero;

        Debug.Log($"[Attack] {attackType} | playerEnd:{_player.combat.EffectiveEndurance} " +
                  $"forceEnemy:{result.ForceOnTarget:F1} forceSelf:{result.ForceOnSelf:F1}");
    }

    // CombatAttackHandler.cs — full GetClosest method
    private Collider GetClosest(Collider[] hits)
    {
        Collider closest = hits[0];
        float minDist = Vector3.Distance(transform.position, hits[0].transform.position);

        for (int i = 1; i < hits.Length; i++)
        {
            float d = Vector3.Distance(transform.position, hits[i].transform.position);
            if (d < minDist)
            {
                minDist = d;
                closest = hits[i];
            }
        }

        return closest;
    }

    // --------------------------------------------------------
    //  HELPERS
    // --------------------------------------------------------

    private Vector3 GetAttackDirection()
    {
        Vector3 last = _player.movement.LastDirection;
        if (last.magnitude > 0.1f) return last.normalized;

        return _player.movement.LastFacingDirection;
    }

    private float CalculateDamage(AttackType type, bool isCenterHit)
    {
        float multiplier = isCenterHit ? 1f : edgeDamageMultiplier;
        float baseDamage = type == AttackType.PunchRunning
            ? runningPunchDamage
            : basicPunchDamage;
        return baseDamage * multiplier;
    }
}
