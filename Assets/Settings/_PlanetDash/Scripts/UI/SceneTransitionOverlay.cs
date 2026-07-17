using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// Boot splash: a black screen with a fade-in/hold/fade-out "NEBULA DASH"
// title beat and a sound effect, played once on Start() before the scene's
// real content is revealed. Overlay starts opaque and title starts
// invisible (baked into the scene) so there's no flash of the menu
// underneath before this finishes.
public class SceneTransitionOverlay : MonoBehaviour
{
    public Image overlay;
    public TextMeshProUGUI titleText;
    public AudioClip transitionSound;
    public float fadeDuration = 0.2f;
    public float titleHold = 0.3f;

    private AudioSource sfxSource;

    void Awake()
    {
        sfxSource = GetComponent<AudioSource>();
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();
    }

    void Start()
    {
        StartCoroutine(PlayEntrance());
    }

    IEnumerator PlayEntrance()
    {
        if (sfxSource != null && transitionSound != null)
            sfxSource.PlayOneShot(transitionSound, 0.8f);

        yield return FadeTitle(0f, 1f);
        yield return new WaitForSeconds(titleHold);
        yield return FadeTitle(1f, 0f);
        yield return FadeOverlay(1f, 0f);
        if (overlay != null) overlay.gameObject.SetActive(false);
    }

    IEnumerator FadeOverlay(float from, float to)
    {
        if (overlay == null) yield break;
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            overlay.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / fadeDuration));
            yield return null;
        }
        overlay.color = new Color(0f, 0f, 0f, to);
    }

    IEnumerator FadeTitle(float from, float to)
    {
        if (titleText == null) yield break;
        float t = 0f;
        Color c = titleText.color;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, t / fadeDuration);
            titleText.color = c;
            yield return null;
        }
        c.a = to;
        titleText.color = c;
    }
}
