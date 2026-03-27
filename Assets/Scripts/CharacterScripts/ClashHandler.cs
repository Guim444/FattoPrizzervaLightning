using UnityEngine;

/// <summary>
/// Detecta el contacto físico directo (clash) entre el player y RioTutte.
/// Delega el cálculo de fuerzas a KnockbackResolver: ya no tiene su propia
/// coroutine de push ni sus propios parámetros de fuerza.
///
/// CAMBIOS respecto a la versión anterior:
///   - baseClashForce / enduranceForceMultiplier eliminados.
///     Las fuerzas vienen del KnockbackResolverConfig.
///   - PushPlayer() (coroutine) eliminado.
///     El player recibe knockback vía PlayerKnockbackHandler.ReceiveKnockback().
///   - El sistema de física es el mismo que para cualquier otro golpe.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ClashHandler : MonoBehaviour
{
    // --------------------------------------------------------
    //  CONFIGURACIÓN
    // --------------------------------------------------------

    [Header("References")]
    public PlayerController         player;
    public KnockbackResolverConfig  resolverConfig;

    [Header("Game Design: Clash")]
    [Tooltip("Knockback base al enemigo al colisionar (sin contar Endurance).")]
    public float baseKnockbackToEnemy = 3f;

    [Tooltip("Cuánto añade el Endurance del player al knockback del enemigo.\n" +
             "Endurance 0 → baseKnockbackToEnemy\n" +
             "Endurance 1 → baseKnockbackToEnemy + 1 * enduranceKnockbackMultiplier")]
    public float enduranceKnockbackMultiplier = 2.5f;

    [Tooltip("Tiempo mínimo entre dos choques consecutivos.")]
    public float clashCooldown = 0.6f;

    // --------------------------------------------------------
    //  PRIVATE
    // --------------------------------------------------------

    private float _cooldownTimer;

    // --------------------------------------------------------
    //  INIT
    // --------------------------------------------------------

    void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
    }

    // --------------------------------------------------------
    //  UPDATE
    // --------------------------------------------------------

    void Update()
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;
    }

    // --------------------------------------------------------
    //  COLLISION DETECTION
    // --------------------------------------------------------

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (_cooldownTimer > 0f)   return;
        if (!player.canMove)       return;

        var enemy = hit.collider.GetComponent<RioTutteEnemy>();
        if (enemy == null) return;

        bool playerMoving = player.movement.GetDirectionalInput().magnitude > 0.1f;
        bool enemyMoving  = enemy.IsMoving;
        if (!playerMoving && !enemyMoving) return;

        ExecuteClash(enemy, hit.transform.position);
    }

    // --------------------------------------------------------
    //  CLASH LOGIC
    // --------------------------------------------------------

    // ClashHandler.cs — método ExecuteClash completo
    void ExecuteClash(RioTutteEnemy enemy, Vector3 enemyPosition)
    {
        Vector3 clashDir = (enemyPosition - transform.position).normalized;
        clashDir.y = 0f;
        if (clashDir == Vector3.zero) clashDir = transform.forward;

        KnockbackResult result = KnockbackResolver.Resolve(
            AttackType.Clash,
            player.combat.endurance,
            enemy.endurance,
            resolverConfig
        );

        enemy.ReceiveKnockback(clashDir, result.ForceOnTarget);

        if (result.ForceOnSelf > 0.01f)
            player.knockbackHandler.ReceiveKnockback(-clashDir, result.ForceOnSelf);

        _cooldownTimer = clashCooldown;

        Debug.Log($"[Clash] PlayerEnd:{player.combat.endurance} EnemyEnd:{enemy.endurance} " +
                  $"→ forceEnemy:{result.ForceOnTarget:F1} forceSelf:{result.ForceOnSelf:F1}");
    }
}