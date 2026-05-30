using UnityEngine;

/// <summary>
/// Clampea la posición del jugador en Z (zona de movimiento libre).
/// Usado por HUDManager para restringir el movimiento fuera del combate.
/// </summary>
public class PlayerBoundaryClamp : MonoBehaviour
{
    [Header("Límites en Z")]
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
