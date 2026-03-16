using System.Collections;
using UnityEngine;

public class FireVisualScript : MonoBehaviour
{
    [Range(0, 2)] public float fluctuationTime;
    [Range(0, 5)] public float fluctuationRate;

    public Light fireLight;
    public float baseIntensity;

    public void Awake()
    {
        fireLight = GetComponent<Light>();
        baseIntensity = fireLight.intensity;
        StartCoroutine(FireFluctuation());
    }

    public IEnumerator FireFluctuation()
    {
        float targetUp = baseIntensity + fluctuationRate;
        float targetDown = baseIntensity;

        while (true)
        {
            while (fireLight.intensity < targetUp)
            {
                fireLight.intensity += (fluctuationRate / fluctuationTime) * Time.deltaTime;
                yield return null;
            }

            while (fireLight.intensity > targetDown)
            {
                fireLight.intensity -= (fluctuationRate / fluctuationTime) * Time.deltaTime;
                yield return null;
            }
        }
    }
}
