using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    public static PlayerAnimations instance;
    public Animator animator;
    public PlayerController player;

    public bool pauseAnimatorControl = false;

    // ── BLEND TREE THRESHOLDS ────────────────────────────────────
    [Header("Blend Tree: Umbrales de velocidad")]
    public float walkThreshold   = 4.4f;
    public float runThreshold    = 9f;
    public float runMidThreshold = 11f;
    public float runMaxThreshold = 13.5f;

    // ── ACELERACIÓN POR TRAMO (unidades/segundo con Time.deltaTime) ──
    [Header("Blend Tree: Aceleración por tramo (u/s)")]
    public float accelWalkToRun  = 3.5f;
    public float accelRunToMid   = 4.5f;
    public float accelMidToMax   = 6.0f;

    public float BlendSpeed => _blendSpeed;
    private float _blendSpeed;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (player == null || animator == null) return;
        if (pauseAnimatorControl) return;

        UpdateBlendSpeed();

        animator.SetFloat("Speed",        _blendSpeed);
        animator.SetBool("isMoving",      player.currentState == State.Moving
                                       || player.currentState == State.Running
                                       || player.currentState == State.Deenergized);
        animator.SetBool("isRunning",     player.currentState == State.Running
                                       || player.currentState == State.PunchRunning);
        animator.SetBool("isGliding",     player.currentState == State.Gliding);
        animator.SetBool("isKnockedback", player.knockbackHandler.ShowKnockbackAnim);
        animator.SetBool("isDead",        player.combat.HP <= 0);
        animator.SetBool("IdleFront",     Mathf.Abs(player.movement.LastDirection.x) > 0.1f);
    }

    void UpdateBlendSpeed()
    {
        bool isWalking = player.currentState == State.Moving;
        bool isRunning = player.currentState == State.Running
                      || player.currentState == State.PunchRunning;

        if (!isWalking && !isRunning)
        {
            _blendSpeed = 0f; // frenado inmediato
            return;
        }

        // Sin tecla de correr: fijo en walk
        if (isWalking)
        {
            _blendSpeed = walkThreshold;
            return;
        }

        // Tecla de correr: salto inmediato a walk si venía de parado
        if (_blendSpeed < walkThreshold)
            _blendSpeed = walkThreshold;

        // Aceleración progresiva por tramo
        float accel = _blendSpeed < runThreshold    ? accelWalkToRun
                    : _blendSpeed < runMidThreshold  ? accelRunToMid
                    :                                  accelMidToMax;

        _blendSpeed = Mathf.Min(_blendSpeed + accel * Time.deltaTime, runMaxThreshold);
    }
}
