using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class FireVisualScript : MonoBehaviour
{
    private const float MinimumFluctuationValue = 0.01f;

    [Header("Fire Fluctuation")]
    [SerializeField, Range(MinimumFluctuationValue, 2f)]
    private float fluctuationTime = 1f;

    [SerializeField, Range(MinimumFluctuationValue, 100f)]
    private float fluctuationRate = 1f;

    private Light _fireLight;
    private Coroutine _fluctuationRoutine;
    private float _baseIntensity;
    private float _currentOffset;
    private bool _baseIntensityInitialized;
    private bool _isIncreasing = true;
    private bool _animationPlaybackEnabled = true;

    private void Awake()
    {
        EnsureLightReference();

        if (!_baseIntensityInitialized)
        {
            _baseIntensity = _fireLight.intensity;
            _baseIntensityInitialized = true;
        }
    }

    private void OnEnable()
    {
        StartFluctuation();
    }

    private void OnDisable()
    {
        StopFluctuation();
    }

    private void OnValidate()
    {
        fluctuationTime = Mathf.Max(MinimumFluctuationValue, fluctuationTime);
        fluctuationRate = Mathf.Max(MinimumFluctuationValue, fluctuationRate);
    }

    public void SetAnimationPlaybackEnabled(bool shouldRun)
    {
        _animationPlaybackEnabled = shouldRun;

        if (shouldRun)
            StartFluctuation();
        else
            StopFluctuation();
    }

    /// <summary>
    /// Actualiza la intensidad base calculada por el gestor de iluminación.
    /// El parpadeo conserva su desplazamiento actual sobre esta nueva base.
    /// </summary>
    public void SetBaseIntensity(float intensity)
    {
        EnsureLightReference();
        _baseIntensity = intensity;
        _baseIntensityInitialized = true;
        ApplyCurrentIntensity();
    }

    private IEnumerator FireFluctuation()
    {
        while (true)
        {
            float targetOffset = _isIncreasing ? fluctuationRate : 0f;
            float speed = fluctuationRate / fluctuationTime;

            _currentOffset = Mathf.MoveTowards(
                _currentOffset,
                targetOffset,
                speed * Time.deltaTime);

            ApplyCurrentIntensity();

            if (Mathf.Approximately(_currentOffset, targetOffset))
                _isIncreasing = !_isIncreasing;

            yield return null;
        }
    }

    private void StartFluctuation()
    {
        if (!_animationPlaybackEnabled
            || !isActiveAndEnabled
            || _fireLight == null
            || _fluctuationRoutine != null)
        {
            return;
        }

        if (fluctuationTime <= 0f || fluctuationRate <= 0f)
        {
            _currentOffset = 0f;
            ApplyCurrentIntensity();
            Debug.LogWarning(
                $"[{nameof(FireVisualScript)}] {name} necesita valores mayores que cero "
                + $"para {nameof(fluctuationTime)} y {nameof(fluctuationRate)}.",
                this);
            return;
        }

        _fluctuationRoutine = StartCoroutine(FireFluctuation());
    }

    private void StopFluctuation()
    {
        if (_fluctuationRoutine == null)
            return;

        StopCoroutine(_fluctuationRoutine);
        _fluctuationRoutine = null;
    }

    private void ApplyCurrentIntensity()
    {
        if (_fireLight != null)
            _fireLight.intensity = _baseIntensity + _currentOffset;
    }

    private void EnsureLightReference()
    {
        if (_fireLight == null)
            _fireLight = GetComponent<Light>();
    }
}
