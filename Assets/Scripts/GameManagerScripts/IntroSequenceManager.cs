using System.Collections;
using Cinemachine;
using UnityEngine;

public class IntroSequenceManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController playerCC;
    [SerializeField] private PlayerBoundaryClamp boundaryClamp;
    [SerializeField] private PlayerInputHandler playerInputHandler;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private HUDManager hudManager;
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    [Header("Camera — Intro FOV")]
    [SerializeField] private CinemachineVirtualCamera introVirtualCamera;
    [SerializeField] private float introStartFOV  = 30f;
    [SerializeField] private float introEndFOV    = 60f;
    [SerializeField] private Color fadeEndBgColor = Color.black;

    [Header("Phase 1 — Black Screen")]
    [SerializeField] private float holdBlackDuration = 1f;

    [Header("Phase 2 — Fade + auto movement")]
    [SerializeField] private float fadeInDuration = 4f;
    [SerializeField] private float fadeExponent   = 2f;
    [Tooltip("Mueve al jugador automáticamente en X mientras se revela durante el fade.")]
    [SerializeField] private bool autoWalkEnabled = true;
    [SerializeField] private float autoWalkSpeed  = 3f;
    [SerializeField] private float introPlayerZ   = 0f;

    [Header("Blink — Position")]
    [SerializeField] private float blinkStartX   = -100f;
    [SerializeField] private float blinkEndX     = 0f;
    [SerializeField] private float blinkInterval = 0.5f;

    [Header("Blink — Human ratio per quarter (1=always human, 0=always soul)")]
    [SerializeField] private float ratioQ1 = 1.00f;
    [SerializeField] private float ratioQ2 = 0.85f;
    [SerializeField] private float ratioQ3 = 0.50f;
    [SerializeField] private float ratioQ4 = 0.15f;

    [Header("Blink — Form Lock")]
    [SerializeField] private bool lockHumanForm = false;

    [Header("Animator — Intro Controller")]
    [SerializeField] private RuntimeAnimatorController introAnimatorController;

    private RuntimeAnimatorController _originalAnimatorController;
    private Material _originalMaterial;
    private Material _spriteFadeMat;
    private Coroutine _blinkCoroutine;
    private CameraClearFlags _originalClearFlags;

    public float IntroPlayerZ => introPlayerZ;


    private bool isWalking;
    private bool played;

    private float _timer;
    private float _currentCooldown;

    

    private void Awake()
    {
        if (playerSpriteRenderer == null && playerTransform != null)
            playerSpriteRenderer = playerTransform.GetComponentInChildren<SpriteRenderer>();

        if (playerController == null && playerTransform != null)
            playerController = playerTransform.GetComponent<PlayerController>();

        if (playerSpriteRenderer != null)
            _originalMaterial = playerSpriteRenderer.sharedMaterial;

        if (mainCamera != null)
            _originalClearFlags = mainCamera.clearFlags;

        _spriteFadeMat = new Material(Shader.Find("Sprites/Default"));

    }

    private void Update()
    {
        if (isWalking && !played)
        {
            _timer += Time.deltaTime;

            if (_timer >= 5f)
            {
                playerAnimator.SetTrigger("TransformToGhost");
                played = true;
            }
        }
    }

    public void ShowBlackScreen()
    {
        if (playerController != null)
        {
            playerController.ResetToIdle();
            playerController.enabled = false;
        }

        if (mainCamera != null)
        {
            mainCamera.clearFlags      = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
            introVirtualCamera.m_Lens.FieldOfView = introStartFOV;
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

    private IEnumerator PlayIntroSequence()
    {
        ActivatePlayerInvisible();

        yield return StartCoroutine(HoldBlack());
        yield return StartCoroutine(FadeFromBlack());

        KeepPlayerAtIntroDepth();
        if (playerCC != null) playerCC.enabled = true;
        if (playerInputHandler != null) playerInputHandler.enabled = true;
        if (playerController != null) playerController.enabled = true;

        _blinkCoroutine = StartCoroutine(BlinkCoroutine());
    }

    private IEnumerator HoldBlack()
    {
        if (holdBlackDuration <= 0f) yield break;

        if (mainCamera != null)
            introVirtualCamera.m_Lens.FieldOfView = introStartFOV;

        yield return new WaitForSecondsRealtime(holdBlackDuration);
    }

    private void ActivatePlayerInvisible()
    {
        if (playerTransform == null) return;
        if (playerCC != null) playerCC.enabled = false;
        KeepPlayerAtIntroDepth();
        Physics.SyncTransforms();
    }

    private IEnumerator FadeFromBlack()
    {
        if (playerSpriteRenderer != null) playerSpriteRenderer.enabled = false;
        playerTransform.gameObject.SetActive(true);
        //if (playerSpriteRenderer != null && _spriteFadeMat != null)
        //    playerSpriteRenderer.sharedMaterial = _spriteFadeMat;
        if (playerSpriteRenderer != null)
            playerSpriteRenderer.color = new Color(1f, 1f, 1f, 0f);
        if (playerSpriteRenderer != null) playerSpriteRenderer.enabled = true;

        if (playerAnimator != null && introAnimatorController != null)
        {
            _originalAnimatorController = playerAnimator.runtimeAnimatorController;
            playerAnimator.runtimeAnimatorController = introAnimatorController;
        }

        if (playerAnimator != null)
        {
            playerAnimator.enabled = true;
            playerAnimator.SetBool("IdleFrontHuman", true);
            playerAnimator.SetBool("IdleFront", false);
        }

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
                introVirtualCamera.m_Lens.FieldOfView = Mathf.Lerp(introStartFOV, introEndFOV, reveal);
            }

            SetPlayerAlpha(reveal);
            if (autoWalkEnabled)
            {
                Vector3 pos = playerTransform.position;
                pos.x += autoWalkSpeed * Time.unscaledDeltaTime;
                pos.z = introPlayerZ;
                playerTransform.position = pos;
            }
            else
            {
                KeepPlayerAtIntroDepth();
            }

            yield return null;
        }

        SetPlayerAlpha(1f);
        if (playerSpriteRenderer != null && _originalMaterial != null)
            playerSpriteRenderer.sharedMaterial = _originalMaterial;
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = fadeEndBgColor;
            mainCamera.clearFlags      = _originalClearFlags;
        }
        played = false;
        isWalking = true;
    }

    private void KeepPlayerAtIntroDepth()
    {
        if (playerTransform == null) return;

        Vector3 pos = playerTransform.position;
        pos.z = introPlayerZ;
        playerTransform.position = pos;
    }

    private void SetPlayerAlpha(float alpha)
    {
        if (playerSpriteRenderer == null) return;
        Color c = playerSpriteRenderer.color;
        c.a = alpha;
        playerSpriteRenderer.color = c;
    }

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
        if (playerAnimator == null || !playerAnimator.enabled) return;
        playerAnimator.SetBool("IdleFrontHuman", true);
        playerAnimator.SetBool("IdleFront", false);
    }

    private void SetSoulForm()
    {
        if (playerAnimator == null || !playerAnimator.enabled) return;
        playerAnimator.SetBool("IdleFront", true);
        playerAnimator.SetBool("IdleFrontHuman", false);
    }

    private float GetHumanRatio()
    {
        float x             = playerTransform.position.x;
        float quarterLength = (blinkEndX - blinkStartX) / 4f;

        if (x < blinkStartX + quarterLength)      return ratioQ1;
        if (x < blinkStartX + quarterLength * 2f) return ratioQ2;
        if (x < blinkStartX + quarterLength * 3f) return ratioQ3;
        return ratioQ4;
    }

    public void RestoreOriginalAnimator()
    {
        if (playerAnimator != null && _originalAnimatorController != null)
            playerAnimator.runtimeAnimatorController = _originalAnimatorController;
    }

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

        RestoreOriginalAnimator();

        if (playerAnimator != null) playerAnimator.enabled = true;
        SetSoulForm();

        playerInputHandler.enabled = false;
        boundaryClamp.enabled = true;
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
