using System.Collections;
using UnityEngine;

/// <summary>
/// Añade el comportamiento de ventisca sobre LightingStateManager.
/// LightingStateManager gestiona el input de teclado (1-4) y el cielo/luces.
/// Este script escucha las mismas teclas SOLO para coordinar el viento.
///
/// IMPORTANTE: enableKeyboardShortcuts en LightingStateManager debe estar activo.
///             enableKeyboardShortcuts aquí controla SOLO el viento.
/// </summary>
public class EnvironmentStateManager : MonoBehaviour
{
    [Header("Sub-managers")]
    [SerializeField] private LightingStateManager lightingManager;
    [SerializeField] private WindStateManager windManager;

    [Header("Duraciones de transición de viento")]
    [SerializeField] private float windTransitionDuration = 2f;

    [Header("Test")]
    [SerializeField] private bool enableKeyboardShortcuts = true;

    private void Update()
    {
        if (!enableKeyboardShortcuts) return;

        if      (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) TriggerWindState(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) TriggerWindState(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) TriggerWindState(3);
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) TriggerWindState(4);
    }

    /// <summary>Aplica el estado de viento correspondiente al número de estado (1-4).</summary>
    public void TriggerWindState(int stateNumber)
    {
        if (windManager == null) return;

        switch (stateNumber)
        {
            case 1: windManager.SnapToState(WindStateManager.WindIntensity.Max);                     break;
            case 2: windManager.TransitionTo(WindStateManager.WindIntensity.Medium, windTransitionDuration); break;
            case 3: windManager.TransitionTo(WindStateManager.WindIntensity.Min,    windTransitionDuration); break;
            case 4: windManager.TransitionTo(WindStateManager.WindIntensity.Medium, windTransitionDuration); break;
        }
    }
}