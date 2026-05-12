using System.Collections;
using Cinemachine;
using UnityEngine;

public class IntroSequenceManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController playerCC;
    [SerializeField] private PlayerBoundaryClamp boundaryClamp;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private HUDManager hudManager;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("Cámara — FOV intro")]
    [SerializeField] private CinemachineVirtualCamera introVirtualCamera;
    [SerializeField] private float introStartFOV  = 30f;
    [SerializeField] private float introEndFOV    = 60f;
    [SerializeField] private Color fadeEndBgColor = Color.black;

    [Header("Fase 1 — Pantalla negra")]
    [SerializeField] private float holdBlackDuration = 1f;

    [Header("Fase 2 — Fade + movimiento automático")]
    [SerializeField] private float fadeInDuration = 4f;
    [SerializeField] private float fadeExponent   = 2f;
    [SerializeField] private float autoWalkSpeed  = 3f;
    [SerializeField] private float introPlayerZ   = 0f;

   [Header("Blink — Sprites")]
    [SerializeField] private Sprite humanSprite;
    [SerializeField] private Sprite soulSprite;
    [SerializeField] private Material humanMaterial;

    [Header("Blink — Posición")]
    [SerializeField] private float blinkStartX   = -100f;
    [SerializeField] private float blinkEndX     = 0f;
    [SerializeField] private float blinkInterval = 0.5f;

    [Header("Blink — Ratio humano por cuarto (1=siempre humano, 0=siempre alma)")]
    [SerializeField] private float ratioQ1 = 1.00f;
    [SerializeField] private float ratioQ2 = 0.85f;
    [SerializeField] private float ratioQ3 = 0.50f;
    [SerializeField] private float ratioQ4 = 0.15f;

    [Header("Blink — Bloqueo de forma")]
    [SerializeField] private bool lockHumanForm = false;

    private Material _originalMaterial;
    private Material _spriteFadeMat;
    private Coroutine _blinkCoroutine;
    private CameraClearFlags _originalClearFlags;

    private void Awake()
    {
        // Fallback: buscar el SpriteRenderer automáticamente si no está asignado en Inspector
        if (playerSpriteRenderer == null && playerTransform != null)
            playerSpriteRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>();

        if (playerSpriteRenderer != null)
            _originalMaterial = playerSpriteRenderer.sharedMaterial;

        if (mainCamera != null)
            _originalClearFlags = mainCamera.clearFlags;

        _spriteFadeMat = new Material(Shader.Find("Sprites/Default"));
    }

    // ── Entrada pública desde HUDManager ─────────────────────────────────

    public void ShowBlackScreen()
    {
        if (mainCamera != null)
        {
            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
            introVirtualCamera.m_Lens.FieldOfView     = introStartFOV;
        }

        if (playerAnimator != null)     playerAnimator.enabled = false;
        if (playerInputHandler != null) playerInputHandler.enabled = false;
        if (playerTransform != null)    playerTransform.gameObject.SetActive(false);
        SetHumanForm();
    }

    public void StartIntro()
    {
        StartCoroutine(PlayIntroSequence());
    }

    // ── Secuencia principal ───────────────────────────────────────────────

    private IEnumerator PlayIntroSequence()
    {

        ActivatePlayerInvisible(); 

        yield return StartCoroutine(HoldBlack());
        yield return StartCoroutine(FadeFromBlack());

        if (playerInputHandler != null) playerInputHandler.enabled = true;
        _blinkCoroutine = StartCoroutine(BlinkCoroutine());
    }

    // ── Fase 1: pantalla negra + lerp de FOV ─────────────────────────────

    private IEnumerator HoldBlack()
    {
        if (holdBlackDuration <= 0f) yield break;

        // Fijar FOV de inicio — el lerp ocurre en FadeFromBlack para que sea visible
        if (mainCamera != null)
            introVirtualCamera.m_Lens.FieldOfView = introStartFOV;

        yield return new WaitForSecondsRealtime(holdBlackDuration);
    }

    // ── Fase 2: fade de fondo + jugador aparece caminando ────────────────

    // Ajusta posición Z mientras el jugador sigue inactivo — NO lo activa
    private void ActivatePlayerInvisible()
    {
        if (playerTransform == null) return;
        playerCC.enabled = false;
        Vector3 pos = playerTransform.position;
        pos.z = introPlayerZ;
        playerTransform.position = pos;
        Physics.SyncTransforms();
        playerCC.enabled = true;
    }

    private IEnumerator FadeFromBlack()
    {
        // Orden deliberado para blindar contra OnEnable que resetee el color:
        // 1) Apagar el renderer antes de activar el objeto
        if (playerSpriteRenderer != null) playerSpriteRenderer.enabled = false;
        // 2) Activar el objeto — todos los OnEnable disparan aquí de forma síncrona
        playerTransform.gameObject.SetActive(true);
        // 3) Usar material de fade — Sprites/Default respeta SpriteRenderer.color.a
        if (playerSpriteRenderer != null && _spriteFadeMat != null)
            playerSpriteRenderer.sharedMaterial = _spriteFadeMat;
        // 4) Forzar color a transparente con RGB limpio (fade IN: empieza invisible)
        if (playerSpriteRenderer != null)
            playerSpriteRenderer.color = new Color(1f, 1f, 1f, 0f);
        // 5) Reactivar renderer — el player renderiza a alpha=0 (invisible)
        if (playerSpriteRenderer != null) playerSpriteRenderer.enabled = true;
        yield return null;

        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t      = Mathf.Clamp01(elapsed / fadeInDuration);
            float reveal = Mathf.Pow(t, fadeExponent);

            if (mainCamera != null)
            {
                mainCamera.backgroundColor = Color.Lerp(Color.black, fadeEndBgColor, reveal);
                introVirtualCamera.m_Lens.FieldOfView     = Mathf.Lerp(introStartFOV, introEndFOV, reveal);
            }

            SetPlayerAlpha(reveal);
            playerCC.Move(Vector3.right * autoWalkSpeed * Time.deltaTime);

            yield return null;
        }

        SetPlayerAlpha(1f);
        // Restaurar material original ahora que el fade terminó
        if (playerSpriteRenderer != null && _originalMaterial != null)
            playerSpriteRenderer.sharedMaterial = _originalMaterial;
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = fadeEndBgColor;
            mainCamera.clearFlags      = _originalClearFlags;
        }
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
            if (lockHumanForm)
            {
                SetHumanForm();
                yield return null;
                continue;
            }

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
        if (playerSpriteRenderer == null) return;
        if (humanSprite != null)   playerSpriteRenderer.sprite = humanSprite;
        if (humanMaterial != null) playerSpriteRenderer.sharedMaterial = _spriteFadeMat;
    }

    private void SetSoulForm()
    {
        if (playerSpriteRenderer == null) return;
        if (soulSprite != null) playerSpriteRenderer.sprite = soulSprite;
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
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }
        SetSoulForm();

        if (playerAnimator != null) playerAnimator.enabled = true;

        playerInputHandler.enabled = false;
        boundaryClamp.enabled = false;
        yield return StartCoroutine(MovePlayerToPosition(target.position, 2f));

        playerAnimator.SetBool("IdleFront", false);
        playerInputHandler.enabled = true;

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
}
