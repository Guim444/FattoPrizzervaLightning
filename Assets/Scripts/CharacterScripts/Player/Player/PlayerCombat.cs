using UnityEngine;

/// <summary>
/// Responsabilidad única: gestionar el estado de combate del jugador.
/// Implementa IDamageable. Maneja HP, muerte y el cooldown de puñetazos normales.
/// Los parámetros de daño de cada golpe viven aquí porque son datos de combate,
/// aunque su aplicación ocurre dentro de los estados correspondientes.
/// </summary>
public class PlayerCombat : MonoBehaviour, IDamageable
{
    // ==================================================
    // DAMAGE VALUES
    // ==================================================
    [Header("Combat: Damage Values")]
    public float basicPunchDamage;
    public float thrustDamage1;
    public float thrustDamage2;
    public float thrustDamage3;

    /// <summary>Daño base que aplica el jugador actualmente (puede modificarse por estados).</summary>
    public float playerDamage;

    // ==================================================
    // COMBAT STATE
    // ==================================================
    [Header("Combat: State")]
    public int damageBoost = 0;
    public int endurance   = 0;

    // ==================================================
    // PUNCH COOLDOWN
    // ==================================================
    [Header("Combat: Punch Cooldown")]
    public float normalPunchTimer    = 0f;
    public float normalPunchCooldown = 0.5f;

    // ==================================================
    // STAMINA COSTS
    // ==================================================
    [Header("Game Design: Stamina Costs")]
    public float punchStaminaCostPhase0        = 2f;
    public float punchRunningStaminaCostPhase1  = 2f;
    public float punchRunningStaminaCostPhase2  = 4f;
    public float punchRunningStaminaCostPhase3  = 6f;

    // ==================================================
    // PRIVATE REFS
    // ==================================================
    private PlayerController _player;

    // ==================================================
    // IDamageable
    // ==================================================
    public float HP { get; set; }

    // ==================================================
    // INITIALIZATION
    // ==================================================

    /// <summary>Inicializa la referencia al orquestador y establece la HP inicial.</summary>
    public void Initialize(PlayerController player)
    {
        _player = player;
        HP = 2;
    }

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Recibe daño. Muere si HP llega a 0.
    /// </summary>
    public void TakeDamage(int dmg)
    {
        HP = Mathf.Max(0, HP - dmg);
        if (HP == 0) Die();
    }

    /// <summary>
    /// Ejecuta la secuencia de muerte del jugador.
    /// </summary>
    public void Die()
    {
        _player.canMove      = false;
        _player.animator.speed = 1;
        _player.animator.SetBool("isDead", true);
        // TODO: conectar con BattleManager cuando exista
    }

    /// <summary>
    /// Decrementa el timer de cooldown del puñetazo normal.
    /// Debe llamarse cada Update().
    /// </summary>
    public void UpdatePunchCooldown()
    {
        if (normalPunchTimer > 0)
            normalPunchTimer -= Time.deltaTime;
    }
}
