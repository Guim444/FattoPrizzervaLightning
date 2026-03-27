using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Gestiona los hitos scripted del tutorial.
/// Cada milestone sube el Endurance del player en el momento que el diseñador decida.
///
/// USO:
///   – Llamar TriggerNextMilestone() desde un botón de UI, una zona trigger,
///     un evento de animación, o directamente desde otro script.
///   – Los milestones se ejecutan en orden. Cada uno puede tener un delay
///     y disparar un UnityEvent (para reproducir SFX, mostrar texto, etc.).
///
/// SETUP:
///   Por defecto la lista tiene End.1 y End.2 listos.
///   Ajustar 'newEndurance' y 'delaySeconds' según el ritmo del tutorial.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    // ==================================================
    // MILESTONE DEFINITION
    // ==================================================

    [System.Serializable]
    public class EnduranceMilestone
    {
        [Tooltip("Nombre descriptivo del hito (solo para el Inspector).")]
        public string label = "Hito";

        [Tooltip("Valor al que sube el Endurance del player al alcanzar este hito.")]
        public int newEndurance = 1;

        [Tooltip("Segundos de espera antes de aplicar el cambio " +
                 "(tiempo para feedback visual/SFX previo).")]
        public float delaySeconds = 0.5f;

        [Tooltip("Evento opcional al activar (mostrar UI, SFX, efecto de partículas, etc.).")]
        public UnityEvent onTriggered;
    }

    // ==================================================
    // INSPECTOR FIELDS
    // ==================================================

    [Header("References")]
    public PlayerController player;

    [Header("Endurance Milestones (orden cronológico)")]
    public List<EnduranceMilestone> milestones = new List<EnduranceMilestone>
    {
        new EnduranceMilestone { label = "Endurance 0 → 1", newEndurance = 1, delaySeconds = 0.5f },
        new EnduranceMilestone { label = "Endurance 1 → 2", newEndurance = 2, delaySeconds = 0.5f },
    };

    // ==================================================
    // PRIVATE STATE
    // ==================================================

    private int _nextMilestoneIndex = 0;

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Activa el siguiente milestone de Endurance en la secuencia.
    /// Llamar desde: botón UI, trigger de zona, evento de animación, etc.
    /// </summary>
    public void TriggerNextMilestone()
    {
        if (_nextMilestoneIndex >= milestones.Count)
        {
            Debug.Log("[TutorialManager] Todos los milestones completados.");
            return;
        }

        StartCoroutine(ApplyMilestone(milestones[_nextMilestoneIndex]));
        _nextMilestoneIndex++;
    }

    /// <summary>
    /// Activa un milestone específico por índice (0-based), ignorando el orden.
    /// Útil para acceso directo desde otros scripts o para testing.
    /// </summary>
    public void TriggerMilestone(int index)
    {
        if (index < 0 || index >= milestones.Count)
        {
            Debug.LogWarning($"[TutorialManager] Índice {index} fuera de rango.");
            return;
        }

        StartCoroutine(ApplyMilestone(milestones[index]));
    }

    /// <summary>
    /// Reinicia el puntero de milestones (para rejugar el tutorial).
    /// </summary>
    public void ResetMilestones()
    {
        _nextMilestoneIndex = 0;
        Debug.Log("[TutorialManager] Milestones reiniciados.");
    }

    // ==================================================
    // INTERNAL
    // ==================================================

    IEnumerator ApplyMilestone(EnduranceMilestone milestone)
    {
        if (milestone.delaySeconds > 0f)
            yield return new WaitForSeconds(milestone.delaySeconds);

        player.combat.endurance = milestone.newEndurance;
        milestone.onTriggered?.Invoke();

        Debug.Log($"[TutorialManager] '{milestone.label}' → Player Endurance = {milestone.newEndurance}");
    }
}