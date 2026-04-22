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
    [SerializeField] private MonoBehaviour playerInputHandler; // arrastra PlayerInputHandler aquí
    [SerializeField] private HUDManager hudManager;

    [Header("Tiempos")]
    [SerializeField] private float blizzardBuildUpDuration = 2f;

    private ParticleSystem _blizzardPS;

    private void Awake()
    {
        if (blizzardRoot != null)
            _blizzardPS = blizzardRoot.GetComponentInChildren<ParticleSystem>();
    }

    private void Start()
    {
        StartCoroutine(PlayIntroSequence());
    }

    private IEnumerator PlayIntroSequence()
    {
        // ── Estado inicial ─────────────────────────────────────────────────
        playerInputHandler.enabled = false;
        boundaryClamp.enabled = false;

        // ── Ventisca aparece ───────────────────────────────────────────────
        if (_blizzardPS != null)
        {
            _blizzardPS.Play();
            yield return StartCoroutine(BuildUpBlizzard(blizzardBuildUpDuration));
        }


        playerInputHandler.enabled = true;
        boundaryClamp.enabled = true;
    }

    private IEnumerator BuildUpBlizzard(float duration)
    {
        var emission = _blizzardPS.emission;
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
        yield return StartCoroutine(MovePlayerToPosition(target.position, 2f));

        playerAnimator.SetBool("IdleFront", false);
        playerInputHandler.enabled = true;

        // La ventisca para
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