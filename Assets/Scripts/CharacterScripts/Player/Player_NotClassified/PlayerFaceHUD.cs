using UnityEngine;
using UnityEngine.UI;

public class PlayerFaceHUD : MonoBehaviour
{
    [Header("References")]
    public PlayerCombat playerCombat;
    public Image faceImage;

    [Header("Face Sprites (asignar cuando estén listos)")]
    public Sprite face100; // perfecto
    public Sprite face75;  // herido leve
    public Sprite face50;  // herido
    public Sprite face25;  // muy herido
    public Sprite face0;   // casi KO

    [Header("Placeholder Colors (mientras no hay sprites)")]
    public Color color100 = Color.green;
    public Color color75  = new Color(0.6f, 1f, 0f);   // verde amarillento
    public Color color50  = Color.yellow;
    public Color color25  = new Color(1f, 0.5f, 0f);   // naranja
    public Color color0   = Color.red;

    private float _lastHP = -1f;

    void Update()
    {
        if (playerCombat == null || faceImage == null) return;
        if (playerCombat.HP == _lastHP) return; // solo actualiza si cambia

        _lastHP = playerCombat.HP;
        UpdateFace(playerCombat.HP, playerCombat.HP); // HP actual = maxHP por ahora
    }

    void UpdateFace(float currentHP, float maxHP)
    {
        float pct = currentHP / maxHP;

        Sprite targetSprite = pct switch
        {
            >= 1f    => face100,
            >= 0.75f => face75,
            >= 0.50f => face50,
            >= 0.25f => face25,
            _        => face0
        };

        Color targetColor = pct switch
        {
            >= 1f    => color100,
            >= 0.75f => color75,
            >= 0.50f => color50,
            >= 0.25f => color25,
            _        => color0
        };

        // Usa sprite si está asignado, si no usa color placeholder
        if (targetSprite != null)
            faceImage.sprite = targetSprite;
        else
            faceImage.color = targetColor;
    }
}
