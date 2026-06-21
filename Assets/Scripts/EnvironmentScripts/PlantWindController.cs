using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla las animaciones de una o varias plantas.
/// Usa CrossFade directo (igual que WindStateManager) para forzar el estado
/// independientemente de desde dónde venga el Animator — esto evita el problema
/// de "solo funciona la primera vez" que ocurre con SetBool.
///
/// Animator setup recomendado:
///   [WindyEntry] ---(HasExitTime)---> [WindyLoop]   (ambos en capa 0)
///   [WindyOut]   ---(HasExitTime)---> [IdleSmooth]  (opcional, si quieres el flow automático)
///   [IdleSmooth] loops
///
/// Los nombres de estado son configurables desde el Inspector.
/// Ya NO se necesitan los bools NotWindy / IsIdleSmooth en el Animator.
///
/// API pública (llamada por WindStateManager o desde código):
///   PlayWindy()      → fuerza WindyEntry → loop
///   PlayWindyExit()  → espera loopDurationBeforeOut segundos → fuerza WindyOut
///   PlayIdleSmooth() → fuerza IdleSmooth
/// </summary>
public class PlantWindController : MonoBehaviour
{
    [System.Serializable]
    public struct PlantEntry
    {
        [Tooltip("Animator del GameObject de la planta.")]
        public Animator animator;
    }

    [Header("Plantas")]
    [SerializeField] private PlantEntry[] plants;

    [Header("Nombres de estado en el Animator")]
    [SerializeField] private string windyEntryState = "WindyEntry";
    [SerializeField] private string windyLoopState  = "WindyLoop";
    [SerializeField] private string windyOutState   = "WindyOut";
    [SerializeField] private string idleSmoothState = "IdleSmoth";

    [Header("Config")]
    [Range(0f, 1f)]
    [Tooltip("Tiempo de CrossFade normalizado (0 = instantáneo).")]
    [SerializeField] private float crossFadeTime = 0f;
    [Tooltip("Segundos que el loop ventoso corre antes de reproducir la animación 'out'.")]
    [SerializeField] private float loopDurationBeforeOut = 2f;

    private Coroutine _exitCoroutine;

    private void Awake()
    {
        if (!HasMissingPlantReferences())
            return;

        var matches = new List<PlantEntry>();
        foreach (var animator in Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (HasAllPlantStates(animator))
                matches.Add(new PlantEntry { animator = animator });
        }

        plants = matches.ToArray();

        if (plants.Length > 0)
            Debug.LogWarning($"[{nameof(PlantWindController)}] Se repararon referencias de plantas durante esta ejecución. Revisa el Inspector para corregirlas de forma permanente.", this);
    }

    // ── API pública ──────────────────────────────────────────────────────────

    /// <summary>entry → loop. Fuerza WindyEntry independientemente del estado actual.</summary>
    public void PlayWindy()
    {
        StopExitCoroutine();
        CrossFadeAll(windyEntryState);
    }

    /// <summary>Mantiene el loop durante loopDurationBeforeOut segundos y luego fuerza WindyOut.</summary>
    public void PlayWindyExit()
    {
        StopExitCoroutine();
        _exitCoroutine = StartCoroutine(WindyExitRoutine());
    }

    /// <summary>Fuerza IdleSmooth directamente.</summary>
    public void PlayIdleSmooth()
    {
        StopExitCoroutine();
        CrossFadeAll(idleSmoothState);
    }

    // ── Privado ──────────────────────────────────────────────────────────────

    private bool HasMissingPlantReferences()
    {
        if (plants == null || plants.Length == 0)
            return true;

        foreach (var plant in plants)
        {
            if (plant.animator == null)
                return true;
        }

        return false;
    }

    private bool HasAllPlantStates(Animator animator)
    {
        return animator != null
            && animator.runtimeAnimatorController != null
            && AnimatorContainsState(animator, windyEntryState)
            && AnimatorContainsState(animator, windyLoopState)
            && AnimatorContainsState(animator, windyOutState)
            && AnimatorContainsState(animator, idleSmoothState);
    }

    private static bool AnimatorContainsState(Animator animator, string stateName)
    {
        int shortHash = Animator.StringToHash(stateName);
        int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");
        return animator.HasState(0, shortHash) || animator.HasState(0, fullPathHash);
    }

    private IEnumerator WindyExitRoutine()
    {
        CrossFadeAll(windyLoopState);
        yield return new WaitForSeconds(loopDurationBeforeOut);
        CrossFadeAll(windyOutState);
        _exitCoroutine = null;
    }

    private void CrossFadeAll(string stateName)
    {
        if (plants == null) return;

        foreach (var p in plants)
        {
            if (p.animator != null)
                p.animator.CrossFade(stateName, crossFadeTime, 0);
        }
    }

    private void StopExitCoroutine()
    {
        if (_exitCoroutine == null) return;
        StopCoroutine(_exitCoroutine);
        _exitCoroutine = null;
    }
}
