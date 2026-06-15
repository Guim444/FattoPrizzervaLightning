using UnityEngine;

/// <summary>
/// Coordina las animaciones ambientales al pulsar teclas 1-4.
/// LightingStateManager gestiona su propio input para cielo y luces.
/// Este script escucha las mismas teclas para ventisca, árboles y plantas.
/// </summary>
public class EnvironmentStateManager : MonoBehaviour
{
    [Header("Sub-managers")]
    [SerializeField] private WindStateManager windManager;
    [SerializeField] private PlantWindController plantManager;

    [Header("Test")]
    [SerializeField] private bool enableKeyboardShortcuts = true;

    private void Awake()
    {
        bool repaired = false;

        if (windManager == null)
        {
            windManager = GetComponent<WindStateManager>();
            if (windManager == null)
                windManager = FindUniqueManager<WindStateManager>();

            repaired |= windManager != null;
        }

        if (plantManager == null)
        {
            plantManager = GetComponent<PlantWindController>();
            if (plantManager == null)
                plantManager = FindUniqueManager<PlantWindController>();

            repaired |= plantManager != null;
        }

        if (repaired)
            Debug.LogWarning($"[{nameof(EnvironmentStateManager)}] Se repararon referencias perdidas durante esta ejecución. Revisa el Inspector para corregirlas de forma permanente.", this);
    }

    private void Update()
    {
        if (!enableKeyboardShortcuts) return;

        if      (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ApplyWindState(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ApplyWindState(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) ApplyWindState(3);
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) ApplyWindState(4);
    }

    private void ApplyWindState(int key)
    {
        if (windManager == null) { Debug.LogWarning("[EnvironmentStateManager] windManager no asignado en el Inspector."); return; }

        switch (key)
        {
            // Tecla 1: ventisca máxima, snap inmediato (estado de inicio / reset)
            case 1:
                windManager.SnapToState(WindStateManager.WindPreset.W1_MaxIdle);
                plantManager?.PlayWindy();
                break;
            // Tecla 2: transición de máxima a media, luego idle media
            case 2:
                windManager.TransitionTo(WindStateManager.WindPreset.W2_MaxToMedium);
                plantManager?.PlayWindyExit();
                break;
            // Tecla 3: transición de media a mínima, luego idle mínima
            case 3:
                windManager.TransitionTo(WindStateManager.WindPreset.W3_MediumToMin);
                plantManager?.PlayIdleSmooth();
                break;
            // Tecla 4: transición de mínima a media (cielo azul), luego idle media
            case 4:
                windManager.TransitionTo(WindStateManager.WindPreset.W4_MinToMedium);
                plantManager?.PlayWindy();
                break;
        }
    }

    private T FindUniqueManager<T>() where T : Component
    {
        var matches = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (matches.Length == 1)
            return matches[0];

        if (matches.Length > 1)
            Debug.LogWarning($"[{nameof(EnvironmentStateManager)}] Hay varios {typeof(T).Name}. Asigna la referencia manualmente.", this);

        return null;
    }
}
