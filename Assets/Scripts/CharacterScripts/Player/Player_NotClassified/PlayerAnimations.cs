using UnityEngine;

public class PlayerAnimations : MonoBehaviour
{
    public static PlayerAnimations instance;
    public Animator animator;
    public PlayerController player;

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

        float speed = player.currentState switch
        {
            State.Idle => 0f,
            State.Moving => .5f,
            State.Running => 1f,
            State.Tired => 0.25f,
            _ => 0f
        };
        animator.SetFloat("Speed", speed);
        animator.SetBool("isMoving", player.currentState == State.Moving
                                     || player.currentState == State.Running
                                     || player.currentState == State.Tired);
        animator.SetBool("isRunning", player.currentState == State.Running
                                      || player.currentState == State.PunchRunning);
        if (player.currentState == State.Punching
            || player.currentState == State.PunchRunning)
            animator.SetTrigger("isPunching");
        animator.SetBool("isTired", player.currentState == State.Tired);
        animator.SetBool("isGliding", player.currentState == State.Gliding);
        animator.SetBool("isKnockedback", player.currentState == State.Knockedback);
        animator.SetBool("isDead", player.combat.HP <= 0);
    }
}
