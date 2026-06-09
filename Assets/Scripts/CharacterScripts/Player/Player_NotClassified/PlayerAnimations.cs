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
    public float accelToWalk     = 8f;
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
        animator.SetBool("isMoving",      player.StateMachineController.CurrentState == State.Moving
                                       || player.StateMachineController.CurrentState == State.Running
                                       || player.StateMachineController.CurrentState == State.Deenergized);
        animator.SetBool("isRunning",     player.StateMachineController.CurrentState == State.Running
                                       || player.StateMachineController.CurrentState == State.PunchRunning);
        animator.SetBool("isGliding",     player.StateMachineController.CurrentState == State.Gliding);
        animator.SetBool("isKnockedback", player.KnockbackHandler.ShowKnockbackAnim);
        animator.SetBool("isDead",        player.Combat.HP <= 0);
        animator.SetBool("IdleFront",     player.StateMachineController.CurrentState == State.Idle
                                       && Mathf.Abs(player.Movement.LastDirection.x) > 0.1f);
    }

    void UpdateBlendSpeed()
    {
        bool isWalking = player.StateMachineController.CurrentState == State.Moving;
        bool isRunning = player.StateMachineController.CurrentState == State.Running
                      || player.StateMachineController.CurrentState == State.PunchRunning;

        if (!isWalking && !isRunning)
        {
            _blendSpeed = 0f; // frenado inmediato
            return;
        }

        // Sin tecla de correr: acelera suavemente hasta walkThreshold
        if (isWalking)
        {
            _blendSpeed = Mathf.MoveTowards(_blendSpeed, walkThreshold, accelToWalk * Time.deltaTime);
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
