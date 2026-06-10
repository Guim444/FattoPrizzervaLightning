using System.Collections;
using UnityEngine;

/// <summary>
/// RioTutte attack behaviours.
///
/// SINGLE RESPONSIBILITY (SRP):
///   Execute and manage the lifecycle of each individual attack.
///   Does NOT decide WHEN to attack — that is RioTutteEnemy.ExecuteAttack().
///
/// ATTACKS:
///   DashGrab  — Fast dash towards the player; on contact, grabs and hits them.
///   SuperDash — Dash in the opposite direction to the player (Invulnerable layer during it).
///
/// PROJECT CONVENTION:
///   Durations hardcoded as serializable fields — .length is never read at runtime.
///   SetTrigger is only called at the exact moment of the event (grab start, hit).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class RioTutteAttacks : MonoBehaviour, IAttacker
{
    // ── DURATIONS ────────────────────────────────────────────────
    [Header("Attack Durations (hardcoded, no .length)")]
    [Tooltip("Maximum time for DashGrab before it cancels automatically.")]
    public float dashGrabTimeout = 2f;

    [Tooltip("Time RioTutte holds the player before hitting.")]
    public float grabHoldDuration = 0.8f;

    [Tooltip("Total duration of SuperDash in seconds.")]
    public float superDashDuration = 0.73f;

    // ── SPEEDS ───────────────────────────────────────────────────
    [Header("Speeds")]
    public float dashGrabSpeed  = 7.5f;
    public float superDashSpeed = 15f;

    // ── PUBLIC STATE (read by RioTutteAnimatorDriver and RioTutteEnemy) ─
    public bool IsAttacking      { get; private set; }
    public bool IsUsingDashGrab  { get; private set; }
    public bool IsGrabbing       { get; private set; }
    public bool IsUsingSuperDash { get; private set; }

    // ── PRIVATE ──────────────────────────────────────────────────
    private CharacterController _cc;
    private Animator            _anim;
    private RioTutteEnemy       _enemy;
    private PlayerController    _player;

    private Vector3   _dashDirection;
    private int       _originalLayer;
    private Coroutine _activeCoroutine;

    // ────────────────────────────────────────────────────────────
    //  INIT
    // ────────────────────────────────────────────────────────────

    void Awake()
    {
        _cc            = GetComponent<CharacterController>();
        _anim          = GetComponentInChildren<Animator>();
        _enemy         = GetComponent<RioTutteEnemy>();
        _originalLayer = gameObject.layer;
    }

    void Start()
    {
        _player = FindFirstObjectByType<PlayerController>();
        if (_player == null)
            Debug.LogWarning("[RioTutteAttacks] PlayerController not found in scene.");
    }

    // ────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Starts the DashGrab: RioTutte lunges towards the player to grab them.
    /// Called from RioTutteEnemy.ExecuteAttack().
    /// </summary>
    public void StartDashGrab()
    {
        if (IsAttacking || _player == null) return;

        Vector3 toPlayer = _player.transform.position - transform.position;
        _dashDirection   = new Vector3(toPlayer.x, 0f, toPlayer.z).normalized;

        IsAttacking     = true;
        IsUsingDashGrab = true;
        _activeCoroutine = StartCoroutine(DashGrabRoutine());
    }

    /// <summary>
    /// Starts the SuperDash: RioTutte lunges in the opposite direction to the player.
    /// Called from RioTutteEnemy.ExecuteAttack().
    /// </summary>
    /// <param name="lastMoveDir">RioTutte's last movement direction (used to calculate the opposite).</param>
    public void StartSuperDash(Vector3 lastMoveDir)
    {
        if (IsAttacking) return;

        // Tries to go in the opposite direction of the last movement (which was towards the player)
        Vector3 preferred = -lastMoveDir.normalized;
        if (!TryGetValidSuperDashDir(preferred, out Vector3 dashDir))
        {
            bool found = false;
            for (int i = 0; i < 20 && !found; i++)
            {
                float   angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                Vector3 rnd   = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                found = TryGetValidSuperDashDir(rnd, out dashDir);
            }
            if (!found) return; // No free space: cancel silently
        }

        IsAttacking      = true;
        IsUsingSuperDash = true;
        gameObject.layer = LayerMask.NameToLayer("Invulnerable");
        _activeCoroutine = StartCoroutine(SuperDashRoutine(dashDir));
    }

    /// <summary>
    /// Executes the grab sequence when colliding with the player during a DashGrab.
    /// Called from RioTutteEnemy.OnTriggerEnter / OnControllerColliderHit.
    /// </summary>
    public void GrabPlayer()
    {
        if (IsGrabbing || _player == null) return;

        // Position the player right beside RioTutte
        int     side    = _player.transform.position.x > transform.position.x ? 1 : -1;
        Vector3 grabPos = new Vector3(transform.position.x + side * _cc.radius,
                                      _player.transform.position.y,
                                      transform.position.z);
        _player.cc.Move(grabPos - _player.transform.position);
        _player.canMove  = false;
        IsUsingDashGrab  = false; // dashGrab → false in animator: prevents AnyState from interrupting Grabbing
        IsGrabbing       = true;

        // Cancel the dash coroutine and start the hit coroutine
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(GrabHitRoutine());
    }

    /// <summary>
    /// Interrupts the current attack (e.g.: RioTutte receives knockback during the dash).
    /// </summary>
    public void CancelAttack()
    {
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        if (IsUsingSuperDash) gameObject.layer = _originalLayer;
        if (_player != null && IsGrabbing) _player.canMove = true;
        ResetState();
    }

    // ────────────────────────────────────────────────────────────
    //  COROUTINES
    // ────────────────────────────────────────────────────────────

    IEnumerator DashGrabRoutine()
    {
        _enemy.FlipCharacter(_dashDirection);
        float elapsed = 0f;

        while (elapsed < dashGrabTimeout)
        {
            if (_cc.enabled)
                _cc.Move(_dashDirection * dashGrabSpeed * Time.deltaTime);

            // Distance-based detection: does not depend on layers or static CC callbacks.
            Vector3 toPlayer2D = _player.transform.position - transform.position;
            toPlayer2D.y = 0f;
            float grabDist = _cc.radius + _player.cc.radius + 0.2f;
            if (toPlayer2D.magnitude <= grabDist)
            {
                GrabPlayer();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Timeout without catching the player
        ResetState();
        _enemy.ResetAttackCooldown();
    }

    IEnumerator GrabHitRoutine()
    {
        // Wait for the hold duration before hitting.
        // GrabPunch is fired AFTER so the animator is already in Grabbing
        // when it receives the trigger and can use it to exit to the next state.
        yield return new WaitForSeconds(grabHoldDuration);

        _anim.SetTrigger("GrabPunch");

        if (_player != null)
        {
            Vector3 pushDir = _player.transform.position - transform.position;
            pushDir.y = 0f;
            if (pushDir == Vector3.zero) pushDir = transform.forward;

            // Fuerza garantizada superior al endurance del player → siempre vuela
            _player.KnockbackHandler.ReceiveEnemyKnockback(
                pushDir.normalized,
                _enemy.attackKnockbackBase * 2f
            );
            _player.Combat.TakeDamage(1);

            if (_player.Combat.HP > 0)
                _player.canMove = true;
        }

        ResetState();
        _enemy.ResetAttackCooldown();
    }

    IEnumerator SuperDashRoutine(Vector3 direction)
    {
        _enemy.FlipCharacter(direction);
        float elapsed = 0f;

        while (elapsed < superDashDuration)
        {
            if (_cc.enabled)
                _cc.Move(direction * superDashSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        gameObject.layer = _originalLayer;
        IsUsingSuperDash = false;
        ResetState();
        _enemy.ResetAttackCooldown();
    }

    // ────────────────────────────────────────────────────────────
    //  HELPERS
    // ────────────────────────────────────────────────────────────

    void ResetState()
    {
        IsAttacking      = false;
        IsUsingDashGrab  = false;
        IsGrabbing       = false;
        IsUsingSuperDash = false;
    }

    /// <summary>
    /// Checks whether a wall is blocking the SuperDash direction.
    /// </summary>
    bool TryGetValidSuperDashDir(Vector3 dir, out Vector3 result)
    {
        float dashDist = superDashSpeed * superDashDuration;
        if (dir == Vector3.zero || Physics.Raycast(transform.position, dir.normalized, dashDist, LayerMask.GetMask("Wall")))
        {
            result = Vector3.zero;
            return false;
        }
        result = dir.normalized;
        return true;
    }
}
