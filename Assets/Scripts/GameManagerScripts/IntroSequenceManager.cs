using System.Collections;
using UnityEngine;

public class IntroSequenceManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject blizzardRoot;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController playerCC;
    [SerializeField] private PlayerBoundaryClamp boundaryClamp;
    [SerializeField] private MonoBehaviour playerInputHandler;
    [SerializeField] private HUDManager hudManager;

    [Header("Fade")]
    [SerializeField] private UnityEngine.UI.Image fadePanel;
    [SerializeField] private float fadeInDuration = 0.3f;

    [Header("Blizzard")]
    [SerializeField] private float _fullBlizzardRate = 2000f;

    [Header("Blink — Sprites")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Sprite humanSprite;
    [SerializeField] private Sprite soulSprite;
    [SerializeField] private Material humanMaterial;

    private Material _originalMaterial;

    [Header("Blink — Posición")]
    [SerializeField] private float blinkStartX   = -100f;
    [SerializeField] private float blinkEndX     = 0f;
    [SerializeField] private float blinkInterval = 0.5f; // duración del ciclo completo

    [Header("Blink — Ratio humano por cuarto (1=siempre humano, 0=siempre alma)")]
    [SerializeField] private float ratioQ1 = 1.00f;
    [SerializeField] private float ratioQ2 = 0.85f;
    [SerializeField] private float ratioQ3 = 0.50f;
    [SerializeField] private float ratioQ4 = 0.15f;

    private ParticleSystem _blizzardPS;

    private void Awake()
    {
        if (blizzardRoot != null)
            _blizzardPS = blizzardRoot.GetComponentInChildren<ParticleSystem>();

        if (playerSpriteRenderer != null)
            _originalMaterial = playerSpriteRenderer.sharedMaterial;
    }

    public void ShowBlackScreen()
    {
        if (fadePanel == null) return;
        fadePanel.gameObject.SetActive(true);
        var c = fadePanel.color;
        c.a = 1f;
        fadePanel.color = c;

        // Prepara la apariencia del jugador bajo el negro — Combat Directly no pasa por aquí
        if (playerAnimator != null) playerAnimator.enabled = false;
        if (playerCC != null) playerCC.enabled = false;
        SetHumanForm();
    }

    public void StartIntro()
    {
        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        // Blizzard a tope ANTES del fade — cuando la pantalla se abra ya está la tormenta
        if (_blizzardPS != null)
        {
            var emission = _blizzardPS.emission;
            emission.rateOverTime = _fullBlizzardRate;
            _blizzardPS.Play();
        }

        if (fadePanel != null)
            yield return StartCoroutine(FadeFromBlack());

        if (playerCC != null) playerCC.enabled = true;
        StartCoroutine(BlinkCoroutine());
    }

    // ── Fade ──────────────────────────────────────────────────────────────

    private IEnumerator FadeFromBlack()
    {
        fadePanel.gameObject.SetActive(true);
        var c = fadePanel.color;
        c.a = 1f;
        fadePanel.color = c;

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(1f, 0f, elapsed / fadeInDuration);
            fadePanel.color = c;
            yield return null;
        }

        c.a = 0f;
        fadePanel.color = c;
        fadePanel.gameObject.SetActive(false);
        
    }

    // ── Blink ─────────────────────────────────────────────────────────────

    private IEnumerator BlinkCoroutine()
    {
        while (true)
        {
            float ratio = GetHumanRatio();

            if (ratio >= 1f)
            {
                SetHumanForm();
                yield return null;
                continue;
            }

            float humanTime = ratio * blinkInterval;
            float soulTime  = (1f - ratio) * blinkInterval;

            SetHumanForm();
            if (humanTime > 0.01f) yield return new WaitForSeconds(humanTime);

            SetSoulForm();
            if (soulTime > 0.01f) yield return new WaitForSeconds(soulTime);
        }
    }

    private void SetHumanForm()
    {
        playerSpriteRenderer.sprite = humanSprite;
        if (humanMaterial != null)
            playerSpriteRenderer.sharedMaterial = humanMaterial;
    }

    private void SetSoulForm()
    {
        playerSpriteRenderer.sprite = soulSprite;
        playerSpriteRenderer.sharedMaterial = _originalMaterial;
    }

    private float GetHumanRatio()
    {
        float x             = playerTransform.position.x;
        float quarterLength = (blinkEndX - blinkStartX) / 4f;

        if (x < blinkStartX + quarterLength)       return ratioQ1;
        if (x < blinkStartX + quarterLength * 2f)  return ratioQ2;
        if (x < blinkStartX + quarterLength * 3f)  return ratioQ3;
        return ratioQ4;
    }

    // ── Church ────────────────────────────────────────────────────────────

    public void OnPlayerEnterChurch(Transform churchPosition)
    {
        StartCoroutine(EnterChurchSequence(churchPosition));
    }

    private IEnumerator EnterChurchSequence(Transform target)
    {
        StopCoroutine(BlinkCoroutine());
        if (playerAnimator != null) playerAnimator.enabled = true;

        playerInputHandler.enabled = false;
        boundaryClamp.enabled = false;
        yield return StartCoroutine(MovePlayerToPosition(target.position, 2f));

        playerAnimator.SetBool("IdleFront", false);
        playerInputHandler.enabled = true;

        if (_blizzardPS != null)
            yield return StartCoroutine(StopBlizzard(1.5f));

        hudManager?.OnClickCombatPosition();
    }

    private IEnumerator MovePlayerToPosition(Vector3 target, float duration)
    {
        Vector3 start = playerTransform.position;
        float elapsed = 0f;

        playerCC.enabled = false;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerTransform.position = Vector3.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        playerTransform.position = target;
        playerCC.enabled = true;
    }

    private IEnumerator StopBlizzard(float duration)
    {
        var emission = _blizzardPS.emission;
        float startRate = emission.rateOverTime.constant;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            emission.rateOverTime = Mathf.Lerp(startRate, 0f, elapsed / duration);
            yield return null;
        }

        emission.rateOverTime = 0f;
        _blizzardPS.Stop();
    }
}
