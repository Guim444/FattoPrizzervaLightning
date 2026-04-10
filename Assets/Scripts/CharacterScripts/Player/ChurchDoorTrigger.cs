using UnityEngine;

public class ChurchDoorTrigger : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private IntroSequenceManager introManager;
    [SerializeField] private Transform churchInsidePosition;
    [SerializeField] private Light[] lightsToTransition;

    [Header("Iluminación")]
    [SerializeField] private float targetLightIntensity = 1.5f;
    [SerializeField] private float lightTransitionDuration = 2f;

    private bool triggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        introManager.OnPlayerEnterChurch(churchInsidePosition);
        StartCoroutine(TransitionLights());
    }

    private System.Collections.IEnumerator TransitionLights()
    {
        float elapsed = 0f;
        float[] startIntensities = new float[lightsToTransition.Length];

        for (int i = 0; i < lightsToTransition.Length; i++)
            startIntensities[i] = lightsToTransition[i].intensity;

        while (elapsed < lightTransitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / lightTransitionDuration;

            for (int i = 0; i < lightsToTransition.Length; i++)
                lightsToTransition[i].intensity = Mathf.Lerp(
                    startIntensities[i], targetLightIntensity, t);

            yield return null;
        }
    }
}
