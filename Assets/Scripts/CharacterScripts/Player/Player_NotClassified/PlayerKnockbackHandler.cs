using UnityEngine;

/// <summary>
/// Manages the player's knockback.
/// No longer calculates forces: receives them pre-resolved from CombatAttackHandler
/// or ClashHandler via ReceiveKnockback().
///
/// CHANGES from the previous version:
///   - All physics delegated to KnockbackPhysicsBody (shared struct).
///   - ReceiveKnockback(direction, force) receives direct force, no internal lookup.
///   - Mass is auto-configured from the player's endurance in Awake().
///   - Removed TypeOfDamage / pushForce* system — that logic
///     now lives in KnockbackResolver + KnockbackResolverConfig.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerKnockbackHandler : MonoBehaviour, IKnockbackable
{
    // --------------------------------------------------------
    //  CONFIGURATION
    // --------------------------------------------------------

    [Header("Physics")]
    [Tooltip("If true, mass is automatically calculated from the player's endurance.")]
    public bool autoMassFromEndurance = true;

    [Tooltip("Manual mass (only if autoMassFromEndurance = false).")]
    public float manualMass = 1f;

    [Tooltip("Ground friction. Higher = harder, heavier stop.")]
    public float groundFriction = 5f;

    [Tooltip("Air friction. Lower = more glide.")]
    public float airFriction = 1.5f;

    [Tooltip("Speed threshold below which knockback is considered finished.")]
    public float stopThreshold = 0.15f;

    // --------------------------------------------------------
    //  STATE (visible in Inspector for debug)
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

    /// <summary>True while knockback is active.</summary>
    public bool IsKnockedBack => _body.IsActive;

    /// <summary>
    /// True only when knockback comes from an enemy attack.
    /// False for own punch recoil or collision (ClashHandler).
    /// Read by PlayerAnimations to decide whether to show the animation.
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
        // Auto-mass from endurance (requires PlayerCombat to already be initialized)
        if (autoMassFromEndurance && _player.Combat != null)
            _body.mass = Mathf.Max(0.6f, KnockbackPhysicsBody.MassFromEndurance(_player.Combat.endurance));
        // Auto-masa desde endurance (necesita que PlayerCombat ya esté inicializado)
        if (autoMassFromEndurance && _player.Combat != null)
            _body.mass = Mathf.Max(0.6f, KnockbackPhysicsBody.MassFromEndurance(_player.Combat.endurance));
        else
            _body.mass = manualMass;
    }

    // --------------------------------------------------------
    //  IKnockbackable
    // --------------------------------------------------------

    /// <summary>
    /// Receives pre-resolved knockback (direct force).
    /// Called by CombatAttackHandler (attacker reaction)
    /// or by RioTutteEnemy (when attacking the player).
    ///
    /// NOTE: the second parameter is called 'preResolvedForce' to make clear
    /// that it is NOT the attacker's endurance — it is the final force.
    /// The IKnockbackable interface keeps the (Vector3, int) signature for
    /// compatibility; we cast to float internally.
    /// </summary>
    public void ReceiveKnockback(Vector3 direction, float force)
    {
        ShowKnockbackAnim = false;
        ApplyKnockback(direction, force);
    }

    /// <summary>
    /// Call only from RioTutteEnemy (direct attack on player).
    /// Activates the knockback animation in addition to the physics.
    /// </summary>
    public void ReceiveEnemyKnockback(Vector3 direction, float force)
    {
        ShowKnockbackAnim = true;
        ApplyKnockback(direction, force);
    }

    private void ApplyKnockback(Vector3 direction, float force)
    {
        // Recalculate mass in case endurance changed at runtime
        if (autoMassFromEndurance && _player.Combat != null)
            _body.mass = Mathf.Max(0.6f, KnockbackPhysicsBody.MassFromEndurance(_player.Combat.endurance));
        // Recalcula masa por si el endurance cambió en runtime
        if (autoMassFromEndurance && _player.Combat != null)
            _body.mass = Mathf.Max(0.6f, KnockbackPhysicsBody.MassFromEndurance(_player.Combat.endurance));

        _body.Receive(direction, force);

        //_player.currentState = State.Knockedback;
        //StateMachine.SetState(State.Knockedback);

        _player.StateMachineController.IntentBuffer.Add(PlayerStateRequest.Knockback);

    }

    // --------------------------------------------------------
    //  UPDATE LOOP
    // --------------------------------------------------------

    /// <summary>
    /// Called every frame from PlayerController.Update().
    /// </summary>
    public void HandleKnockback()
    {
        if (!_body.IsActive) return;

        Vector3 delta = _body.Tick(_player.isGrounded, Time.deltaTime);

        if (_cc.enabled)
            _cc.Move(delta);

        // Block normal movement during knockback
        _player.Movement.LastDirection = Vector3.zero;
        _player.Movement.CurrentSpeed  = Vector3.zero;
        // Bloquea el movimiento normal durante knockback
        _player.Movement.LastDirection = Vector3.zero;
        _player.Movement.CurrentSpeed  = Vector3.zero;

        // Update debug
        _isKnockedBack   = _body.IsActive;
        _debugVelocity   = _body.Velocity;

        // When knockback ends, return to Idle
        if (!_body.IsActive)
        {
            ShowKnockbackAnim = false;

            //_player.currentState = State.Idle;
            //StateMachine.SetState(State.Idle);
            _player.StateMachineController.IntentBuffer.Add(PlayerStateRequest.Idle);
        }
    }

    /// <summary>
    /// Cancels knockback immediately (e.g.: player death).
    /// </summary>
    public void CancelKnockback() => _body.Cancel();
}
