using UnityEngine;

public class PlayerZoneTransformation : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Transform playerTransform;

    [Header("X Zones — player position")]
    [SerializeField] private float zone1End = -60f;
    [SerializeField] private float zone2End = -30f;
    // zone 3: from zone2End to the church

    [Header("Materials per zone")]
    [SerializeField] private Material zoneMaterial1; // initial form
    [SerializeField] private Material zoneMaterial2; // intermediate form
    [SerializeField] private Material zoneMaterial3; // pure white

    private int currentZone = 0;
    private Material currentMaterial;
    private Material targetMaterial;

    private void Start()
    {
        // Placeholder: material swap per zone
        // When you define the visual look, change this to color Lerp,
        // shader property Lerp, or full mesh swap
        currentZone = 1;
        if (zoneMaterial1 != null)
            playerRenderer.material = zoneMaterial1;
    }

    private void Update()
    {
        EvaluateZone();
    }

    private void EvaluateZone()
    {
        float x = playerTransform.position.x;

        int newZone;
        if (x < zone1End)
            newZone = 1;
        else if (x < zone2End)
            newZone = 2;
        else
            newZone = 3;

        if (newZone != currentZone)
        {
            currentZone = newZone;
            ApplyZoneMaterial(currentZone);
        }
    }

    private void ApplyZoneMaterial(int zone)
    {
        // Currently does a direct material swap
        // Replace with a Lerp once the shader is defined
        switch (zone)
        {
            case 1:
                if (zoneMaterial1 != null) playerRenderer.material = zoneMaterial1;
                break;
            case 2:
                if (zoneMaterial2 != null) playerRenderer.material = zoneMaterial2;
                break;
            case 3:
                if (zoneMaterial3 != null) playerRenderer.material = zoneMaterial3;
                break;
        }
    }

    // When the shader is ready, call this to do a color Lerp
    // instead of the direct swap
    public void SetMaterialColorLerp(Color target, float duration)
    {
        StartCoroutine(LerpMaterialColor(target, duration));
    }

    private System.Collections.IEnumerator LerpMaterialColor(Color target, float duration)
    {
        Color start = playerRenderer.material.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            playerRenderer.material.color = Color.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        playerRenderer.material.color = target;
    }
}
