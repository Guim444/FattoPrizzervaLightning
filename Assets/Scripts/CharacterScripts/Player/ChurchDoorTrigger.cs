using UnityEngine;

public class ChurchDoorTrigger : MonoBehaviour
{
    [SerializeField] private HUDManager hudManager;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Camera playerCamera;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if ((playerLayer.value & (1 << other.gameObject.layer)) == 0) return;
        triggered = true;
        if (playerCamera != null)
            playerCamera.fieldOfView = 60;
        
        hudManager.OnEnterCombatFromTrigger();
    }
}
