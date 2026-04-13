using UnityEngine;

/// <summary>
/// Gestiona el knockback del jugador.
/// Ya no calcula fuerzas: las recibe ya resueltas desde CombatAttackHandler
/// o ClashHandler vía ReceiveKnockback().
///
/// CAMBIOS respecto a la versión anterior:
///   - Toda la física delegada a KnockbackPhysicsBody (struct compartido).
///   - ReceiveKnockback(direction, force) recibe fuerza directa, sin lookup interno.
///   - La masa se auto-configura desde el endurance del jugador en Awake().
///   - Eliminado el sistema de TypeOfDamage / pushForce* — esa lógica
///     vive ahora en KnockbackResolver + KnockbackResolverConfig.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerKnockbackHandler : MonoBehaviour, IKnockbackable
{
    // --------------------------------------------------------
    //  CONFIGURACIÓN
    // --------------------------------------------------------

    [Header("Physics")]
    [Tooltip("Si true, la masa se calcula automáticamente desde el endurance del jugador.")]
    public bool autoMassFromEndurance = true;

    [Tooltip("Masa manual (solo si autoMassFromEndurance = false).")]
    public float manualMass = 1f;

    [Tooltip("Fricción en suelo. Mayor = parada más brusca y con más peso.")]
    public float groundFriction = 5f;

    [Tooltip("Fricción en aire. Menor = más planeo.")]
    public float airFriction = 1.5f;

    [Tooltip("Umbral de velocidad para considerar el knockback terminado.")]
    public float stopThreshold = 0.15f;

    // --------------------------------------------------------
    //  ESTADO (visible en Inspector para debug)
    // --------------------------------------------------------

    [Header("Debug (read-only)")]
    [SerializeField] private bool _isKnockedBack;
    [SerializeField] private Vector3 _debugVelocity;

    // --------------------------------------------------------
    //  PRIVATE
    // --------------------------------------------------------

    private CharacterController      _cc;
    private PlayerController         _player;
    private KnockbackPhysicsBody     _body;

    /// <summary>True mientras el knockback esté activo.</summary>
    public bool IsKnockedBack => _body.IsActive;

    /// <summary>
    /// True solo cuando el knockback proviene de un ataque de enemigo.
    /// False para recoil de puñetazo propio o colisión (ClashHandler).
    /// Lo lee PlayerAnimations para decidir si muestra la animación.
    /// </summary>
    public bool ShowKnockbackAnim { get; private set; }

    // --------------------------------------------------------
    //  INIT
    // --------------------------------------------------------

    void Awake()
    {
        _cc     = GetComponent<CharacterController>();
        _player = GetComponent<PlayerController>();

        _body = KnockbackPhysicsBody.Default;
        _body.groundFriction = groundFriction;
        _body.airFriction    = airFriction;
        _body.stopThreshold  = stopThreshold;
    }

    void Start()
    {
        // Auto-masa desde endurance (necesita que PlayerCombat ya esté inicializado)
        if (autoMassFromEndurance && _player.combat != null)
            _body.mass = Mathf.Max(0.6f, KnockbackPhysicsBody.MassFromEndurance(_player.combat.endurance));
        else
            _body.mass = manualMass;
    }

    // --------------------------------------------------------
    //  IKnockbackable
    // --------------------------------------------------------

    /// <summary>
    /// Recibe knockback ya resuelto (fuerza directa).
    /// Llamado por CombatAttackHandler (reacción del atacante)
    /// o por RioTutteEnemy (cuando ataca al player).
    ///
    /// NOTA: el segundo parámetro se llama 'preResolvedForce' para dejar
    /// claro que NO es el endurance del atacante — ya es la fuerza final.
    /// La interfaz IKnockbackable mantiene la firma (Vector3, int) para
    /// compatibilidad; usamos un cast a float internamente.
    /// </summary>
    public void ReceiveKnockback(Vector3 direction, float force)
    {
        ShowKnockbackAnim = false;
        ApplyKnockback(direction, force);
    }

    /// <summary>
    /// Llamar solo desde RioTutteEnemy (ataque directo al player).
    /// Activa la animación de knockback además de la física.
    /// </summary>
    public void ReceiveEnemyKnockback(Vector3 direction, float force)
    {
        ShowKnockbackAnim = true;
        ApplyKnockback(direction, force);
    }

    private void ApplyKnockback(Vector3 direction, float force)
    {
        // Recalcula masa por si el endurance cambió en runtime
        if (autoMassFromEndurance && _player.combat != null)
            _body.mass = Mathf.Max(0.6f, KnockbackPhysicsBody.MassFromEndurance(_player.combat.endurance));

        _body.Receive(direction, force);
        _player.currentState = State.Knockedback;
        StateMachine.SetState(State.Knockedback);
    }

    // --------------------------------------------------------
    //  UPDATE LOOP
    // --------------------------------------------------------

    /// <summary>
    /// Llamado cada frame desde PlayerController.Update().
    /// </summary>
    public void HandleKnockback()
    {
        if (!_body.IsActive) return;

        Vector3 delta = _body.Tick(_player.isGrounded, Time.deltaTime);

        if (_cc.enabled)
            _cc.Move(delta);

        // Bloquea el movimiento normal durante knockback
        _player.movement.LastDirection = Vector3.zero;
        _player.movement.CurrentSpeed  = Vector3.zero;

        // Actualiza debug
        _isKnockedBack   = _body.IsActive;
        _debugVelocity   = _body.Velocity;

        // Cuando termina el knockback, vuelve a Idle
        if (!_body.IsActive)
        {
            ShowKnockbackAnim = false;
            _player.currentState = State.Idle;
            StateMachine.SetState(State.Idle);
        }
    }

    /// <summary>
    /// Cancela el knockback inmediatamente (ej: muerte del jugador).
    /// </summary>
    public void CancelKnockback() => _body.Cancel();
}