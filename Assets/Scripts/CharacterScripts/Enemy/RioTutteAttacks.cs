using System.Collections;
using UnityEngine;

/// <summary>
/// Comportamientos de ataque de RioTutte.
///
/// RESPONSABILIDAD ÚNICA (SRP):
///   Ejecutar y gestionar el ciclo de vida de cada ataque individual.
///   No decide CUÁNDO atacar — eso lo hace RioTutteEnemy.ExecuteAttack().
///
/// ATAQUES:
///   DashGrab  — Dash rápido hacia el player; al contactar, lo agarra y golpea.
///   SuperDash — Dash en dirección opuesta al player (capa Invulnerable durante él).
///
/// CONVENCIÓN DEL PROYECTO:
///   Durations hardcodeadas como campos serializables — nunca se lee .length en runtime.
///   SetTrigger solo se llama en el momento exacto del evento (inicio de grab, golpe).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class RioTutteAttacks : MonoBehaviour, IAttacker
{
    // ── DURATIONS ────────────────────────────────────────────────
    [Header("Attack Durations (hardcoded, no .length)")]
    [Tooltip("Tiempo máximo del DashGrab antes de cancelarse automáticamente.")]
    public float dashGrabTimeout = 2f;

    [Tooltip("Tiempo que RioTutte sostiene al player antes de golpear.")]
    public float grabHoldDuration = 0.8f;

    [Tooltip("Duración total del SuperDash en segundos.")]
    public float superDashDuration = 0.73f;

    // ── SPEEDS ───────────────────────────────────────────────────
    [Header("Speeds")]
    public float dashGrabSpeed  = 7.5f;
    public float superDashSpeed = 15f;

    // ── PUBLIC STATE (leído por RioTutteAnimatorDriver y RioTutteEnemy) ─
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
        _player = FindObjectOfType<PlayerController>();
        if (_player == null)
            Debug.LogWarning("[RioTutteAttacks] No se encontró PlayerController en la escena.");
    }

    // ────────────────────────────────────────────────────────────
    //  API PÚBLICA
    // ────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia el DashGrab: RioTutte se lanza hacia el player para agarrarlo.
    /// Llamado desde RioTutteEnemy.ExecuteAttack().
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
    /// Inicia el SuperDash: RioTutte se lanza en dirección opuesta al player.
    /// Llamado desde RioTutteEnemy.ExecuteAttack().
    /// </summary>
    /// <param name="lastMoveDir">Última dirección de movimiento de RioTutte (para calcular opuesto).</param>
    public void StartSuperDash(Vector3 lastMoveDir)
    {
        if (IsAttacking) return;

        // Intenta ir en dirección opuesta al último movimiento (que era hacia el player)
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
            if (!found) return; // Sin espacio libre: cancela silenciosamente
        }

        IsAttacking      = true;
        IsUsingSuperDash = true;
        gameObject.layer = LayerMask.NameToLayer("Invulnerable");
        _activeCoroutine = StartCoroutine(SuperDashRoutine(dashDir));
    }

    /// <summary>
    /// Ejecuta la secuencia de agarre al colisionar con el player durante un DashGrab.
    /// Llamado desde RioTutteEnemy.OnTriggerEnter / OnControllerColliderHit.
    /// </summary>
    public void GrabPlayer()
    {
        if (IsGrabbing || _player == null) return;

        // Posiciona al player justo al lado de RioTutte
        int     side    = _player.transform.position.x > transform.position.x ? 1 : -1;
        Vector3 grabPos = new Vector3(transform.position.x + side * _cc.radius,
                                      _player.transform.position.y,
                                      transform.position.z);
        _player.cc.Move(grabPos - _player.transform.position);
        _player.canMove  = false;
        IsUsingDashGrab  = false; // dashGrab → false en el animator: evita que AnyState interrumpa Grabbing
        IsGrabbing       = true;

        // Cancela el coroutine del dash y lanza el de golpear
        if (_activeCoroutine != null) StopCoroutine(_activeCoroutine);
        _activeCoroutine = StartCoroutine(GrabHitRoutine());
    }

    /// <summary>
    /// Interrumpe el ataque en curso (ej: RioTutte recibe knockback durante el dash).
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

            // Detección de contacto con OverlapSphere (convención del proyecto).
            // RioTutte no tiene trigger collider propio, por eso no usamos OnTriggerEnter.
            Collider[] hits = Physics.OverlapSphere(transform.position, _cc.radius + 0.35f,
                                                    LayerMask.GetMask("Player"));
            if (hits.Length > 0)
            {
                GrabPlayer();
                yield break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Timeout sin atrapar al player
        ResetState();
        _enemy.ResetAttackCooldown();
    }

    IEnumerator GrabHitRoutine()
    {
        // Esperamos la duración del agarre antes de golpear.
        // GrabPunch se dispara DESPUÉS para que el animator ya esté en Grabbing
        // cuando recibe el trigger y pueda usarlo para salir al estado siguiente.
        yield return new WaitForSeconds(grabHoldDuration);

        _anim.SetTrigger("GrabPunch");

        if (_player != null)
        {
            Vector3 pushDir = _player.transform.position - transform.position;
            pushDir.y = 0f;
            if (pushDir == Vector3.zero) pushDir = transform.forward;

            // Fuerza garantizada superior al endurance del player → siempre vuela
            _player.knockbackHandler.ReceiveEnemyKnockback(
                pushDir.normalized,
                _enemy.attackKnockbackBase * 2f
            );
            _player.combat.TakeDamage(1);

            if (_player.combat.HP > 0)
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
    /// Comprueba si hay una pared bloqueando la dirección del SuperDash.
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
