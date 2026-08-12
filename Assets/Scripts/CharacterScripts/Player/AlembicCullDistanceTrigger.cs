using UnityEngine;

/// <summary>
/// Amplía el rango de reproducción de los Alembic al entrar el jugador en el trigger.
/// Este componente es independiente de ChurchDoorTrigger y no inicia la secuencia de la iglesia.
/// </summary>
public class AlembicCullDistanceTrigger : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("Si está desactivado, el trigger ignora la entrada del jugador.")]
    [SerializeField] private bool triggerEnabled = true;

    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (!triggerEnabled || triggered) return;
        if ((playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

        WindStateManager[] managers = Object.FindObjectsByType<WindStateManager>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (WindStateManager manager in managers)
            manager.UseChurchAlembicDistance();

        triggered = managers.Length > 0;
    }
}
