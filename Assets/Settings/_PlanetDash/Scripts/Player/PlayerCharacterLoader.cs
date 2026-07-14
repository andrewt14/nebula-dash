using UnityEngine;

// Swaps in whichever character was picked on the start screen
// (PlayerPrefs "SelectedCharacter", written by CharacterSelector) before
// the player starts moving. Runs in Awake so it's done before
// PlayerAnimator.Start() reads its animator reference.
//
// Convention: roster entry 0 is assumed to be the character already
// built into this scene (the default model/glow under this object), so
// index 0 is a no-op — every other entry hides the default and spawns
// its own prefab in its place.
public class PlayerCharacterLoader : MonoBehaviour
{
    public CharacterRoster roster;
    public PlayerAnimator playerAnimator;
    // The scene's built-in default model + any effects riding on it
    // (e.g. the suit rim glow shell) — hidden if a different character
    // is selected.
    public GameObject[] defaultVisualParts;

    void Awake()
    {
        if (roster == null || roster.characters == null || roster.characters.Length == 0)
            return;

        int index = PlayerPrefs.GetInt("SelectedCharacter", 0);
        if (index <= 0 || index >= roster.characters.Length)
            return; // default character — already in the scene, nothing to do

        CharacterRoster.CharacterOption opt = roster.characters[index];
        if (opt.prefab == null) return;

        foreach (GameObject part in defaultVisualParts)
            if (part != null) part.SetActive(false);

        GameObject instance = Instantiate(opt.prefab, transform);
        instance.transform.localPosition = opt.gameplaySpawnOffset;
        instance.transform.localRotation = Quaternion.identity;

        Animator anim = instance.GetComponentInChildren<Animator>();
        if (playerAnimator != null && anim != null)
            playerAnimator.animator = anim;
    }
}
