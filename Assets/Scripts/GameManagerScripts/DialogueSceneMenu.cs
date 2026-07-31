using UnityEngine;

public class DialogueSceneMenu : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private GameObject selectionPanel;

    [Header("Scene Transition")]
    [Tooltip("Escena de iluminación a la que vuelve la opción Test.")]
    [SerializeField] private string previousLightingSceneName = "LightingScene";

    private bool _isSwitchingScene;

    private void OnEnable()
    {
        _isSwitchingScene = false;

        if (selectionPanel != null)
            selectionPanel.SetActive(true);
    }

    public void SelectDialogue()
    {
        if (_isSwitchingScene) return;

        if (selectionPanel != null)
            selectionPanel.SetActive(false);
    }

    public void SelectTest()
    {
        if (_isSwitchingScene) return;

        _isSwitchingScene = true;

        IntroSequenceManager introSequenceManager =
            Object.FindFirstObjectByType<IntroSequenceManager>();

        if (introSequenceManager == null)
        {
            Debug.LogError(
                $"[{nameof(DialogueSceneMenu)}] No se encontró IntroSequenceManager para devolver el control al jugador.",
                this);
            _isSwitchingScene = false;
            return;
        }

        bool switched = introSequenceManager.ResumeAfterDialogueTest(
            previousLightingSceneName,
            gameObject.scene.name);

        if (!switched)
            _isSwitchingScene = false;
    }
}
