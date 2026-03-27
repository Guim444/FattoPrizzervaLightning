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
        AttackType attackType = KnockbackResolver.StateToAttackType(_player.currentState);
        float range = attackType == AttackType.PunchRunning
            ? punchRangeRunning
            : punchRange;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 direction = GetAttackDirection();

        Debug.DrawRay(origin, direction * range, Color.red, 0.5f);
        Debug.Log($"[Attack] type:{attackType} range:{range} dir:{direction} state:{_player.currentState}");
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, enemyLayer))
            return;

        bool isCenterHit = (hit.distance / range) < edgeThresholdRatio;
        float damage = CalculateDamage(attackType, isCenterHit);

        // --- Resolver ---
        int receiverEndurance = GetReceiverEndurance(hit.collider);
        KnockbackResult result = KnockbackResolver.Resolve(
            attackType,
            _player.combat.endurance,
            receiverEndurance,
            resolverConfig
        );

        // --- Aplicar al target ---
        if (hit.collider.TryGetComponent<IDamageable>(out var damageable))
            damageable.TakeDamage((int)damage);

        float knockbackForce = result.ForceOnTarget * attackKnockbackMultiplier;
        if (hit.collider.TryGetComponent<IKnockbackable>(out var kb))
            kb.ReceiveKnockback(direction, knockbackForce);

        // --- Aplicar reacción al player (Newton 3) ---
        float selfKnockback = result.ForceOnSelf * attackKnockbackMultiplier;
        if (selfKnockback > 0.01f)
            _player.knockbackHandler.ReceiveKnockback(-direction, selfKnockback);

        // --- Frena al player al impactar (feedback táctil) ---
        _player.movement.LastDirection = Vector3.zero;
        _player.movement.CurrentSpeed = Vector3.zero;
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

    /// <summary>
    /// Extrae el endurance del receptor de forma segura.
    /// Añadir más tipos de enemigo aquí conforme se creen.
    /// </summary>
    private int GetReceiverEndurance(Collider col)
    {
        if (col.TryGetComponent<RioTutteEnemy>(out var enemy))
            return enemy.endurance;
        return 0;
    }
}
