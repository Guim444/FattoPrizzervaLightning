using Cinemachine;
using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private GameObject enemy;
    [SerializeField] private GameObject enemyRio;
    [SerializeField] private PlayerBoundaryClamp playerBoundary;
    [SerializeField] GameObject _canvasMain;
    [SerializeField] GameObject _canvasUIStats;

    [Header("UI")]
    [SerializeField] private GameObject selectionPanel;

    [Header("Referencias")]
    [SerializeField] private GameObject OutsideChurch;

    [SerializeField] private Transform playerTransform;

    [SerializeField] private PlayableAreaManager playableAreaManager;
    [SerializeField] private IntroSequenceManager introSequenceManager;

    [SerializeField] private TutorialRingManager ringManager;

    // [SerializeField] GameObject outSide;
    [SerializeField] private GameObject playerTransformFront;
    [SerializeField] private Animator playerAnimator;

    [Header("Configuración — Jugador")]
    [SerializeField] private float startPositionX = 0f;

    [SerializeField] private float startPositionY = 2.25f;
    [SerializeField] private float combatStartPositionX = 0f;
    [SerializeField] private float combatStartPositionY = 2.25f;
    [SerializeField] private float combatStartPositionZ = -2f;

    [Header("Configuración — Cámara")]
    [SerializeField] private Vector3 combatCameraPosition = new Vector3(-11f, 3.35f, 0f);
    [SerializeField] private CinemachineZoomController cinemachineZoomController;
    [SerializeField] private CinemachineBrain cinemachineBrain;
    [SerializeField] private CameraMovement cameraMovement;


    private void Awake()
    {
        _canvasMain.SetActive(true);
        Time.timeScale = 0f;
    }

    private void ActivateCinemachine()
    {
        cameraMovement.enabled = false;
        if (cinemachineBrain != null) cinemachineBrain.enabled = true;
        cinemachineZoomController?.gameObject.SetActive(true);
    }

    private void ActivateCameraMovement()
    {
        cinemachineZoomController?.gameObject.SetActive(false);
        if (cinemachineBrain != null) cinemachineBrain.enabled = false;
        cameraMovement.enabled = true;
    }

    public void OnClickCombatPosition()
    {
        Time.timeScale = 1f;
        ActivateCinemachine();
        playerBoundary.enabled = false;
        enemy.SetActive(false);
        playerTransformFront.SetActive(false);
        enemyRio.SetActive(true);
        MovePlayerToX(combatStartPositionX);
        playableAreaManager.SetActive(true);
        ringManager?.RefreshStartPositions();
        ringManager.enabled = true;
        HidePanel();
    }

    public void OnEnterCombatFromTrigger()
    {
        OnClickCombatPosition();
    }

    public void OnClickStartHere()
    {
        _canvasUIStats.SetActive(false);
        playableAreaManager.SetActive(false);
        ActivateCinemachine();
        introSequenceManager?.ShowBlackScreen();
        Time.timeScale = 1f;
        playerBoundary.enabled = true;
        OutsideChurch.SetActive(false);
        enemy.SetActive(false);
        enemyRio.SetActive(false);
        playerTransformFront.SetActive(false);
        MoveStartPlayerToX(startPositionX);
        ringManager.enabled = false;
        playerAnimator?.SetBool("IdleFront", true);
        HidePanel();
        introSequenceManager?.StartIntro();
    }

    // Llamar desde TutorialRingManager.onEnemyOut.
    // Desactiva el ring y pasa a modo libre manteniendo a RioTutte activo (siguiente fase).
    public void OnRingPhaseComplete()
    {
        ringManager.enabled = false;
        playerBoundary.enabled = false;
        var rioTutte = enemyRio.GetComponent<RioTutteEnemy>();
        rioTutte.AdvancePhase(); // 0 → 1: activa DashGrab
        rioTutte.facePlayerByZ = true;
    }

    public void OnClickFreeMove()
    {
        Time.timeScale = 1f;
        ActivateCameraMovement();
        playerTransformFront.SetActive(true);
        ringManager.enabled = false;
        playerBoundary.enabled = false;
        enemy.SetActive(true);
        OutsideChurch.SetActive(true);
        enemyRio.SetActive(false);
        MovePlayerToX(combatStartPositionX);
        HidePanel();
    }

    private void MovePlayerToX(float x)
    {
        if (playerTransform == null)
        {
            Debug.LogError("[HUDManager] playerTransform no está asignado en el Inspector.");
            return;
        }

        Debug.Log(
            $"[HUDManager] MovePlayerToX — antes: {playerTransform.position}  →  destino X:{x} Y:{combatStartPositionY} Z:{combatStartPositionZ}");

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerTransform.position = new Vector3(x, combatStartPositionY, combatStartPositionZ);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        Debug.Log($"[HUDManager] MovePlayerToX — después: {playerTransform.position}");
    }

    private void MoveStartPlayerToX(float x)
    {
        if (playerTransform == null)
        {
            Debug.LogError("[HUDManager] playerTransform no está asignado en el Inspector.");
            return;
        }

        Debug.Log(
            $"[HUDManager] MovePlayerToX — antes: {playerTransform.position}  →  destino X:{x} Y:{combatStartPositionY} Z:{combatStartPositionZ}");

        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        playerTransform.position = new Vector3(x, startPositionY, combatStartPositionZ);
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        Debug.Log($"[HUDManager] MovePlayerToX — después: {playerTransform.position}");
    }

    private void HidePanel()
    {
        _canvasMain.SetActive(false);
    }
}
