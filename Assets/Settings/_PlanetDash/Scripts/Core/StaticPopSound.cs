using UnityEngine;

// Brief static/radio-crackle pop synced to an on-screen ambient particle
// moment (currently AmbientEffects' shooting stars) — replaces the earlier
// sustained ambient drone (ThunderSound), reported back as unwanted
// "ambient noise" on its own. This is deliberately tiny: a fast-decaying
// burst of raw noise, no tonal content, no sustain — a quick crackle, not
// a background cue that lingers.
public static class StaticPopSound
{
    private static AudioClip cached;

    public static AudioClip GetClip()
    {
        if (cached == null) cached = Generate();
        return cached;
    }

    static AudioClip Generate()
    {
        const int sampleRate = 44100;
        const float duration = 0.22f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        System.Random rng = new System.Random();
        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

            // Fast exponential decay — a quick crackle/pop, not a
            // lingering hiss.
            float envelope = Mathf.Exp(-t * 18f);
            // Tiny instant attack ramp so sample 0 doesn't click.
            float attack = Mathf.Clamp01(t / 0.004f);

            data[i] = noise * envelope * attack * 0.5f;
        }

        AudioClip clip = AudioClip.Create("ProceduralStaticPop", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
