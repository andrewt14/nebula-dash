using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Sound Effects")]
    public AudioClip jumpSound;
    public AudioClip collectSound;
    public AudioClip impactSound;
    public AudioClip deathSound;
    public AudioClip boulderSound;
    public AudioClip groundBreakSound;
    public AudioClip alienAppearSound;

    [Header("Music")]
    public AudioClip backgroundMusic;

    private AudioSource sfxSource;
    private AudioSource musicSource;
    // Lets in-scene UI (e.g. the gameplay pause menu's mute button) wire
    // straight to this scene's actual music source, so toggling reacts
    // live instead of only taking effect on the next scene load.
    public AudioSource MusicSource => musicSource;

void Awake()
{
    Instance = this;

    sfxSource = gameObject.AddComponent<AudioSource>();
    musicSource = gameObject.AddComponent<AudioSource>();

    sfxSource.playOnAwake = false;
    sfxSource.priority = 0;
    sfxSource.reverbZoneMix = 0f;
    // Muting (rather than gating every PlayOneShot call) respects the
    // "SoundEnabled" preference set from the settings/pause UI while
    // still letting every existing PlaySomething() call stay unchanged.
    sfxSource.mute = PlayerPrefs.GetInt("SoundEnabled", 1) == 0;

    musicSource.loop = true;
    musicSource.volume = 0.4f;
    musicSource.priority = 128;
}

void Start()
{
    // Preload all clips to eliminate delay
    if (jumpSound != null) sfxSource.clip = jumpSound;
    if (collectSound != null) sfxSource.PlayOneShot(collectSound, 0f);
    if (impactSound != null) sfxSource.PlayOneShot(impactSound, 0f);
    if (deathSound != null) sfxSource.PlayOneShot(deathSound, 0f);

    if (backgroundMusic != null)
    {
        musicSource.clip = backgroundMusic;
        // Respects the on/off toggle set on the start screen
        // (MusicToggle.cs), persisted via PlayerPrefs.
        if (PlayerPrefs.GetInt("MusicEnabled", 1) == 1)
            musicSource.Play();
    }
}

public void SetSfxMuted(bool muted)
{
    sfxSource.mute = muted;
}

public void PlayJump()
{
    if (jumpSound != null)
        sfxSource.PlayOneShot(jumpSound, 0.8f);
}

public void PlayCollect()
{
    if (collectSound != null)
        sfxSource.PlayOneShot(collectSound, 0.7f);
}

public void PlayImpact()
{
    if (impactSound != null)
        sfxSource.PlayOneShot(impactSound, 0.7f);
}

public void PlayDeath()
{
    if (deathSound != null)
        sfxSource.PlayOneShot(deathSound, 0f);
}

public void PlayBoulder()
{
    if (boulderSound != null)
        sfxSource.PlayOneShot(boulderSound, 0.6f);
}

public void PlayGroundBreak()
{
    if (groundBreakSound != null)
        sfxSource.PlayOneShot(groundBreakSound, 0.7f);
}

public void PlayAlienAppear()
{
    if (alienAppearSound != null)
        sfxSource.PlayOneShot(alienAppearSound, 0.5f);
}

// AudioSource playback isn't affected by Time.timeScale, so a boulder's
// slow-mo has to pitch the music down by hand to actually read as a
// "brief slo mo" for the audio too, not just the visuals.
public void SetMusicPitch(float pitch)
{
    musicSource.pitch = pitch;
}
}