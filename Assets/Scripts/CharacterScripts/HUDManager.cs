using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private CameraMovement cameraMovement;
    [SerializeField] private GameObject enemy;
    [SerializeField] private GameObject enemyRio;
    [SerializeField] private PlayerBoundaryClamp playerBoundary;
    [SerializeField] GameObject _canvasMain;

    [Header("UI")]
    [SerializeField] private GameObject selectionPanel;

    [Header("Referencias")]
    [SerializeField] private Transform playerTransform;

    [SerializeField] private TutorialRingManager ringManager;

    [SerializeField] private GameObject playerTransformFront;
    [SerializeField] private Animator playerAnimator;

    [Header("Configuración")]
    [SerializeField] private float combatStartPositionY = 1.5f;

    [SerializeField] private float combatStartPositionZ = 0f;
    [SerializeField] private float combatStartPositionX = -90f;

    [SerializeField] private float freePosition = 13f;

    private void Awake()
    {
        _canvasMain.SetActive(true);
        Time.timeScale = 0f;
    }

    public void OnClickCombatPosition()
    {
        Time.timeScale = 1f;
        playerBoundary.enabled = false;
        enemy.SetActive(false);
        playerTransformFront.SetActive(false);

        enemyRio.SetActive(true);
        cameraMovement.freeMoveMode = false;
        cameraMovement.followPlayerX = false;
        ringManager?.RefreshStartPositions();
        ringManager.enabled = true;
        HidePanel();
    }

    // Llamado desde ChurchDoorTrigger: igual que OnClickCombatPosition
    // pero además resetea la cámara a su posición de combate original.
    public void OnEnterCombatFromTrigger()
    {
        OnClickCombatPosition();
        cameraMovement.ResetToCombatX();
    }

    public void OnClickStartHere()
    {
        Time.timeScale = 1f;
        playerBoundary.enabled = true;
        enemy.SetActive(false);
        enemyRio.SetActive(false);
        playerTransformFront.SetActive(false);
        cameraMovement.freeMoveMode = false;
        MovePlayerToX(combatStartPositionX);
        cameraMovement.RefreshCombatX();
        cameraMovement.followPlayerX = true;
        ringManager.enabled = false;
        playerAnimator?.SetBool("IdleFront", true);
        HidePanel();
    }

    // Llamar desde TutorialRingManager.onEnemyOut.
    // Desactiva el ring y pasa a modo libre manteniendo a RioTutte activo (siguiente fase).
    public void OnRingPhaseComplete()
    {
        ringManager.enabled = false;
        playerBoundary.enabled = false;
        cameraMovement.freeMoveMode = true;

        var rioTutte = enemyRio.GetComponent<RioTutteEnemy>();
        rioTutte.AdvancePhase();   // 0 → 1: activa DashGrab
        rioTutte.facePlayerByZ = true;
    }

    public void OnClickFreeMove()
    {
        Time.timeScale = 1f;
        playerTransformFront.SetActive(true);
        ringManager.enabled = false;
        playerBoundary.enabled = false;
        enemy.SetActive(true);
        enemyRio.SetActive(false);
        cameraMovement.freeMoveMode = true;
        MovePlayer(freePosition);
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

    private void MovePlayer(float z)
    {
        CharacterController cc = playerTransform.GetComponent<CharacterController>();

        cc.enabled = false;

        Vector3 pos = playerTransform.position;
        pos.z = z;
        playerTransform.position = pos;

        cc.enabled = true;
    }

    private void HidePanel()
    {
        selectionPanel.SetActive(false);
    }
}
