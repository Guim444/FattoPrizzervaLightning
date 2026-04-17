using UnityEngine;

/// <summary>
/// Responsabilidad única: ejecutar un ataque del jugador.
/// Detecta al objetivo con Raycast/OverlapSphere, llama a KnockbackResolver,
/// aplica daño + knockback al target y reacción (recoil) al propio player.
///
/// Reemplaza a PlayerCombat.ExecutePunch().
/// ClashHandler sigue siendo independiente (gestiona colisión física continua).
///
/// SETUP: añadir al mismo GameObject que PlayerController.
/// </summary>
[RequireComponent(typeof(PlayerController))]
public class CombatAttackHandler : MonoBehaviour
{
    // --------------------------------------------------------
    //  REFERENCIAS
    // --------------------------------------------------------

    [Header("References")]
    [Tooltip("Opcional. Si es null se usan los valores por defecto del KnockbackResolver.")]
    public KnockbackResolverConfig resolverConfig;

    // --------------------------------------------------------
    //  PARÁMETROS DE DISEÑO
    // --------------------------------------------------------

    [Header("Game Design: Attack Detection")]
    public float punchRange = 1.8f;

    public float punchRangeRunning = 2.6f; // PunchRunning tiene más alcance
    public float edgeThresholdRatio = 0.6f; // hits por encima de este ratio = borde (½ daño)
    public LayerMask enemyLayer;

    [Header("Game Design: Damage Values")]
    public float basicPunchDamage = 1f;

    public float runningPunchDamage = 1.5f;
    public float edgeDamageMultiplier = 0.5f;

    [Header("Game Design: Knockback Multiplier")]
    [Tooltip("Multiplicador de knockback adicional al atacar (vs colisionar).\n" +
             "Ejemplo: si ClashHandler da 8, atacar dará 8 * attackKnockbackMultiplier")]
    public float attackKnockbackMultiplier = 1.5f;

    // --------------------------------------------------------
    //  PRIVATE
    // --------------------------------------------------------

    private PlayerController _player;

    // --------------------------------------------------------
    //  INIT
    // --------------------------------------------------------

    void Awake() => _player = GetComponent<PlayerController>();

    // --------------------------------------------------------
    //  API PÚBLICA
    // --------------------------------------------------------

    /// <summary>
    /// Punto de entrada único para cualquier ataque del jugador.
    /// Llamar desde el estado correspondiente de la StateMachine
    /// (PunchingState, PunchRunningState) en su Enter() o en el
    /// frame de impacto según la animación.
    /// </summary>
    public void ExecuteAttack()
    {
        if (!_player.canAttack) return;
        _player.canAttack = false;

        AttackType attackType = KnockbackResolver.StateToAttackType(_player.currentState);
        Vector3 direction = GetAttackDirection();

        // Esfera proyectada hacia adelante: el origen se desplaza la mitad del radio
        // en la dirección de ataque, así el alcance efectivo = radius * 1.5 en vez de radius.
        float radius = attackType == AttackType.PunchRunning ? punchRangeRunning : punchRange;
        Vector3 origin = transform.position + Vector3.up * 0.5f + direction * (radius * 0.5f);

        Debug.DrawLine(transform.position + Vector3.up * 0.5f, origin + direction * radius, Color.red, 0.5f);

        LayerMask mask = enemyLayer.value == 0 ? ~0 : enemyLayer;
        Collider[] all = Physics.OverlapSphere(origin, radius, mask);

        // Solo colliders de otros GameObjects y que estén en la dirección de ataque
        Collider[] hits = System.Array.FindAll(all, c =>
            c.gameObject != gameObject &&
            Vector3.Dot((c.transform.position - transform.position).normalized, direction) > 0.3f
        );

        if (hits.Length == 0)
        {
            Debug.Log($"[Attack] Sin impacto — origin:{origin} radius:{radius} dir:{direction}");
            return;
        }

        Collider closest = GetClosest(hits);
        float damage = CalculateDamage(attackType, true);

        // El endurance del enemigo ya no existe — solo importa el del player
        KnockbackResult result = KnockbackResolver.Resolve(
            attackType,
            _player.combat.endurance,
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

        Debug.Log($"[Attack] {attackType} | playerEnd:{_player.combat.endurance} " +
                  $"forceEnemy:{result.ForceOnTarget:F1} forceSelf:{result.ForceOnSelf:F1}");
    }

    // CombatAttackHandler.cs — método GetClosest completo
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
