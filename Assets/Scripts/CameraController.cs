using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Mouse Look Settings")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private float verticalRotationLimit = 80.0f;
    
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float sprintMultiplier = 2.0f;
    
    private float rotationX = 0f;
    private float rotationY = 0f;
    
    private void Start()
    {
        // Lock cursor to center of screen for better mouse control
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Initialize rotation based on current camera rotation
        Vector3 currentRotation = transform.eulerAngles;
        rotationY = currentRotation.y;
        rotationX = currentRotation.x;
        
        // Normalize rotationX to -180 to 180 range
        if (rotationX > 180f)
            rotationX -= 360f;
    }
    
    private void Update()
    {
        HandleMouseLook();
        HandleMovement();
        
        // Toggle cursor lock with Escape key
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
    
    private void HandleMouseLook()
    {
        // Get mouse input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        
        // Rotate camera horizontally (Y axis) - 360° rotation
        rotationY += mouseX;
        
        // Rotate camera vertically (X axis) with limit
        rotationX -= mouseY;
        rotationX = Mathf.Clamp(rotationX, -verticalRotationLimit, verticalRotationLimit);
        
        // Apply rotation to camera
        transform.rotation = Quaternion.Euler(rotationX, rotationY, 0f);
    }
    
    private void HandleMovement()
    {
        // Get input from ZQSD keys (French keyboard layout)
        // Z = Forward (like W), Q = Left (like A), S = Backward, D = Right
        Vector3 moveDirection = Vector3.zero;
        
        if (Input.GetKey(KeyCode.Z)) // Forward
        {
            moveDirection += transform.forward;
        }
        if (Input.GetKey(KeyCode.S)) // Backward
        {
            moveDirection -= transform.forward;
        }
        if (Input.GetKey(KeyCode.Q)) // Left
        {
            moveDirection -= transform.right;
        }
        if (Input.GetKey(KeyCode.D)) // Right
        {
            moveDirection += transform.right;
        }
        if (Input.GetKey(KeyCode.Space)) // Up
        {
            moveDirection += Vector3.up;
        }
        if (Input.GetKey(KeyCode.C)) // Down
        {
            moveDirection += Vector3.down;
        }
        
        // Normalize movement direction to prevent faster diagonal movement
        moveDirection.Normalize();
        
        // Apply sprint multiplier if Shift is held
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed *= sprintMultiplier;
        }
        
        // Move camera
        transform.position += moveDirection * currentSpeed * Time.deltaTime;
    }
}
