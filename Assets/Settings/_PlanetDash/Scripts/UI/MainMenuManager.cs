using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using TMPro;

/// <summary>
/// MainMenuManager — Animated start menu for Nebula Dash.
/// Camera orbits around the player model on alien ground.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI subtitleText;
    public TextMeshProUGUI bestScoreText;
    public Button startButton;
    public Image fadeOverlay;

    [Header("Title Animation")]
    public float titleBobSpeed = 1.5f;
    public float titleBobAmount = 10f;

    [Header("Subtitle Pulse")]
    public float subtitlePulseSpeed = 2f;
    public float subtitleMinAlpha = 0.3f;

    [Header("Camera Orbit")]
    public Transform menuCamera;
    public Transform orbitTarget;       // empty GameObject at player model chest
    public float orbitSpeed = 8f;       // degrees/sec, used for idle auto-rotate
    public float orbitRadius = 4f;
    public float orbitHeight = 1.8f;
    public float cameraLookHeight = 1f;

    [Header("Drag-to-Rotate")]
    // Player can drag/swipe to spin the character; it coasts on release
    // and eases back into the idle auto-rotate after a few seconds of
    // no input, instead of just staying wherever the player left it.
    public float dragSensitivity = 0.25f;   // degrees per pixel of drag
    public float mouseDragSensitivity = 4f; // degrees per unit of Input.GetAxis("Mouse X")
    public float inertiaDamping = 2.5f;     // how fast flick momentum decays
    public float idleResumeDelay = 2.5f;    // seconds of no input before auto-rotate resumes
    private float angularVelocity = 0f;     // degrees/sec, driven by drag or auto-rotate
    private float lastInputTime = -999f;

    [Header("Transition")]
    public float fadeDuration = 0.8f;

    private Vector3 titleStartPos;
    private float orbitAngle = 200f;
    private bool isTransitioning = false;

    void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;

        if (titleText != null)
            titleStartPos = titleText.rectTransform.anchoredPosition;

        int best = PlayerPrefs.GetInt("HighScore", 0);
        if (bestScoreText != null)
            bestScoreText.text = best > 0 ? $"BEST  {best}" : "";

        if (startButton != null)
            startButton.onClick.AddListener(OnStartPressed);

        // Fade in from black
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            fadeOverlay.color = Color.black;
            StartCoroutine(FadeIn());
        }
    }

    void Update()
    {
        AnimateTitle();
        AnimateSubtitle();
        HandleDragInput();
        OrbitCamera();

        if (!isTransitioning && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
            OnStartPressed();
    }

    void AnimateTitle()
    {
        if (titleText == null) return;
        float y = Mathf.Sin(Time.time * titleBobSpeed) * titleBobAmount;
        titleText.rectTransform.anchoredPosition = titleStartPos + Vector3.up * y;
    }

    void AnimateSubtitle()
    {
        if (subtitleText == null) return;
        float a = Mathf.Lerp(subtitleMinAlpha, 1f, (Mathf.Sin(Time.time * subtitlePulseSpeed) + 1f) / 2f);
        Color c = subtitleText.color;
        c.a = a;
        subtitleText.color = c;
    }

    // Touch drag (mobile) or mouse drag (editor/desktop testing) spins the
    // platform directly; releasing leaves it coasting on its last
    // velocity, which decays back to the idle auto-rotate speed.
    void HandleDragInput()
    {
        bool dragging = false;
        float degreesDelta = 0f;

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Moved)
            {
                degreesDelta = -t.deltaPosition.x * dragSensitivity;
                dragging = true;
            }
        }
        else if (Input.GetMouseButton(0))
        {
            float mouseDeltaX = Input.GetAxis("Mouse X");
            if (Mathf.Abs(mouseDeltaX) > 0.0001f)
            {
                degreesDelta = -mouseDeltaX * mouseDragSensitivity;
                dragging = true;
            }
        }

        if (dragging)
        {
            orbitAngle += degreesDelta;
            angularVelocity = degreesDelta / Mathf.Max(Time.deltaTime, 0.0001f);
            lastInputTime = Time.time;
        }
    }

    void OrbitCamera()
    {
        if (menuCamera == null || orbitTarget == null) return;

        bool idle = Time.time - lastInputTime > idleResumeDelay;
        if (idle)
        {
            // Ease any leftover flick speed back toward the steady
            // auto-rotate speed instead of snapping to it.
            angularVelocity = Mathf.Lerp(angularVelocity, orbitSpeed, Time.deltaTime * 0.5f);
        }
        else
        {
            // Coast and decay while the finger/mouse isn't actively moving.
            bool activeInput = Input.touchCount > 0 || Input.GetMouseButton(0);
            if (!activeInput)
                angularVelocity = Mathf.Lerp(angularVelocity, 0f, Time.deltaTime * inertiaDamping);
        }

        orbitAngle += angularVelocity * Time.deltaTime;
        if (orbitAngle >= 360f) orbitAngle -= 360f;
        if (orbitAngle < 0f) orbitAngle += 360f;

        float rad = orbitAngle * Mathf.Deg2Rad;
        Vector3 center = orbitTarget.position;

        menuCamera.position = new Vector3(
            center.x + Mathf.Sin(rad) * orbitRadius,
            center.y + orbitHeight,
            center.z + Mathf.Cos(rad) * orbitRadius
        );

        menuCamera.LookAt(center + Vector3.up * cameraLookHeight);
    }

    void OnStartPressed()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        StartCoroutine(TransitionToGame());
    }

    IEnumerator TransitionToGame()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.gameObject.SetActive(true);
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                fadeOverlay.color = new Color(0, 0, 0, Mathf.Clamp01(t / fadeDuration));
                yield return null;
            }
        }
        SceneManager.LoadScene("GamePlay");
    }

    IEnumerator FadeIn()
    {
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            fadeOverlay.color = new Color(0, 0, 0, 1f - Mathf.Clamp01(t / fadeDuration));
            yield return null;
        }
        fadeOverlay.gameObject.SetActive(false);
    }
}