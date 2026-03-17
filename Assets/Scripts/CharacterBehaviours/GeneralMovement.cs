using UnityEngine;

[System.Serializable]
public class GeneralMovement : MonoBehaviour
{
    public float moveSpeed;
    public Rigidbody rb;

    public GeneralMovement(Rigidbody rigidbody)
    {
        rb = rigidbody;
    }

    public void Move(Vector2 input)
    {
        Vector3 direction = new Vector3(input.x, 0f, input.y);
        Vector3 velocity = direction * moveSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }
}