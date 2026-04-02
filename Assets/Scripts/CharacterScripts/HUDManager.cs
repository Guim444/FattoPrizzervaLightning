using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [SerializeField] private CameraMovement cameraMovement;
    [SerializeField] private GameObject enemy;
    [SerializeField] private PlayerBoundaryClamp playerBoundary;

    [Header("UI")]
    [SerializeField] private GameObject selectionPanel;

    [Header("Referencias")]
    [SerializeField] private Transform playerTransform;

    [Header("Configuración")]
    [SerializeField] private float combatStartPositionY = 1.5f;
    [SerializeField] private float combatStartPositionZ = 0f;
    [SerializeField] private float combatStartPositionX = -90f;

    [SerializeField] private float freePosition = 13f;

    public void OnClickCombatPosition()
    {
        playerBoundary.enabled = false;
        cameraMovement.freeMoveMode = false;
        HidePanel();
    }

    public void OnClickStartHere()
    {
        playerBoundary.enabled = true;
        enemy.SetActive(false);
        cameraMovement.freeMoveMode = false;
        MovePlayerToX(combatStartPositionX);
        HidePanel();
    }

    public void OnClickFreeMove()
    {
        playerBoundary.enabled = false;
        enemy.SetActive(false);
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
