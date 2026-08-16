using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FadeScreen : MonoBehaviour
{
    [SerializeField] private Image _fadeImage;
    [SerializeField] private bool _fadeInOnStart;
    [SerializeField, Min(0f)] private float _fadeInStartDuration = 1f;

    private Coroutine _fadeCoroutine;

    private void Start()
    {
        if (_fadeInOnStart)
            StartFadeIn(_fadeInStartDuration);
    }

    public void StartFadeIn(float duration)
    {
        StartFade(1f, 0f, duration);
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
        StartFade(0f, 1f, duration);
    }

    private void StartFade(float initialAlpha, float targetAlpha, float duration)
    {
        if (_fadeImage == null)
        {
            Debug.LogError("FadeScreen: falta asignar Fade Image.", this);
            return;
        }

        StopFading();
        _fadeCoroutine = StartCoroutine(Fade(initialAlpha, targetAlpha, duration));
    }

    public void StopFading()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
    }

    private IEnumerator Fade(float initialAlpha, float targetAlpha, float duration)
    {
        duration = Mathf.Max(0f, duration);

        if (duration == 0f)
        {
            SetAlpha(targetAlpha);
            _fadeCoroutine = null;
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
        _fadeCoroutine = null;
    }

    private void SetAlpha(float alpha)
    {
        Color color = _fadeImage.color;
        color.a = alpha;
        _fadeImage.color = color;
    }
}
