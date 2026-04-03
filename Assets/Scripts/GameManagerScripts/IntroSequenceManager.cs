using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class IntroSequenceManager : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Image blackScreen;
    [SerializeField] private ParticleSystem blizzard;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController playerCC;
    [SerializeField] private PlayerBoundaryClamp boundaryClamp;
    [SerializeField] private MonoBehaviour playerInputHandler; // arrastra PlayerInputHandler aquí

    [Header("Tiempos")]
    [SerializeField] private float blizzardBuildUpDuration = 2f;
    [SerializeField] private float holdBlackScreenDuration = 1f;
    [SerializeField] private float fadeOutDuration = 2f;
    [SerializeField] private float autoWalkDuration = 3f;
    [SerializeField] private float autoWalkSpeed = 2f;

    [Header("Animaciones")]
    [SerializeField] private string walkAnimationParam = "IsWalking";

    private void Start()
    {
        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        // ── Estado inicial ─────────────────────────────────────────────────
        SetBlackScreen(1f);
        playerInputHandler.enabled = false;
        boundaryClamp.enabled = false;

        // ── Ventisca aparece mientras pantalla está en negro ───────────────
        if (blizzard != null)
        {
            blizzard.Play();
            yield return StartCoroutine(BuildUpBlizzard(blizzardBuildUpDuration));
        }

        yield return new WaitForSeconds(holdBlackScreenDuration);

        // ── Fade out de pantalla negra + auto-walk simultáneos ─────────────
        playerAnimator.SetBool(walkAnimationParam, true);
        StartCoroutine(FadeOutBlackScreen(fadeOutDuration));
        yield return StartCoroutine(AutoWalk(autoWalkDuration));

        // ── Input habilitado al terminar el auto-walk ──────────────────────
        playerAnimator.SetBool(walkAnimationParam, false);
        playerInputHandler.enabled = true;
        boundaryClamp.enabled = true;
    }

    private IEnumerator BuildUpBlizzard(float duration)
    {
        var emission = blizzard.emission;
        float startRate = 0f;
        float targetRate = emission.rateOverTime.constant;
        float elapsed = 0f;

        emission.rateOverTime = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            emission.rateOverTime = Mathf.Lerp(startRate, targetRate, elapsed / duration);
            yield return null;
        }

        emission.rateOverTime = targetRate;
    }

    private IEnumerator FadeOutBlackScreen(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            SetBlackScreen(Mathf.Lerp(1f, 0f, elapsed / duration));
            yield return null;
        }
        SetBlackScreen(0f);
        blackScreen.gameObject.SetActive(false);
    }

    private IEnumerator AutoWalk(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerCC.Move(Vector3.forward * autoWalkSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private void SetBlackScreen(float alpha)
    {
        Color c = blackScreen.color;
        c.a = alpha;
        blackScreen.color = c;
    }

    // Llamado desde ChurchDoorTrigger
    public void OnPlayerEnterChurch(Transform churchPosition)
    {
        StartCoroutine(EnterChurchSequence(churchPosition));
    }

    private IEnumerator EnterChurchSequence(Transform target)
    {
        // Desactiva input y mueve al player a la posición determinada
        playerInputHandler.enabled = false;
        boundaryClamp.enabled = false;
        playerAnimator.SetBool(walkAnimationParam, true);

        yield return StartCoroutine(MovePlayerToPosition(target.position, 2f));

        playerAnimator.SetBool(walkAnimationParam, false);
        playerInputHandler.enabled = true;

        // La ventisca para
        if (blizzard != null)
            yield return StartCoroutine(StopBlizzard(1.5f));
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
        var emission = blizzard.emission;
        float startRate = emission.rateOverTime.constant;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            emission.rateOverTime = Mathf.Lerp(startRate, 0f, elapsed / duration);
            yield return null;
        }

        emission.rateOverTime = 0f;
        blizzard.Stop();
    }
}