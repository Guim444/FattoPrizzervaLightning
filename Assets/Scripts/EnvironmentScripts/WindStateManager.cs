using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la intensidad de la ventisca en 3 niveles: Max, Medium, Min.
/// Completamente serializable — asigna los Animators y los valores desde el Inspector
/// cuando los assets estén listos.
///
/// El float "WindIntensity" en cada Animator se interpola suavemente entre estados.
/// </summary>
public class WindStateManager : MonoBehaviour
{
    public enum WindIntensity { Max = 0, Medium = 1, Min = 2 }

    [System.Serializable]
    public struct WindStateData
    {
        public string stateName;
        [Range(0f, 1f)]
        [Tooltip("Valor del parámetro WindIntensity en el Animator (0=calma, 1=máxima ventisca).")]
        public float intensityValue;
    }

    [Header("Estados de Viento (0=Max, 1=Medium, 2=Min)")]
    [SerializeField] private WindStateData[] windStates = new WindStateData[]
    {
        new WindStateData { stateName = "Max",    intensityValue = 1.0f },
        new WindStateData { stateName = "Medium", intensityValue = 0.5f },
        new WindStateData { stateName = "Min",    intensityValue = 0.1f },
    };

    [Header("Animators de ventisca")]
    [SerializeField] private Animator[] windAnimators;
    [SerializeField] private string intensityParam = "WindIntensity";

    [Header("Transición")]
    [SerializeField] private float defaultTransitionDuration = 2f;

    private float _currentIntensity = 1f;
    private int _currentStateIndex = -1;
    private Coroutine _activeTransition;

    /// <summary>Transiciona suavemente a la intensidad de viento dada.</summary>
    public void TransitionTo(WindIntensity intensity, float duration = -1f)
    {
        int idx = (int)intensity;
        if (idx == _currentStateIndex) return;

        if (_activeTransition != null) StopCoroutine(_activeTransition);
        _currentStateIndex = idx;

        float dur = duration < 0f ? defaultTransitionDuration : duration;
        _activeTransition = StartCoroutine(TransitionCoroutine(windStates[idx].intensityValue, dur));
    }

    /// <summary>Salta instantáneamente a la intensidad dada sin interpolación.</summary>
    public void SnapToState(WindIntensity intensity)
    {
        if (_activeTransition != null)
        {
            StopCoroutine(_activeTransition);
            _activeTransition = null;
        }
        int idx = (int)intensity;
        _currentStateIndex = idx;
        SetIntensity(windStates[idx].intensityValue);
    }

    private IEnumerator TransitionCoroutine(float targetIntensity, float duration)
    {
        float startIntensity = _currentIntensity;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetIntensity(Mathf.Lerp(startIntensity, targetIntensity, t));
            yield return null;
        }

        SetIntensity(targetIntensity);
        _activeTransition = null;
    }

    private void SetIntensity(float intensity)
    {
        _currentIntensity = intensity;
        foreach (var anim in windAnimators)
        {
            if (anim != null)
                anim.SetFloat(intensityParam, intensity);
        }
    }
}