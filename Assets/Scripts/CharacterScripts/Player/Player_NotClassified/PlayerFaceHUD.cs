using UnityEngine;
using UnityEngine.UI;

public class PlayerFaceHUD : MonoBehaviour
{
    [Header("References")]
    public PlayerCombat playerCombat;
    public Image faceImage;

    [Header("Face Sprites (assign when ready)")]
    public Sprite face100; // perfect
    public Sprite face75;  // lightly wounded
    public Sprite face50;  // wounded
    public Sprite face25;  // badly wounded
    public Sprite face0;   // near KO

    [Header("Placeholder Colors (while sprites are not available)")]
    public Color color100 = Color.green;
    public Color color75  = new Color(0.6f, 1f, 0f);   // yellowish green
    public Color color50  = Color.yellow;
    public Color color25  = new Color(1f, 0.5f, 0f);   // orange
    public Color color0   = Color.red;

    private float _lastHP = -1f;

    void Update()
    {
        if (playerCombat == null || faceImage == null) return;
        if (playerCombat.HP == _lastHP) return; // only update on change

        _lastHP = playerCombat.HP;
        UpdateFace(playerCombat.HP, playerCombat.HP); // current HP = maxHP for now
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

        // Use sprite if assigned, otherwise use placeholder color
        if (targetSprite != null)
            faceImage.sprite = targetSprite;
        else
            faceImage.color = targetColor;
    }
}
