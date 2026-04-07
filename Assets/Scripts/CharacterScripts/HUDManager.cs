using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private CameraMovement cameraMovement;
    [SerializeField] private GameObject enemy;
    [SerializeField] private GameObject enemyRio;
    [SerializeField] private PlayerBoundaryClamp playerBoundary;

    [Header("UI")]
    [SerializeField] private GameObject selectionPanel;

    [Header("Referencias")]
    [SerializeField] private Transform playerTransform;

    [SerializeField] private GameObject playerTransformFront;

    [Header("Configuración")]
    [SerializeField] private float combatStartPositionY = 1.5f;

    [SerializeField] private float combatStartPositionZ = 0f;
    [SerializeField] private float combatStartPositionX = -90f;

    [SerializeField] private float freePosition = 13f;

    public void OnClickCombatPosition()
    {
        playerBoundary.enabled = false;
        enemy.SetActive(false);
        playerTransformFront.SetActive(false);

        enemyRio.SetActive(true);
        cameraMovement.freeMoveMode = false;
        HidePanel();
    }

    public void OnClickStartHere()
    {
        playerBoundary.enabled = true;
        enemy.SetActive(false);
        enemyRio.SetActive(true);
        playerTransformFront.SetActive(false);

        cameraMovement.freeMoveMode = false;
        MovePlayerToX(combatStartPositionX);
        cameraMovement.RefreshCombatX();
        HidePanel();
    }

    public void OnClickFreeMove()
    {
        playerTransformFront.SetActive(true);

        playerBoundary.enabled = false;
        enemy.SetActive(true);
        enemyRio.SetActive(false);
        cameraMovement.freeMoveMode = true;
        MovePlayer(freePosition);
        HidePanel();
    }

    private void MovePlayerToX(float x)
    {
        CharacterController cc = playerTransform.GetComponent<CharacterController>();

        cc.enabled = false;
        playerTransform.position = new Vector3(x, combatStartPositionY, combatStartPositionZ);
        cc.enabled = true;
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
