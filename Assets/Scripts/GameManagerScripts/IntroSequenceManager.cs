using System.Collections;
using UnityEngine;

public class IntroSequenceManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private GameObject blizzardRoot;
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController playerCC;
    [SerializeField] private PlayerBoundaryClamp boundaryClamp;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private HUDManager hudManager;

    [Header("Fase 1 — Quad negro + nieve")]
    [SerializeField] private MeshRenderer blackScreenQuad;
    [SerializeField] private float holdBlackDuration = 2f;

    [Header("Fase 2 — Reveal del jugador")]
    [SerializeField] private MeshRenderer fadeQuad;
    [SerializeField] private float fadeInDuration = 4f;
    // 1 = lineal | 2 = oscuro más tiempo, revela al final | 3 = muy dramático
    [SerializeField] private float fadeExponent   = 2f;

    [Header("Jugador — aparición")]
    [SerializeField] private float autoWalkSpeed = 3f;
    [SerializeField] private float introPlayerZ  = 0f;

    [Header("Cámara intro")]
    [SerializeField] private float introStartFOV = 20f;
    [SerializeField] private float introEndFOV   = 40f;

    [Header("Blizzard")]
    [SerializeField] private float _fullBlizzardRate = 2000f;

    [Header("Blink — Sprites")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;
    [SerializeField] private Sprite humanSprite;
    [SerializeField] private Sprite soulSprite;
    [SerializeField] private Material humanMaterial;

    private Material _originalMaterial;
    private Material _quadMaterial;
    private Material _fadeMaterial;
    private CameraClearFlags _originalClearFlags;
    private Color _originalBgColor;

    [Header("Blink — Posición")]
    [SerializeField] private float blinkStartX   = -100f;
    [SerializeField] private float blinkEndX     = 0f;
    [SerializeField] private float blinkInterval = 0.5f;

    [Header("Blink — Ratio humano por cuarto (1=siempre humano, 0=siempre alma)")]
    [SerializeField] private float ratioQ1 = 1.00f;
    [SerializeField] private float ratioQ2 = 0.85f;
    [SerializeField] private float ratioQ3 = 0.50f;
    [SerializeField] private float ratioQ4 = 0.15f;

    private ParticleSystem _blizzardPS;
    private Coroutine _blinkCoroutine;

    private void Awake()
    {
        if (blizzardRoot != null)
            _blizzardPS = blizzardRoot.GetComponentInChildren<ParticleSystem>();

        if (playerSpriteRenderer != null)
            _originalMaterial = playerSpriteRenderer.sharedMaterial;

        if (blackScreenQuad != null)
            _quadMaterial = blackScreenQuad.material;

        if (fadeQuad != null)
            _fadeMaterial = fadeQuad.material;

        if (mainCamera != null)
        {
            _originalClearFlags = mainCamera.clearFlags;
            _originalBgColor    = mainCamera.backgroundColor;
        }
    }

    public void ShowBlackScreen()
    {
        // Quad negro cubre la escena
        if (blackScreenQuad != null)
            blackScreenQuad.gameObject.SetActive(true);

        // Cámara: fondo negro + FOV inicial cerrado
        if (mainCamera != null)
        {
            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
            mainCamera.fieldOfView     = introStartFOV;
        }

        // Prepara al jugador — Combat Directly no pasa por aquí
        if (playerAnimator != null) playerAnimator.enabled = false;
        if (playerInputHandler != null) playerInputHandler.enabled = false;
        if (playerTransform != null) playerTransform.gameObject.SetActive(false);
        SetHumanForm();
    }

    public void StartIntro()
    {
        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        // Blizzard a tope ANTES del fade — la tormenta ya está cuando la pantalla se abre
        if (_blizzardPS != null)
        {
            var emission = _blizzardPS.emission;
            emission.rateOverTime = _fullBlizzardRate;
            _blizzardPS.Play();
        }

        // Activar player invisible durante el hold — tiene holdBlackDuration segundos para posicionarse en el suelo
        ActivatePlayerInvisible();

        yield return StartCoroutine(HoldBlackWithFOV());
        yield return StartCoroutine(FadeFromBlack());

        if (playerInputHandler != null) playerInputHandler.enabled = true;
        _blinkCoroutine = StartCoroutine(BlinkCoroutine());
    }

    // ── Hold negro: FOV se abre de introStartFOV → introEndFOV ───────────

    private IEnumerator HoldBlackWithFOV()
    {
        if (holdBlackDuration <= 0f) yield break;

        if (mainCamera != null)
            mainCamera.fieldOfView = introStartFOV;

        yield return new WaitForSecondsRealtime(holdBlackDuration);
    }

    // ── Fade: swap de quads, jugador camina ──────────────────────────────

    private void ActivatePlayerInvisible()
    {
        if (playerTransform == null) return;
        playerCC.enabled = false;
        Vector3 pos = playerTransform.position;
        pos.z = introPlayerZ;
        playerTransform.position = pos;
        Physics.SyncTransforms();
        playerCC.enabled = true;
        SetPlayerAlpha(0f);
        playerTransform.gameObject.SetActive(true);
    }

    private IEnumerator FadeFromBlack()
    {
        // Activar el transparente con alpha=1 PRIMERO, esperar un frame, luego quitar el opaco
        SetFadeQuadAlpha(1f);
        if (fadeQuad != null) fadeQuad.gameObject.SetActive(true);
        yield return null;
        if (blackScreenQuad != null) blackScreenQuad.gameObject.SetActive(false);

        // Fade: quad desaparece, FOV se abre, jugador aparece caminando — todo a la vez
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t      = Mathf.Clamp01(elapsed / fadeInDuration);
            float reveal = Mathf.Pow(t, fadeExponent);

            SetFadeQuadAlpha(1f - reveal);
            SetPlayerAlpha(reveal);
            if (mainCamera != null)
                mainCamera.fieldOfView = Mathf.Lerp(introStartFOV, introEndFOV, t);
            playerCC.Move(Vector3.right * autoWalkSpeed * Time.deltaTime);

            yield return null;
        }

        if (mainCamera != null)
            mainCamera.fieldOfView = introEndFOV;

        SetFadeQuadAlpha(0f);
        SetPlayerAlpha(1f);
        if (fadeQuad != null) fadeQuad.gameObject.SetActive(false);

        if (mainCamera != null)
        {
            mainCamera.clearFlags      = _originalClearFlags;
            mainCamera.backgroundColor = _originalBgColor;
        }
    }

    private void SetFadeQuadAlpha(float alpha)
    {
        if (_fadeMaterial == null) return;
        Color c = _fadeMaterial.color;
        c.a = alpha;
        _fadeMaterial.color = c;
        if (_fadeMaterial.HasProperty("_BaseColor"))
            _fadeMaterial.SetColor("_BaseColor", c);
    }

    private void SetPlayerAlpha(float alpha)
    {
        if (playerSpriteRenderer == null) return;
        Color c = playerSpriteRenderer.color;
        c.a = alpha;
        playerSpriteRenderer.color = c;
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

    public void StopBlinkAndGoSoul()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }
        SetSoulForm();
    }

    private IEnumerator EnterChurchSequence(Transform target)
    {
        StopBlinkAndGoSoul();
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