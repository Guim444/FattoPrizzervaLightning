using UnityEngine;

/// <summary>
/// Clamps the player's Z position (free movement zone).
/// Used by HUDManager to restrict movement outside of combat.
/// </summary>
public class PlayerBoundaryClamp : MonoBehaviour
{
    [Header("Z Limits")]
    public float minZ = -5f;
    public float maxZ =  5f;

    private CharacterController _cc;

    private void Awake() => _cc = GetComponent<CharacterController>();

    private void LateUpdate()
    {
        Vector3 pos = transform.position;
        if (pos.z >= minZ && pos.z <= maxZ) return;

        _cc.enabled        = false;
        pos.z              = Mathf.Clamp(pos.z, minZ, maxZ);
        transform.position = pos;
        _cc.enabled        = true;
    }
}
