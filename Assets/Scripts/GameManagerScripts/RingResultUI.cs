using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Muestra el panel de Game Over o Game Win al final del combate del ring.
///
/// SETUP EN EDITOR:
///   1. Crear un Canvas (Screen Space – Overlay) en la escena.
///   2. Dentro del Canvas crear dos GameObjects: "GameOverPanel" y "GameWinPanel".
///      Cada uno con: fondo (Image), título (TextMeshPro) y botón de reinicio.
///   3. Asignar los dos paneles en este componente.
///   4. En TutorialRingManager → onPlayerOut  → llamar ShowGameOver().
///      En TutorialRingManager → onEnemyOut   → llamar ShowGameWin().
///   5. En onReset conectar: TutorialRingManager.ResetRing(),
///      reposición del player, reposición del enemigo, etc.
///   6. El botón "Reiniciar" de cada panel → OnClick: RingResultUI.Restart()
/// </summary>
public class RingResultUI : MonoBehaviour
{
    [Header("Panels")]
    public GameObject gameOverPanel;
    public GameObject gameWinPanel;

    [Header("Settings")]
    [Tooltip("Pauses the game (timeScale = 0) when showing the result.")]
    public bool pauseOnResult = true;

    [Header("Reset Event")]
    [Tooltip("Connect here everything that needs to reset on restart: " +
             "TutorialRingManager.ResetRing, player/enemy repositioning, etc.")]
    public UnityEvent onReset;

    // --------------------------------------------------
    // UNITY
    // --------------------------------------------------

    private void Awake()
    {
        gameOverPanel?.SetActive(false);
        gameWinPanel?.SetActive(false);
    }

    // --------------------------------------------------
    // PUBLIC API  (connect to TutorialRingManager events)
    // --------------------------------------------------

    public void ShowGameOver()
    {
        gameWinPanel?.SetActive(false);
        gameOverPanel?.SetActive(true);
        if (pauseOnResult) Time.timeScale = 0f;
    }

    public void ShowGameWin()
    {
        gameOverPanel?.SetActive(false);
        gameWinPanel?.SetActive(true);
        if (pauseOnResult) Time.timeScale = 0f;
    }

    // --------------------------------------------------
    // BOTÓN REINICIAR
    // --------------------------------------------------

    public void Restart()
    {
        Time.timeScale = 1f;
        gameOverPanel?.SetActive(false);
        gameWinPanel?.SetActive(false);
        onReset.Invoke();
    }
}
