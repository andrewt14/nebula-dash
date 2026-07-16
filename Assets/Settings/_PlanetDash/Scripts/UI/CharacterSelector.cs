using UnityEngine;
using TMPro;

// Start-screen preview/picker. Reads the shared CharacterRoster asset
// (also used by PlayerCharacterLoader in the game scene) so the same
// list drives both — add a character to the roster asset once, it shows
// up here and in-game with no code changes.
public class CharacterSelector : MonoBehaviour
{
    public CharacterRoster roster;
    public Transform spawnPoint;
    public MainMenuManager menuManager;
    public TextMeshProUGUI nameText;
    // Floating-in-the-galaxy look: no ground, character scaled down and
    // left in its imported bind pose (T-pose) rather than animated.
    public float previewScale = 0.6f;
    // Menu-only holographic look (NebulaDash/HologramCharacter shader) —
    // swapped onto every renderer's material slots, keeping each
    // renderer's own base texture via a MaterialPropertyBlock so one
    // shared material serves every character.
    public Material hologramMaterial;
    // Plays through this GameObject's own AudioSource (a sibling of
    // MainMenuManager/AchievementsUI on the MenuManager object) — the
    // start screen had no SFX at all on any of its interactions.
    public AudioClip switchSound;
    private AudioSource sfxSource;

    private int index = 0;
    private GameObject current;

    void Start()
    {
        sfxSource = GetComponent<AudioSource>();

        // Camera frames a fixed point, not the character — each character
        // has a different spawnOffset/root height to line its own feet up
        // with the pedestal, so orbiting around "current.transform" (as
        // this used to do per-Show) reframed the shot differently per
        // character even though their feet/height end up matching.
        if (menuManager != null && spawnPoint != null)
            menuManager.orbitTarget = spawnPoint;

        index = PlayerPrefs.GetInt("SelectedCharacter", 0);
        if (roster == null || roster.characters == null || roster.characters.Length == 0) return;
        if (index < 0 || index >= roster.characters.Length) index = 0;
        Show(index);
    }

    public void Next() { ChangeIndex(1); }
    public void Previous() { ChangeIndex(-1); }

    void ChangeIndex(int dir)
    {
        if (roster == null || roster.characters == null || roster.characters.Length == 0) return;
        int count = roster.characters.Length;
        index = (index + dir + count) % count;
        Show(index);
        PlayerPrefs.SetInt("SelectedCharacter", index);
        PlayerPrefs.Save();

        if (sfxSource != null && switchSound != null)
            sfxSource.PlayOneShot(switchSound, 0.6f);
    }

    void Show(int i)
    {
        if (current != null) Destroy(current);

        CharacterRoster.CharacterOption opt = roster.characters[i];
        if (opt.prefab != null && spawnPoint != null)
        {
            current = Instantiate(
                opt.prefab, spawnPoint.position, spawnPoint.rotation);
            // menuScaleMultiplier corrects for characters whose baked
            // prefab scale was tuned against the animated gameplay pose,
            // which doesn't carry over to the T-pose used here.
            float totalScale = previewScale * opt.menuScaleMultiplier;
            current.transform.localScale *= totalScale;
            // spawnOffset is defined in the character's own (unscaled)
            // local space, same as PlayerCharacterLoader's usage — scale
            // it by the same total scale so it stays proportionally
            // correct instead of applying as a fixed world-space shift.
            current.transform.position += opt.spawnOffset * totalScale;

            // T-pose float: disabling the Animator leaves the rig in its
            // imported bind pose instead of driving any clip.
            Animator anim = current.GetComponentInChildren<Animator>();
            if (anim != null) anim.enabled = false;

            ApplyHologramLook(current, opt);
        }

        if (nameText != null)
            nameText.text = opt.locked
                ? opt.displayName + " (LOCKED)"
                : opt.displayName;
    }

    void ApplyHologramLook(GameObject instance, CharacterRoster.CharacterOption opt)
    {
        if (hologramMaterial == null) return;

        MaterialPropertyBlock props = new MaterialPropertyBlock();
        foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
        {
            // Not every submaterial has a base texture (e.g. the rim-glow
            // pass shader has no _MainTex/_BaseMap at all) — Material.mainTexture
            // throws on those instead of returning null, so check first.
            Material srcMat = r.sharedMaterial;
            Texture baseTex = srcMat != null &&
                (srcMat.HasProperty("_BaseMap") || srcMat.HasProperty("_MainTex"))
                ? srcMat.mainTexture : null;

            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = hologramMaterial;
            r.sharedMaterials = mats;

            r.GetPropertyBlock(props);
            if (baseTex != null) props.SetTexture("_BaseMap", baseTex);
            props.SetFloat("_TintStrength", opt.hologramTintStrength);
            props.SetFloat("_Alpha", opt.hologramAlpha);
            props.SetFloat("_RimStrength", opt.hologramRimStrength);
            props.SetFloat("_Brightness", opt.hologramBrightness);
            r.SetPropertyBlock(props);
        }
    }
}
