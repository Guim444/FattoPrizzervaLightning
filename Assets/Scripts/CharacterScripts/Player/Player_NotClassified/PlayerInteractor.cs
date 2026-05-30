using UnityEngine;

/// <summary>
/// Single responsibility: manage detection and initiation of environment interactions.
/// Casts a raycast from the player's position and delegates to the IInteractable interface.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    // ==================================================
    // INTERACTION CONFIG
    // ==================================================
    [Header("Interaction")]
    [Tooltip("Layers the interaction raycast can collide with.")]
    public LayerMask interactMask;

    [Tooltip("Maximum interaction distance in world units.")]
    public float interactRange = 3f;

    [Tooltip("Height from which the raycast is cast (relative to player pivot).")]
    public float raycastHeight = 1.5f;

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Tries to interact with the object in front of the player.
    /// Called from PlayerInputHandler when the interaction action is detected.
    /// </summary>
    public void TryInteract()
    {
        Vector3 origin = transform.position + Vector3.up * raycastHeight;

        if (Physics.Raycast(origin, transform.forward, out var hit, interactRange, interactMask))
        {
            // TODO: implement IInteractable and call hit.collider.GetComponent<IInteractable>()?.Interact()
        }
    }
}
