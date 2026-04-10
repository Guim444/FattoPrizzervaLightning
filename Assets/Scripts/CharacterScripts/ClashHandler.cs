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
    public PlayerController player;

    [Header("Game Design: Clash (contacto físico — mucho menor que un puñetazo)")]
    [Tooltip("Fuerza base que recibe el enemigo al chocar. Tunearlo en Inspector.")]
    public float baseKnockbackToEnemy = 1.5f;

    [Tooltip("Fuerza extra por cada punto de Endurance del player.")]
    public float enduranceKnockbackMultiplier = 0.5f;

    [Tooltip("Fuerza de retroceso que recibe el player al chocar.")]
    public float knockbackToSelf = 1.0f;

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
    // ClashHandler.cs — solo ExecuteClash(), el resto queda igual
    void ExecuteClash(RioTutteEnemy enemy, Vector3 enemyPosition)
    {
        Vector3 clashDir = (enemyPosition - transform.position).normalized;
        clashDir.y = 0f;
        if (clashDir == Vector3.zero) clashDir = transform.forward;

        float forceOnEnemy = baseKnockbackToEnemy
                           + player.combat.endurance * enduranceKnockbackMultiplier;

        enemy.ReceiveKnockback(clashDir, forceOnEnemy);

        if (knockbackToSelf > 0.01f)
            player.knockbackHandler.ReceiveKnockback(-clashDir, knockbackToSelf);

        _cooldownTimer = clashCooldown;

        Debug.Log($"[Clash] playerEnd:{player.combat.endurance} " +
                  $"→ forceEnemy:{forceOnEnemy:F1} forceSelf:{knockbackToSelf:F1}");
    }
}