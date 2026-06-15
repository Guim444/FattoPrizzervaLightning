using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages intensity transitions between the 3 lighting states:
/// Tapat (base) → Radiografia (transition) → Lluna (full moon).
///
/// Inspector setup:
/// - Each state defines a Sky to activate and which lights change with their range (from → to).
/// - In Tapat, startIntensity = targetIntensity (fixed value).
/// - In Radiografia and Lluna, startIntensity and targetIntensity define the lerp range.
/// - transitionDuration controls the default duration of each transition.
/// </summary>
public class LightingStateManager : MonoBehaviour
{
    public enum LightingState { Tapat = 0, Radiografia = 1, Lluna = 2, Blue = 3 }

    [System.Serializable]
    public struct LightEntry
    {
        public Light light;
        public float startIntensity;
        public float targetIntensity;
    }

    [System.Serializable]
    public struct LightingStateData
    {
        public string stateName;
        [Tooltip("Trigger del Animator del cielo. Vacío = no dispara nada (ej: estado 1, cielo en reposo).")]
        public string skyAnimatorTrigger;
        public LightEntry[] entries;
    }

    [Header("Sky")]
    [Tooltip("El único Animator del cielo. Comparte el mismo objeto para todos los estados.")]
    [SerializeField] private Animator skyAnimator;

    [Header("Lights Objects")]
    [Tooltip("Activo en estados 1-3 (Tapat, Radiografia, Lluna).")]
    [SerializeField] private GameObject redLightsObject;
    [Tooltip("Activo solo en estado 4 (Azul).")]
    [SerializeField] private GameObject blueLightsObject;
    [SerializeField] private GameObject redSource;
    [SerializeField] private GameObject blueSource;

    [Header("Baked Lighting")]
    [SerializeField] private LightmapStateManager lightmapStateManager;

    [Header("Lighting States")]
    [SerializeField] private LightingStateData[] states = new LightingStateData[4];

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 3f;

    [Header("Blue State — Delay")]
    [Tooltip("Segundos entre pulsar tecla 4 y el snap a luces azules. El cielo y los objetos cambian al instante.")]
    [SerializeField] private float blueLightsDelay = 2f;

    [Header("Play Mode Test")]
    [SerializeField] private LightingState _previewState;
    [SerializeField] private bool enableKeyboardShortcuts = true;

    private int _currentStateIndex = -1;
    private int _currentSkyIndex = -1;
    private Coroutine _activeTransition;
    private Coroutine _blueTransitionCoroutine;
    private readonly List<GameObject> _activatedByManager = new List<GameObject>();

    private void Awake()
    {
        if (lightmapStateManager == null)
            lightmapStateManager = GetComponent<LightmapStateManager>();
    }

