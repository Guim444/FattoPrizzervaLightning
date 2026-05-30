using UnityEngine;

/// <summary>
/// Responsabilidad única: gestionar la detección e inicio de interacciones con el entorno.
/// Lanza un raycast desde la posición del jugador y delega en la interfaz IInteractable.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    // ==================================================
    // INTERACTION CONFIG
    // ==================================================
    [Header("Interaction")]
    [Tooltip("Capas con las que el raycast de interacción puede colisionar.")]
    public LayerMask interactMask;

    [Tooltip("Distancia máxima de interacción en unidades de mundo.")]
    public float interactRange = 3f;

    [Tooltip("Altura desde la que se lanza el raycast (relativa al pivote del jugador).")]
    public float raycastHeight = 1.5f;

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Intenta interactuar con el objeto frente al jugador.
    /// Se llama desde PlayerInputHandler cuando se detecta la acción de interacción.
    /// </summary>
    public void TryInteract()
    {
        Vector3 origin = transform.position + Vector3.up * raycastHeight;

        if (Physics.Raycast(origin, transform.forward, out var hit, interactRange, interactMask))
        {
            // TODO: implementar IInteractable y llamar a hit.collider.GetComponent<IInteractable>()?.Interact()
        }
    }
}
