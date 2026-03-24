using UnityEngine;

/// <summary>
/// Responsabilidad única: escalar al jugador en función de su posición Z
/// para simular profundidad en un entorno 2.5D.
/// La escala va de maxScale (cuando Z == minZ) a minScale (cuando Z == maxZ).
/// Preserva el signo en X para que los flips de sprite funcionen correctamente.
/// </summary>
public class PlayerDepthScaler : MonoBehaviour
{
    // ==================================================
    // Z BOUNDARIES (2.5D DEPTH)
    // ==================================================
    [Header("Z Boundaries")]
    [Tooltip("Posición Z más cercana a cámara (escala máxima).")]
    public float minZ = 0f;

    [Tooltip("Posición Z más alejada de cámara (escala mínima).")]
    public float maxZ = 100f;

    [Tooltip("Escala mínima (fondo del escenario).")]
    public float minScale = 0.3f;

    [Tooltip("Escala máxima (frente del escenario).")]
    public float maxScale = 1f;

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Recalcula y aplica la escala del transform según la Z actual.
    /// Debe llamarse cada frame antes de aplicar movimiento.
    /// </summary>
    public void UpdateScaleBasedOnZ()
    {
        float z = transform.position.z;
        float t = Mathf.Clamp01(Mathf.InverseLerp(minZ, maxZ, z));
        float scaleFactor = Mathf.Lerp(maxScale, minScale, t);

        // Preservamos el signo en X para no romper los flips de sprite
        float signX = Mathf.Sign(transform.localScale.x);
        transform.localScale = new Vector3(scaleFactor * signX, scaleFactor, scaleFactor);
    }
}
