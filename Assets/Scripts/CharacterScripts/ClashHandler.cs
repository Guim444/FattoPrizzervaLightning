using UnityEngine;

/// <summary>
/// Detecta el contacto físico directo (clash) entre el player y RioTutte
/// y delega el cálculo de fuerzas a KnockbackResolver.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ClashHandler : MonoBehaviour
{
    // --------------------------------------------------------
    //  CONFIGURACIÓN
    // --------------------------------------------------------

    [Header("References")]
    public PlayerController player;

    [Tooltip("Opcional. Si es null se usan los valores por defecto del KnockbackResolver.")]
    public KnockbackResolverConfig resolverConfig;

    [Header("Game Design: Clash")]
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
        if (_cooldownTimer > 0f) return;
        if (!player.canMove) return;

        // Solo Running puro. En PunchRunning el knockback lo gestiona ExecuteAttack (animation event)
        // — si dispararan ambos, RioTutte recibiría doble knockback.
        if (player.currentState != State.Running) return;

        var enemy = hit.collider.GetComponent<RioTutteEnemy>();
        if (enemy == null) return;

        ExecuteClash(enemy, hit.transform.position);
    }

    // --------------------------------------------------------
    //  CLASH LOGIC
    // --------------------------------------------------------

    void ExecuteClash(RioTutteEnemy enemy, Vector3 enemyPosition)
    {
        Vector3 clashDir = (enemyPosition - transform.position).normalized;
        clashDir.y = 0f;
        if (clashDir == Vector3.zero) clashDir = transform.forward;

        KnockbackResult result = KnockbackResolver.Resolve(
            AttackType.Clash,
            player.combat.endurance,
            resolverConfig
        );

        enemy.ReceiveKnockback(clashDir, result.ForceOnTarget);

        if (result.ForceOnSelf > 0.01f)
            player.knockbackHandler.ReceiveKnockback(-clashDir, result.ForceOnSelf);

        _cooldownTimer = clashCooldown;
    }
}
