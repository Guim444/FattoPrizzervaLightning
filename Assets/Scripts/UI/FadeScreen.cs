using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeScreen : MonoBehaviour
{
    [SerializeField] private Image _fadeImage;
    [SerializeField] private bool _fadeInOnStart;
    [SerializeField, Min(0f)] private float _fadeInStartDuration = 1f;

    private Coroutine _fadeCoroutine;
    private Action _fadeCompletion;

    private void Start()
    {
        if (_fadeInOnStart)
            StartFadeIn(_fadeInStartDuration);
    }

    public void StartFadeIn(float duration)
    {
        StartFadeIn(duration, null);
    }

    public void StartFadeIn(float duration, Action onComplete)
    {
        StartFade(1f, 0f, duration, onComplete);
    }

    public void SetAlphaInstantly(float alpha)
    {
        if (_fadeImage == null)
        {
            Debug.LogError("FadeScreen: falta asignar Fade Image.", this);
            return;
        }

        StopFading();
        SetAlpha(Mathf.Clamp01(alpha));
    }

    public void StartFadeOut(float duration)
    {
        StartFade(0f, 1f, duration, null);
    }

    private void StartFade(float initialAlpha, float targetAlpha, float duration, Action onComplete)
    {
        if (_fadeImage == null)
        {
            Debug.LogError("FadeScreen: falta asignar Fade Image.", this);
            return;
        }

        StopFading();
        _fadeCompletion = onComplete;
        _fadeCoroutine = StartCoroutine(Fade(initialAlpha, targetAlpha, duration));
    }

    public void StopFading()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        _fadeCompletion = null;
    }

    private IEnumerator Fade(float initialAlpha, float targetAlpha, float duration)
    {
        duration = Mathf.Max(0f, duration);

        if (duration == 0f)
        {
            SetAlpha(targetAlpha);
            CompleteFade();
            yield break;
        }

        SetAlpha(initialAlpha);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);

            SetAlpha(Mathf.Lerp(initialAlpha, targetAlpha, progress));
            yield return null;
        }

        SetAlpha(targetAlpha);
        CompleteFade();
    }

    private void CompleteFade()
    {
        _fadeCoroutine = null;

        Action completion = _fadeCompletion;
        _fadeCompletion = null;
        completion?.Invoke();
    }

    private void SetAlpha(float alpha)
    {
        Color color = _fadeImage.color;
        color.a = alpha;
        _fadeImage.color = color;
    }
}
