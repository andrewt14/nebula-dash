using UnityEngine;

// Reverted to the original Skybox4.mat (Skybox/6 Sided, hand-authored
// nebula art) — the Skybox4Cubemap.mat swap-in had a genuinely bad bake
// (a mismatched Right/Back face edge, visible as a hard seam whenever the
// camera's turn-rotation tween swept across it) and was reported as
// looking worse overall than the original. See Skybox4.mat's own
// _Rotation (28.85) — that value was hand-tuned by whoever originally set
// this material up, almost certainly to park ITS seam out of the way the
// same way this script used to force _Rotation to 0 for the cubemap.
//
// This script used to hard-reset _Rotation to 0 every scene load. That
// was a fix for something continuously ANIMATING _Rotation at runtime
// (sweeping the seam through frame on a cycle) — no code in this project
// does that anymore (checked: nothing but this script ever touches
// _Rotation). Forcing it to 0 wasn't harmless on Skybox4 though — it was
// clobbering the material's own tuned 28.85 back to an untuned value
// every run. Left as a no-op now so the authored rotation survives
// ZoneManager's runtime clone (new Material(RenderSettings.skybox) in
// ZoneManager.Start() copies whatever _Rotation is live on the source at
// that point).
public class SkyboxRotator : MonoBehaviour
{
}
