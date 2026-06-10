using UnityEngine;

/// <summary>
/// Coordina viento y lighting al pulsar teclas 1-4.
/// LightingStateManager gestiona su propio input para cielo y luces.
/// Este script escucha las mismas teclas SOLO para el viento.
/// </summary>
public class EnvironmentStateManager : MonoBehaviour
{
    [Header("Sub-managers")]
    [SerializeField] private WindStateManager windManager;

    [Header("Test")]
    [SerializeField] private bool enableKeyboardShortcuts = true;

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
            case 1: windManager.SnapToState(WindStateManager.WindPreset.W1_MaxIdle);      break;
            // Tecla 2: transición de máxima a media, luego idle media
            case 2: windManager.TransitionTo(WindStateManager.WindPreset.W2_MaxToMedium); break;
            // Tecla 3: transición de media a mínima, luego idle mínima
            case 3: windManager.TransitionTo(WindStateManager.WindPreset.W3_MediumToMin); break;
            // Tecla 4: transición de mínima a media (cielo azul), luego idle media
            case 4: windManager.TransitionTo(WindStateManager.WindPreset.W4_MinToMedium); break;
        }
    }
}