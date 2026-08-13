using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _PlanetDash_UseAudioSessionPlaybackCategory();
#endif

    // Runs once at app launch, before any scene loads — the default iOS
    // audio session category is muted by the hardware ringer/silent
    // switch, which for a music-driven game reads as "sound randomly
    // doesn't work". Playback category ignores the switch.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureAudioSession()
    {
#if UNITY_IOS && !UNITY_EDITOR
        _PlanetDash_UseAudioSessionPlaybackCategory();
#endif
    }

    // Nothing in the project ever set this, so it fell back to whatever
    // Application.targetFrameRate defaults to per-platform — on iOS that
    // leaves frame pacing at the mercy of vSyncCount (Medium quality tier
    // uses vSyncCount 1) with no explicit cap, which on a ProMotion display
    // (iPhone 17 Pro et al., up to 120Hz) means rendering as fast as the
    // display allows rather than a stable target — inconsistent pacing and
    // needless battery/thermal cost for a game with no gameplay reason to
    // run above 60. Locking it explicitly makes 60fps an actual target
    // instead of an incidental side effect of whatever display the run
    // happens to be on.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureTargetFrameRate()
    {
        Application.targetFrameRate = 60;
    }

    [Header("Sound Effects")]
    public AudioClip jumpSound;
    public AudioClip collectSound;
    public AudioClip impactSound;
    public AudioClip deathSound;
    public AudioClip boulderSound;
    public AudioClip groundBreakSound;
    public AudioClip alienAppearSound;
    public AudioClip turnCountdownSound;

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

// Same clip as PlayImpact (the comet's own landing sound), quieter — a
// boulder bounces far more often than a comet lands once, so the full
// 0.7 volume on every single bounce read as loud/fatiguing rather than
// subtle. A boulder's bounce cycle repeats every ~1.6-1.9s for its whole
// lifetime, so the exact same clip was retriggering identically over and
// over — reported back as a "whistle" (the perceptible artifact of
// hearing an unvarying clip loop back on a steady cadence). Per-call
// pitch randomization is the standard fix: no two consecutive bounces
// sound identical, so there's nothing left to lock into a repeating tone.
public void PlayBoulderBounceImpact()
{
    if (impactSound == null || sfxSource == null) return;
    float prevPitch = sfxSource.pitch;
    sfxSource.pitch = Random.Range(0.85f, 1.15f);
    sfxSource.PlayOneShot(impactSound, 0.22f);
    sfxSource.pitch = prevPitch;
}

public void PlayDeath()
{
    // Death sound restored — a prior "revert" left the volume at 0f,
    // which silenced it entirely (that was the "missing death sound").
    if (deathSound != null)
        sfxSource.PlayOneShot(deathSound, 0.8f);
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

public void PlayTurnCountdown()
{
    if (turnCountdownSound != null)
        sfxSource.PlayOneShot(turnCountdownSound, 0.5f);
}

// Went through a lightning-synced ambient rumble/drone (ThunderSound,
// removed) — reported back as an unwanted sustained "ambient noise" on
// its own. Replaced with a much smaller idea: a brief, subtle static
// crackle timed to a specific on-screen moment (currently
// AmbientEffects' shooting stars popping into view) rather than a
// continuous background cue. No delay, no sustained tail — just a quick
// pop synced to the visual.
public void PlayStaticPop()
{
    if (sfxSource == null) return;
    float prevPitch = sfxSource.pitch;
    sfxSource.pitch = Random.Range(0.9f, 1.1f);
    sfxSource.PlayOneShot(StaticPopSound.GetClip(), Random.Range(0.12f, 0.2f));
    sfxSource.pitch = prevPitch;
}

// AudioSource playback isn't affected by Time.timeScale, so a boulder's
// slow-mo has to pitch the music down by hand to actually read as a
// "brief slo mo" for the audio too, not just the visuals.
public void SetMusicPitch(float pitch)
{
    musicSource.pitch = pitch;
}

// Jetpack ignition cue — subtle, and deliberately distinct from
// PlayImpact (different texture/pitch character, see
// JetpackActivateSound) so the two are never confused despite both
// opening with a short transient.
public void PlayJetpackActivate()
{
    if (sfxSource == null) return;
    sfxSource.PlayOneShot(JetpackActivateSound.GetClip(), 0.35f);
}
}