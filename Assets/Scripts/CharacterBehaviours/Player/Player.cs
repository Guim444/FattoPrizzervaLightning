using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    private GeneralMovement movement;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        movement = new GeneralMovement(rb);
    }
    public void OnMovement(InputValue inputValue)
    {
        Vector2 moveInput = inputValue.Get<Vector2>();
        movement.Move(new Vector2(moveInput.y, -moveInput.x));
    }
}