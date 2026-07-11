using UnityEngine;

/// <summary>
/// Single responsibility: scale the player based on their Z position
/// to simulate depth in a 2.5D environment.
/// Scale goes from maxScale (when Z == minZ) to minScale (when Z == maxZ).
/// Preserves the sign on X so sprite flips work correctly.
/// </summary>
public class PlayerDepthScaler : MonoBehaviour
{
    // ==================================================
    // Z BOUNDARIES (2.5D DEPTH)
    // ==================================================
    [Header("Z Boundaries")]
    [Tooltip("Closest Z position to camera (maximum scale).")]
    public float minZ = 0f;

    [Tooltip("Farthest Z position from camera (minimum scale).")]
    public float maxZ = 100f;

    [Tooltip("Minimum scale (back of the stage).")]
    public float minScale = 0.3f;

    [Tooltip("Maximum scale (front of the stage).")]
    public float maxScale = 1f;

    // ==================================================
    // PUBLIC API
    // ==================================================

    /// <summary>
    /// Recalculates and applies the transform scale based on the current Z.
    /// Must be called every frame before applying movement.
    /// </summary>
    public void UpdateScaleBasedOnZ()
    {
        float z = transform.position.z;
        float t = Mathf.Clamp01(Mathf.InverseLerp(minZ, maxZ, z));
        float scaleFactor = Mathf.Lerp(maxScale, minScale, t);

        float signX = Mathf.Sign(transform.localScale.x);
        if (Mathf.Approximately(signX, 0f))
            signX = 1f;

        transform.localScale = new Vector3(scaleFactor * signX, scaleFactor, scaleFactor);
    }
}
