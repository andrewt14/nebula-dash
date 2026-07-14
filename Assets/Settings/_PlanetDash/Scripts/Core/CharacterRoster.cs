using UnityEngine;

// Single source of truth for the character roster, shared by the start
// screen's CharacterSelector (preview/pick) and the gameplay scene's
// PlayerCharacterLoader (actually spawns the pick). Add more characters
// by adding entries here — both places pick it up automatically.
[CreateAssetMenu(fileName = "CharacterRoster", menuName = "NebulaDash/Character Roster")]
public class CharacterRoster : ScriptableObject
{
    [System.Serializable]
    public class CharacterOption
    {
        public string displayName = "Astronaut";
        public GameObject prefab;
        public bool locked = false;
        // Different source rigs put their root pivot in different places
        // (mid-body vs at the feet) even after matching overall height —
        // this corrects the spawn position so feet line up consistently
        // in the T-pose menu preview.
        public Vector3 spawnOffset;
        // Separate correction for actual gameplay: Humanoid retargeting
        // shifts a character differently once the Run animation is
        // actively playing (leg/torso proportion differences interact
        // with the clip's authored hip height), so the same static
        // spawnOffset that fixes the T-pose menu preview does not fix
        // in-game positioning — needs its own value, tuned against the
        // animated pose.
        public Vector3 gameplaySpawnOffset;
        // The prefab's baked-in root scale (see spawnOffset's comment) is
        // tuned against the ANIMATED running pose for gameplay — Humanoid
        // retargeting gives a completely different bounds height in the
        // T-pose used by the menu preview, so a character whose gameplay
        // height matches perfectly can come out a different height in
        // the T-pose preview. Extra multiplier applied only in the menu,
        // on top of CharacterSelector's previewScale.
        public float menuScaleMultiplier = 1f;
        // Per-character override for the menu hologram blend — how much
        // of HologramCharacter's cyan tint to mix over this character's
        // own texture. Lower values let more of the original texture
        // show through (matches the shader's own defaults unless
        // overridden, so existing characters look unchanged).
        [Range(0f, 1f)] public float hologramTintStrength = 0.35f;
        [Range(0f, 1f)] public float hologramAlpha = 0.55f;
        [Range(0f, 4f)] public float hologramRimStrength = 1.5f;
        // Dims the character's own texture before tinting — the only
        // knob that actually reduces apparent brightness for a character
        // with a naturally light/white base texture, since tint/alpha
        // only shift hue or transparency.
        [Range(0f, 2f)] public float hologramBrightness = 1f;
    }

    public CharacterOption[] characters;
}
