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
        [Tooltip("Descriptive name of the milestone (Inspector only).")]
        public string label = "Milestone";

        [Tooltip("Endurance value the player reaches when this milestone is hit.")]
        public int newEndurance = 1;

        [Tooltip("Seconds to wait before applying the change " +
                 "(time for prior visual/SFX feedback).")]
        public float delaySeconds = 0.5f;

        [Tooltip("Optional event on activation (show UI, SFX, particle effect, etc.).")]
        public UnityEvent onTriggered;
    }

    // ==================================================
    // INSPECTOR FIELDS
    // ==================================================

    [Header("References")]
    public PlayerController player;

    [Header("Endurance Milestones (chronological order)")]
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
            Debug.Log("[TutorialManager] All milestones completed.");
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
            Debug.LogWarning($"[TutorialManager] Index {index} out of range.");
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
        Debug.Log("[TutorialManager] Milestones reset.");
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