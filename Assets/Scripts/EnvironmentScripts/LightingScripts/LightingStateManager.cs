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
    public enum LightingState { Tapat = 0, Radiografia = 1, Lluna = 2 }

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
        public GameObject sky;
        public LightEntry[] entries;
    }

    [Header("Lighting States")]
    [SerializeField] private LightingStateData[] states = new LightingStateData[3];

    [Header("Transition")]
    [SerializeField] private float transitionDuration = 3f;

    [Header("Play Mode Test")]
    [SerializeField] private LightingState _previewState;
    [SerializeField] private bool enableKeyboardShortcuts = true;

    private int _currentStateIndex = -1;
    private Coroutine _activeTransition;
    private readonly List<GameObject> _activatedByManager = new List<GameObject>();

    private void Update()
    {
        if (!enableKeyboardShortcuts) return;

        if      (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) TransitionTo(LightingState.Tapat);
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) TransitionTo(LightingState.Radiografia);
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) TransitionTo(LightingState.Lluna);
    }

    /// <summary>Transitions to the given state, interpolating from startIntensity to targetIntensity. Does nothing if already in that state.</summary>
    public void TransitionTo(LightingState state, float duration = -1f)
    {
        int idx = (int)state;
        if (idx == _currentStateIndex) return;
        if (idx < 0 || idx >= states.Length) return;

        if (_activeTransition != null)
            StopCoroutine(_activeTransition);

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

        if (_activeTransition != null)
            StopCoroutine(_activeTransition);

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

    private void SwitchSky(int stateIndex)
    {
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].sky != null)
                states[i].sky.SetActive(i == stateIndex);
        }
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

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            foreach (var entry in target.entries)
            {
                if (entry.light != null)
                    entry.light.intensity = Mathf.Lerp(entry.startIntensity, entry.targetIntensity, t);
            }

            yield return null;
        }

        foreach (var entry in target.entries)
        {
            if (entry.light != null)
                entry.light.intensity = entry.targetIntensity;
        }

        _activeTransition = null;
    }
}