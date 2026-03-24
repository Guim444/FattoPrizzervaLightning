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

        animator.SetBool("isMoving", player.currentState == State.Moving);
        animator.SetBool("isRunning", player.currentState == State.Running);
        animator.SetBool("isPunching", player.currentState == State.Punching
                                       || player.currentState == State.PunchRunning);
        animator.SetBool("isTired", player.currentState == State.Tired);
        animator.SetBool("isGliding", player.currentState == State.Gliding);
    }
}
