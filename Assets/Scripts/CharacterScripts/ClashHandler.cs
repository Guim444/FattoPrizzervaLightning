using UnityEngine;

/// <summary>
/// Detects direct physical contact (clash) between the player and RioTutte
/// and delegates force calculations to KnockbackResolver.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class ClashHandler : MonoBehaviour
{
    // --------------------------------------------------------
    //  CONFIGURATION
    // --------------------------------------------------------

    [Header("References")]
    public PlayerController player;

    [Tooltip("Optional. If null, KnockbackResolver default values are used.")]
    public KnockbackResolverConfig resolverConfig;

    [Header("Game Design: Clash")]
    [Tooltip("Minimum time between two consecutive clashes.")]
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

        // Pure Running only. In PunchRunning, knockback is handled by ExecuteAttack (animation event)
        // — if both fired, RioTutte would receive double knockback.
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
