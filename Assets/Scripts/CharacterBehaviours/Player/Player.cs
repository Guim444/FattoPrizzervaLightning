using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    public GeneralMovement movement;
    public Rigidbody rb;

    void Awake()
    {
        movement.rb = rb;
    }
    public void OnMovement(InputValue inputValue)
    {
        Vector2 moveInput = inputValue.Get<Vector2>();
        movement.Move(new Vector2(moveInput.y, -moveInput.x));
    }
}