using UnityEngine;

public class ChurchDoorTrigger : MonoBehaviour
{
    [Header("Sequence")]
    [SerializeField] private IntroSequenceManager introSequenceManager;
    [SerializeField] private Transform firstAutoMoveTarget;
    [SerializeField, Min(0f)] private float firstAutoMoveDuration = 2f;
    [SerializeField] private Transform secondAutoMoveTarget;
    [SerializeField, Min(0f)] private float secondAutoMoveDuration = 3f;

    [Header("RioTutte Visuals")]
    [SerializeField] private Transform rioTutteStandard;
    [SerializeField] private Transform rioTutteTransformation;

    [Header("Camera")]
    [Tooltip("Altura temporal de cámara durante los automoves.")]
    [SerializeField] private float autoMoveCameraY = 3f;

    [Header("Scene Transition")]
    [Tooltip("Escena que sustituye a la escena de iluminación al terminar el segundo automove.")]
    [SerializeField] private string dialogueSceneName = "DialogueScene";
    [Tooltip("Escena de iluminación que se descarga al mostrar el menú.")]
    [SerializeField] private string lightingSceneName = "LightingScene";

    [Header("Detection")]
    [SerializeField] private LayerMask playerLayer;

    private bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if ((playerLayer.value & (1 << other.gameObject.layer)) == 0) return;

        if (introSequenceManager == null)
        {
            Debug.LogError("[ChurchDoorTrigger] IntroSequenceManager no está asignado.", this);
            return;
        }

        triggered = introSequenceManager.TryStartChurchSequence(
            firstAutoMoveTarget,
            firstAutoMoveDuration,
            secondAutoMoveTarget,
            secondAutoMoveDuration,
            rioTutteStandard,
            rioTutteTransformation,
            autoMoveCameraY,
            dialogueSceneName,
            lightingSceneName);
    }
}
