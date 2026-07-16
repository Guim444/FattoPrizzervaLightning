using System.Collections;
using UnityEngine;

public class FireVisualScript : MonoBehaviour
{
    [Range(0, 2)] public float fluctuationTime;
    [Range(0, 5)] public float fluctuationRate;

    public Light fireLight;
    public float baseIntensity;

    private Coroutine _fluctuationRoutine;
    private bool _animationPlaybackEnabled = true;

    public void Awake()
    {
        fireLight = GetComponent<Light>();
        if (fireLight == null)
        {
            Debug.LogWarning($"[{nameof(FireVisualScript)}] {name} no tiene Light asociado.", this);
            enabled = false;
            return;
        }

        baseIntensity = fireLight.intensity;
        StartFluctuation();
    }

    private void OnEnable()
    {
        if (_animationPlaybackEnabled && _fluctuationRoutine == null && fireLight != null)
            StartFluctuation();
    }

    private void OnDisable()
    {
        StopFluctuation();
    }

    public void SetAnimationPlaybackEnabled(bool shouldRun)
    {
        _animationPlaybackEnabled = shouldRun;

        if (shouldRun)
            StartFluctuation();
        else
            StopFluctuation();
    }

    public IEnumerator FireFluctuation()
    {
        if (fireLight == null || fluctuationTime <= 0f)
            yield break;

        float targetUp = baseIntensity + fluctuationRate;
        float targetDown = baseIntensity;

        while (true)
        {
            while (fireLight.intensity < targetUp)
            {
                fireLight.intensity += (fluctuationRate / fluctuationTime) * Time.deltaTime;
                yield return null;
            }

            while (fireLight.intensity > targetDown)
            {
                fireLight.intensity -= (fluctuationRate / fluctuationTime) * Time.deltaTime;
                yield return null;
            }
        }
    }

    private void StartFluctuation()
    {
        if (!_animationPlaybackEnabled || !isActiveAndEnabled || fireLight == null || _fluctuationRoutine != null)
            return;

        _fluctuationRoutine = StartCoroutine(FireFluctuation());
    }

    private void StopFluctuation()
    {
        if (_fluctuationRoutine == null)
            return;

        StopCoroutine(_fluctuationRoutine);
        _fluctuationRoutine = null;
    }
}
