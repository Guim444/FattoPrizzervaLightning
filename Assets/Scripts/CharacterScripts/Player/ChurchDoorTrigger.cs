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

    [Header("Church Lights")]
    [Tooltip("Luz CHR_Player_Light_Surroundd que se apaga al activar el trigger.")]
    [SerializeField] private Transform playerSurroundLight;

    [Header("Camera")]
    [Tooltip("Altura temporal de cámara durante los automoves.")]
    [SerializeField] private float autoMoveCameraY = 3f;

    [Header("Dialogue Layout")]
    [Tooltip("Escena que contiene únicamente el canvas usado durante el diálogo.")]
    [SerializeField] private string dialogueSceneName = "DialogueScene";
    [Tooltip("Escena que permanece activa y contiene DialogueLayoutManager.")]
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
            playerSurroundLight,
            autoMoveCameraY,
            dialogueSceneName,
            lightingSceneName);
    }
}
