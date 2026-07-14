using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Pause button + panel for the gameplay scene. Freezes gameplay via
// Time.timeScale (everything else in this project already scales
// movement/timers by Time.deltaTime, so this is the same mechanism the
// rest of the game already relies on) and additionally disables
// PlayerController directly, since a couple of its actions (jump/slide)
// assign state immediately rather than scaling by deltaTime — those would
// otherwise leak through a timeScale-only pause and fire the instant the
// game resumes.
public class PauseMenu : MonoBehaviour
{
    public GameObject pausePanel;
    public Button pauseButton;
    public Button resumeButton;
    public Button restartButton;
    public Button quitButton;

    private PlayerController pc;
    private bool isPaused = false;

    void Start()
    {
        pc = FindObjectOfType<PlayerController>();

        if (pauseButton != null) pauseButton.onClick.AddListener(Pause);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(RestartGame);
        if (quitButton != null) quitButton.onClick.AddListener(QuitToMenu);

        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void Update()
    {
        // Hide the pause affordance once the run has ended — the death
        // screen already owns restart/menu from there, and pausing a
        // dead run makes no sense.
        if (pauseButton != null && GameManager.Instance != null)
            pauseButton.gameObject.SetActive(!GameManager.Instance.isGameOver && !isPaused);
    }

    public void Pause()
    {
        if (isPaused) return;
        if (GameManager.Instance != null && GameManager.Instance.isGameOver) return;

        isPaused = true;
        Time.timeScale = 0f;
        if (pc != null) pc.enabled = false;
        if (pausePanel != null) pausePanel.SetActive(true);
        if (pauseButton != null) pauseButton.gameObject.SetActive(false);
    }

    public void Resume()
    {
        if (!isPaused) return;

        isPaused = false;
        Time.timeScale = 1f;
        if (pc != null) pc.enabled = true;
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    void RestartGame()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.RestartGame();
        else SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void QuitToMenu()
    {
        Time.timeScale = 1f;
        if (GameManager.Instance != null) GameManager.Instance.GoToMainMenu();
        else SceneManager.LoadScene("MainMenu");
    }
}