    private void Update()
    {
        if (!enableKeyboardShortcuts) return;

        if      (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) ApplyPhase(1);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) ApplyPhase(2);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) ApplyPhase(3);
        else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) ApplyPhase(4);
    }

    public void ApplyPhase(int phase)
    {
        switch (phase)
        {
            case 1:
                TransitionTo(LightingState.Tapat);
                break;
            case 2:
                TransitionTo(LightingState.Radiografia);
                break;
            case 3:
                TransitionTo(LightingState.Lluna);
                break;
            case 4:
                StartBlueTransition();
                break;
            default:
                Debug.LogWarning($"[{nameof(LightingStateManager)}] La fase {phase} no existe.", this);
                break;
        }
    }

    /// <summary>Dispara el cielo azul al instante y aplica el snap de luces tras blueLightsDelay segundos.</summary>
    public void StartBlueTransition()
    {
        if (_currentStateIndex == (int)LightingState.Blue && _blueTransitionCoroutine == null)
            return;

        CancelBlueTransition();
        StopActiveTransition();

        _blueTransitionCoroutine = StartCoroutine(BlueTransitionCoroutine());
    }

    private IEnumerator BlueTransitionCoroutine()
    {
        TriggerSkyOnly(LightingState.Blue);
        yield return new WaitForSeconds(blueLightsDelay);

        _blueTransitionCoroutine = null;
        SnapToState(LightingState.Blue);
    }

    /// <summary>Transitions to the given state, interpolating from startIntensity to targetIntensity. Does nothing if already in that state.</summary>
    public void TransitionTo(LightingState state, float duration = -1f)
    {
        int idx = (int)state;
        if (idx < 0 || idx >= states.Length) return;

        CancelBlueTransition();

        if (idx == _currentStateIndex && _activeTransition == null) return;

        StopActiveTransition();

        _currentStateIndex = idx;
        SwitchSky(idx);

        float dur = duration < 0f ? transitionDuration : duration;
        _activeTransition = StartCoroutine(TransitionCoroutine(states[idx], dur));
    }

    /// <summary>Advances to the next state (Tapat → Radiografia → Lluna).</summary>
    public void AdvanceState()
    {
        if (_currentStateIndex < states.Length - 1)
            TransitionTo((LightingState)(_currentStateIndex + 1));
    }

    /// <summary>Forces the given state instantly (no interpolation). Useful for initializing the scene at startup.</summary>
    public void SnapToState(LightingState state)
    {
        int idx = (int)state;
        if (idx < 0 || idx >= states.Length) return;

        CancelBlueTransition();
        StopActiveTransition();

        SwitchSky(idx);
        DeactivatePreviousActivations();
        ActivateStateObjects(states[idx]);

        foreach (var entry in states[idx].entries)
        {
            if (entry.light != null)
                entry.light.intensity = entry.targetIntensity;
        }
        _currentStateIndex = idx;
    }

    /// <summary>Dispara únicamente el trigger del Animator del cielo, sin tocar lightsObject ni intensidades.
    /// Úsalo cuando el trigger debe adelantarse al cambio de luces (ej: estado 4).</summary>
    public void TriggerSkyOnly(LightingState state)
    {
        int idx = (int)state;
        if (idx < 0 || idx >= states.Length) return;
        _currentSkyIndex = idx;
        var s = states[idx];
        if (skyAnimator != null && !string.IsNullOrEmpty(s.skyAnimatorTrigger))
            skyAnimator.SetTrigger(s.skyAnimatorTrigger);
    }

    private void SwitchSky(int stateIndex)
    {
        bool isBlue = stateIndex == (int)LightingState.Blue;

        if (lightmapStateManager != null)
        {
            if (isBlue)
                lightmapStateManager.ApplyBlue();
            else
                lightmapStateManager.ApplyWarm();
        }

        if (redLightsObject  != null) redLightsObject.SetActive(!isBlue);
        if (blueLightsObject != null) blueLightsObject.SetActive(isBlue);
        if (redSource        != null) redSource.SetActive(!isBlue);
        if (blueSource       != null) blueSource.SetActive(isBlue);

        // Si TriggerSkyOnly ya disparó el trigger de este estado, no lo repetimos.
        if (_currentSkyIndex == stateIndex) return;

        _currentSkyIndex = stateIndex;
        var current = states[stateIndex];
        if (skyAnimator != null && !string.IsNullOrEmpty(current.skyAnimatorTrigger))
            skyAnimator.SetTrigger(current.skyAnimatorTrigger);
    }

    [ContextMenu("Apply State")]
    private void ApplyPreviewState() => TransitionTo(_previewState);

    private void DeactivatePreviousActivations()
    {
        foreach (var go in _activatedByManager)
            if (go != null) go.SetActive(false);
        _activatedByManager.Clear();
    }

    private void ActivateStateObjects(LightingStateData target)
    {
        foreach (var entry in target.entries)
        {
            if (entry.light == null) continue;
            var go = entry.light.gameObject;
            if (!go.activeSelf)
            {
                go.SetActive(true);
                _activatedByManager.Add(go);
            }
        }
    }

    private IEnumerator TransitionCoroutine(LightingStateData target, float duration)
    {
        DeactivatePreviousActivations();
        ActivateStateObjects(target);
        ApplyStartIntensities(target);

        if (duration <= 0f)
        {
            ApplyTargetIntensities(target);
            _activeTransition = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < target.entries.Length; i++)
            {
                var entry = target.entries[i];
                if (entry.light != null)
                    entry.light.intensity = Mathf.Lerp(entry.startIntensity, entry.targetIntensity, t);
            }

            yield return null;
        }

        ApplyTargetIntensities(target);
        _activeTransition = null;
    }

    private static void ApplyStartIntensities(LightingStateData target)
    {
        foreach (var entry in target.entries)
        {
            if (entry.light != null)
                entry.light.intensity = entry.startIntensity;
        }
    }

    private static void ApplyTargetIntensities(LightingStateData target)
    {
        foreach (var entry in target.entries)
        {
            if (entry.light != null)
                entry.light.intensity = entry.targetIntensity;
        }
    }

    private void StopActiveTransition()
    {
        if (_activeTransition == null)
            return;

        StopCoroutine(_activeTransition);
        _activeTransition = null;
    }

    private void CancelBlueTransition()
    {
        if (_blueTransitionCoroutine == null)
            return;

        StopCoroutine(_blueTransitionCoroutine);
        _blueTransitionCoroutine = null;
    }
}
