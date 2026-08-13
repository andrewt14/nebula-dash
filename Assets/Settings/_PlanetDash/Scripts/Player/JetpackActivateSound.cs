using UnityEngine;

// Procedurally synthesized jetpack ignition cue — no jetpack/power-up SFX
// asset exists in the project. Deliberately shares some character with the
// existing impact sound (a quick noise transient up front, so it still
// reads as a physical "thunk" of the pack kicking on) but layers a fast
// rising pitch sweep on top — that ascending sweep is the "power-up"
// signature the plain impact sound doesn't have, so the two are never
// mistaken for each other even though both open with a punchy transient.
public static class JetpackActivateSound
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
        const float duration = 0.4f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] data = new float[sampleCount];

        System.Random rng = new System.Random();
        double phase = 0.0;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float u = t / duration;

            // Punchy noise transient up front, fast decay — the
            // "impact" half of the cue.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float noiseEnvelope = Mathf.Exp(-t * 22f);

            // Fast rising pitch sweep (220Hz -> 900Hz), its own short
            // envelope so it reads as a distinct "power-up" layer riding
            // on top of the transient rather than a separate second sound.
            float freq = Mathf.Lerp(220f, 900f, u * u);
            phase += 2.0 * System.Math.PI * freq / sampleRate;
            float tone = (float)System.Math.Sin(phase);
            float toneEnvelope = Mathf.Clamp01(1f - u * 1.3f) *
                Mathf.Clamp01(t / 0.015f);

            data[i] = noise * noiseEnvelope * 0.45f + tone * toneEnvelope * 0.4f;
        }

        AudioClip clip = AudioClip.Create("ProceduralJetpackActivate", sampleCount, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
