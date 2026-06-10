using UnityEngine;
using TMPro;

public class DebugPlayerStates : MonoBehaviour
{
    private PlayerController playerController;

    [SerializeField] private TMP_Text debugText;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (debugText == null) return;

        debugText.text = playerController.StateMachineController.GetDebugStateInfo();
    }

}
