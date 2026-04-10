using UnityEngine;

public class PlayerZoneTransformation : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Renderer playerRenderer;
    [SerializeField] private Transform playerTransform;

    [Header("Zonas en X — posición del player")]
    [SerializeField] private float zone1End = -60f;
    [SerializeField] private float zone2End = -30f;
    // zona 3: desde zone2End hasta la iglesia

    [Header("Materiales por zona")]
    [SerializeField] private Material zoneMaterial1; // forma inicial
    [SerializeField] private Material zoneMaterial2; // forma intermedia
    [SerializeField] private Material zoneMaterial3; // blanco puro

    [Header("Velocidad de transición")]
    [SerializeField] private float transitionSpeed = 1f;

    private int currentZone = 0;
    private Material currentMaterial;
    private Material targetMaterial;

    private void Start()
    {
        // Placeholder: swap de materiales por zona
        // Cuando definas el look visual, aquí puedes cambiar a Lerp de color,
        // Lerp de shader property, o swap de mesh completo
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
        // Por ahora hace un swap directo de material
        // Reemplaza esto con un Lerp cuando tengas el shader definido
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

    // Cuando tengas el shader, llama esto para hacer un Lerp de color
    // en lugar del swap directo
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