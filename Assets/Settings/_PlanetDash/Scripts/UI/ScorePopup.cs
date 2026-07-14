using UnityEngine;
using TMPro;
using System.Collections;

public class ScorePopup : MonoBehaviour
{
    public static ScorePopup Instance;
    public GameObject popupPrefab;
    public Canvas canvas;

    void Awake()
    {
        Instance = this;
    }

    public void ShowPopup(string text, Vector3 worldPos)
    {
        ShowPopup(text, worldPos, 0.8f, Color.white);
    }

    // duration/tint let callers like the level-up banner hold longer and
    // read as a distinct "big" event instead of the default quick pickup
    // blip, without needing a second parallel popup system.
    public void ShowPopup(string text, Vector3 worldPos, float duration, Color tint)
    {
        if (popupPrefab == null || canvas == null) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        StartCoroutine(PopupCoroutine(text, screenPos, duration, tint));
    }

    // Fixed screen-space banner near the top, for events that aren't
    // anchored to any world position — achievement unlocks need to read
    // as distinct from pickup popups (which float from the player), not
    // compete with them at the same on-screen spot.
    public void ShowTopBanner(string text, float duration, Color tint)
    {
        if (popupPrefab == null || canvas == null) return;
        Vector2 screenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.88f);
        StartCoroutine(PopupCoroutine(text, screenPos, duration, tint));
    }

    IEnumerator PopupCoroutine(string text, Vector2 screenPos, float duration, Color tint)
    {
        GameObject popup = Instantiate(
            popupPrefab, canvas.transform);
        TextMeshProUGUI tmp =
            popup.GetComponent<TextMeshProUGUI>();
        if (tmp == null) yield break;
        tmp.text = text;
        // Colored outline reads as a soft glow without needing a
        // dedicated glow shader/material.
        tmp.outlineWidth = tint == Color.white ? 0f : 0.25f;
        tmp.outlineColor = tint;

        popup.GetComponent<RectTransform>().position =
            screenPos;

        float timer = 0f;
        Vector3 startPos = popup.transform.position;
        // Fade-out only covers the last 0.8s (matching the original
        // popup's pace) — everything before that is a full-opacity hold,
        // so a longer duration reads as "on screen longer", not "floats
        // up more slowly".
        float fadeStart = Mathf.Max(0f, duration - 0.8f);

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            popup.transform.position = startPos +
                Vector3.up * 30f * t;

            float fadeT = fadeStart >= duration ? t :
                Mathf.Clamp01((timer - fadeStart) / (duration - fadeStart));
            tmp.color = new Color(
                tint.r, tint.g, tint.b,
                Mathf.Lerp(1f, 0f, fadeT));

            float scale = t < 0.3f ?
                Mathf.Lerp(0f, 1.3f, t / 0.3f) :
                Mathf.Lerp(1.3f, 0.8f,
                    (t - 0.3f) / 0.7f);
            popup.transform.localScale =
                Vector3.one * scale;

            yield return null;
        }

        Destroy(popup);
    }
}