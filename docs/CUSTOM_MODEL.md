# Replacing the placeholder model and animating the skills

The survivor currently runs on a **rescaled, retinted Commando clone**. Everything a playable survivor
needs (networking, state machines, camera rig, ragdoll, footsteps, aim origin) comes from that chassis,
and the model is the only borrowed part that is meant to be thrown away.

There are two independent jobs:

1. **[Model](#1-the-model)** — get a mesh into the game in place of Commando's.
2. **[Animation](#2-animation)** — make that mesh move when a skill fires.

You can do (1) and stop there: the skills already work, they just play Commando's animations on a
dragon-shaped mesh. Do (2) when you want real wing beats and claw swipes.

> **Redistribution.** Blizzard's model and textures are not licensed for redistribution. Extract them
> for your own use and keep the asset bundle out of the repo and off Thunderstore; ship the DLL only,
> and have users supply their own bundle. `.gitignore` already excludes `*.bundle` for this reason.

---

## 0. What you need

| Tool | Why |
| --- | --- |
| Unity Hub + the Unity version Risk of Rain 2 was built with | Asset bundles must be built by the same major Unity version the game runs, or the game refuses to load them. Find the version by opening `Risk of Rain 2/Risk of Rain 2_Data/globalgamemanagers` in a text editor and reading the version string near the top (it is also what ThunderKit reports when you point it at the install). |
| [ThunderKit](https://github.com/PassivePicasso/ThunderKit) (optional but strongly recommended) | Imports the game's own shaders, materials and prefabs into your Unity project, and builds + deploys bundles. Without it you cannot use the game's `Hopoo Games/Deferred/Standard` shader and your model will not light like the rest of the game. |
| [wow.export](https://github.com/Kruithne/wow.export) | Extracts Deathwing's model, skeleton and textures from a WoW install as `.obj`/`.gltf`/`.fbx` + PNGs. |
| Blender (optional) | Cleaning up the export, joining meshes, fixing the rig, authoring new clips. |

---

## 1. The model

### 1.1 Export from WoW

Deathwing exists as several creature models (`deathwing`, `deathwing_cataclysm`, the Madness-of-Deathwing
raid model). Pick one, export as **FBX** with skin/armature and textures. The result usually needs:

- **Scale**: WoW units are not metres. Deathwing should end up ~4–6 m at the shoulder; get it roughly
  right in Blender so the Unity import scale stays 1.
- **Orientation**: forward must be **+Z** and up **+Y** in Unity. Rotate in Blender, do not fix it with
  a rotation on the prefab root — the aim origin, muzzles and hitboxes all inherit that transform.
- **One armature**, mesh parented to it, no extra empties at the root.

### 1.2 Build the model prefab in Unity

Create the hierarchy the game expects. Names in **bold** are read by name at runtime, so they must match.

```
mdlDeathwing                     <- prefab root; this is what the mod instantiates
├─ Animator                      (component; controller from section 2)
├─ CharacterModel                (component)
├─ ChildLocator                  (component; see table below)
├─ ModelSkinController           (component; optional — only if you want multiple skins)
├─ DeathwingArmature             <- your armature root
│  └─ ... bones ...
│     └─ **MuzzleCenter**        <- empty at the mouth, +Z pointing out of the jaws
├─ DeathwingMesh                 <- SkinnedMeshRenderer
└─ **AimOrigin**                 <- empty at head height (eye level), used as the aim origin
```

`ChildLocator` entries to add (Name → Transform):

| Name | Where | Used by |
| --- | --- | --- |
| `MuzzleCenter` | mouth, +Z out of the jaws | Molten Breath's jet, and the fallback flame puffs |

That is the only one the mod looks up today (`MoltenBreath.muzzleNames` tries `MuzzleCenter`,
`MuzzleGun`, `MuzzleLeft`, `HeadCenter`, `Head` in that order and falls back to a point above his
core). Add more if you start attaching effects to the wings or claws.

On the `CharacterModel` component, fill **Base Renderer Infos** with one entry per renderer:
`Renderer` = your SkinnedMeshRenderer, `Default Material` = your material, `Default Shadow Casting Mode`
= `On`, `Ignore Overlays` = off (leave overlays on, or Deathwing will not visibly catch fire, freeze or
show elite auras).

Materials: use the game's `Hopoo Games/Deferred/Standard` shader (available once ThunderKit has imported
the game assets) with your albedo in `_MainTex`. A Unity Standard material also renders, but will not
match the game's lighting and will not receive overlays correctly.

### 1.3 Build the asset bundle

With ThunderKit: mark the prefab's bundle name as `deathwing` in the inspector, then use the ThunderKit
manifest/pipeline to build and deploy.

Without ThunderKit, an editor script is enough:

```csharp
// Assets/Editor/BuildBundles.cs
using System.IO;
using UnityEditor;

public static class BuildBundles
{
    [MenuItem("Deathwing/Build Bundle")]
    public static void Build()
    {
        string output = "AssetBundles";
        Directory.CreateDirectory(output);
        BuildPipeline.BuildAssetBundles(
            output,
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64);
    }
}
```

Copy the resulting `deathwing` file (no extension) next to the DLL:

```
Risk of Rain 2/BepInEx/plugins/Deathwing/
├─ Deathwing.dll
└─ deathwing
```

### 1.4 Load it in the mod

Add the bundle loader to `Modules/DeathwingAssets.cs`:

```csharp
private static AssetBundle bundle;

internal static AssetBundle Bundle()
{
    if (bundle)
    {
        return bundle;
    }

    string path = Path.Combine(
        Path.GetDirectoryName(DeathwingPlugin.instanceLocation),
        "deathwing");

    if (!File.Exists(path))
    {
        Log.Info("No model bundle found; using the placeholder chassis model.");
        return null;
    }

    bundle = AssetBundle.LoadFromFile(path);
    return bundle;
}
```

(`DeathwingPlugin` already knows its own location via `Info.Location`; expose it as
`internal static string instanceLocation` in `Awake`.)

Then replace the body of `DeathwingBody.ReplaceModel` — that method is the seam the whole mod was
written around, and nothing else needs to change:

```csharp
private static void ReplaceModel(Transform modelTransform)
{
    CharacterModel characterModel = modelTransform.GetComponent<CharacterModel>();
    if (!characterModel)
    {
        return;
    }

    GameObject custom = DeathwingAssets.Bundle()?.LoadAsset<GameObject>("mdlDeathwing");
    if (!custom)
    {
        // Placeholder path: tint the borrowed chassis mesh instead.
        if (!modelTransform.GetComponent<DeathwingTint>())
        {
            modelTransform.gameObject.AddComponent<DeathwingTint>();
        }

        return;
    }

    // The chassis' own mesh goes away, but its transform stays: it carries the CharacterModel,
    // hitboxes, aim origin and the animator the skill states talk to.
    foreach (Renderer renderer in modelTransform.GetComponentsInChildren<Renderer>(true))
    {
        Object.Destroy(renderer.gameObject);
    }

    GameObject instance = Object.Instantiate(custom, modelTransform, false);
    instance.transform.localPosition = Vector3.zero;
    instance.transform.localRotation = Quaternion.identity;

    // Skins would otherwise reassign Commando's materials over yours on spawn.
    Object.Destroy(modelTransform.GetComponent<ModelSkinController>());

    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
    CharacterModel.RendererInfo[] infos = new CharacterModel.RendererInfo[renderers.Length];
    for (int i = 0; i < renderers.Length; i++)
    {
        infos[i] = new CharacterModel.RendererInfo
        {
            renderer = renderers[i],
            defaultMaterial = renderers[i].sharedMaterial,
            defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
            ignoreOverlays = false
        };
    }

    characterModel.baseRendererInfos = infos;
    characterModel.mainSkinnedMeshRenderer = instance.GetComponentInChildren<SkinnedMeshRenderer>();

    // The animator on your prefab is the one the skill states drive.
    ModelLocator modelLocator = modelTransform.parent
        ? modelTransform.parent.GetComponentInParent<ModelLocator>()
        : null;

    if (modelLocator)
    {
        modelLocator.modelTransform = modelTransform;
    }
}
```

Two knock-on adjustments once a real model is in:

- **Scale**: set `Model Scale` in `BepInEx/config/com.jayty07.deathwing.cfg` to `1` if you exported
  Deathwing at his real size — the 1.9 default exists to make Commando dragon-sized. The capsule,
  hitboxes and camera distance all derive from it.
- **Tint**: set `Tint Model = false`. The tint exists to make Commando look molten and will fight your
  textures.

---

## 2. Animation

### 2.1 How the skills ask for animations

Every skill state calls `PlayCrossfade(layer, stateName, playbackRateParam, duration, crossfade)` on the
model's `Animator`. These are the exact requests the mod makes today:

| Skill | Layer | State | Playback-rate parameter | Source |
| --- | --- | --- | --- | --- |
| Molten Claw, hits 1–2 | `Gesture, Override` | `ThrowGrenade` | `ThrowGrenade.playbackRate` | `SkillStates/MoltenClaw.cs` |
| Molten Claw, finisher | `Gesture, Override` | `FireGun` | `FireGun.playbackRate` | `SkillStates/MoltenClaw.cs` |
| Molten Boulder | `Gesture, Override` | `ThrowGrenade` | `ThrowGrenade.playbackRate` | `SkillStates/HurlMoltenBoulder.cs` |
| Molten Breath | `Gesture, Override` | `ThrowGrenade` | `ThrowGrenade.playbackRate` | `SkillStates/MoltenBreath.cs` |
| Elementium Charge | `Body` | `Sprint` | — | `SkillStates/ElementiumCharge.cs` |
| Wings of the Destroyer | `Body` | `Jump` | — | `SkillStates/WingsOfTheDestroyer.cs` |
| Dive Slam | `Body` | `Fall` | — | `SkillStates/DiveSlam.cs` |
| Cataclysm | `Gesture, Override` | `ThrowGrenade` | `ThrowGrenade.playbackRate` | `SkillStates/Cataclysm.cs` |

They reuse Commando's state names because that is the controller the chassis ships with. The playback-rate
parameter is how the state stretches a clip to fit the skill's duration: the state sets it to
`clipLength / duration`, so a 1 s clip on a 2 s skill plays at 0.5.

### 2.2 The easy route: keep Commando's controller, swap the clips

Recommended for a first pass, because every animator parameter the game drives (locomotion blend, aim
pitch/yaw, grounded, death, and so on) is already wired and named correctly.

1. In Unity, duplicate Commando's `AnimatorController` (ThunderKit → the game's Commando assets).
2. Author your dragon clips in Blender against **your** armature and import them.
3. In the duplicated controller, replace the clip on each state you care about — put your claw swipe on
   `ThrowGrenade`, your overhead slam on `FireGun`, your wing beat on `Jump`, your dive on `Fall`, your
   walk/idle on the locomotion blend tree states.
4. Assign the controller to the `Animator` on `mdlDeathwing`.

Nothing in the mod changes. The state names stay Commando's, which reads oddly in the controller but
costs nothing.

### 2.3 The clean route: your own controller

If you would rather have states called `ClawSwipe` and `WingBeat`:

1. Build an `AnimatorController` with at minimum the layers **`Body`** (locomotion, jump, fall, death)
   and **`Gesture, Override`** (skill gestures, weight 1, override blending).
2. **Copy the parameter list from Commando's controller verbatim.** The game writes locomotion, aim and
   state parameters by name every frame; a parameter you rename or omit silently stops being driven, and
   an animator that is missing one does not error, it just never blends. Open Commando's controller
   side-by-side and reproduce the parameters exactly, then add one `float <StateName>.playbackRate` per
   gesture state of your own.
3. Add a `float` playback-rate parameter for each gesture state and multiply the state's speed by it
   (state inspector → Speed → Multiplier → your parameter).
4. Update the strings in the skill states to match your names, e.g. in `SkillStates/MoltenClaw.cs`:

   ```csharp
   PlayCrossfade("Gesture, Override", "ClawSwipe", "ClawSwipe.playbackRate", duration, 0.1f);
   ```

   The table in [2.1](#21-how-the-skills-ask-for-animations) lists every call site to change.

### 2.4 Timing the animations to the skills

Skill durations come from `attackSpeedStat`, so clips must be able to stretch. Two rules:

- Author each clip at the skill's **base** duration (`MoltenClaw.baseDuration`,
  `MoltenBreath.baseWindupDuration`, and so on) so the playback rate sits near 1 at 0.8 attack speed.
- Do not bake damage timing into the clip. The states fire damage on their own stopwatches, so a hit
  lands whether or not the animation has visually connected. If a swipe lands too early for your
  animation, adjust `MoltenClaw`'s `attackStartFraction`/`attackEndFraction`-style timings in the state
  rather than the clip.

### 2.5 Wing flapping during flight

`WingsOfTheDestroyer` currently plays `Jump` once on takeoff and nothing after. For a real dragon, give
the `Body` layer a looping `Fly` state and enter it after the takeoff clip:

```csharp
// OnEnter, after the takeoff sound
PlayCrossfade("Body", "Fly", 0.2f);
```

and let it run — the state no longer replays anything, so nothing will interrupt the loop until you land.

---

## 3. Checklist

- [ ] Model exported, scaled to metres, forward +Z / up +Y
- [ ] `mdlDeathwing` prefab with `Animator`, `CharacterModel`, `ChildLocator`, `MuzzleCenter`, `AimOrigin`
- [ ] `CharacterModel.baseRendererInfos` filled in, overlays left enabled
- [ ] Materials on `Hopoo Games/Deferred/Standard`
- [ ] Bundle built with the game's Unity version, deployed beside `Deathwing.dll`
- [ ] `ReplaceModel` loading the bundle; `Model Scale` and `Tint Model` adjusted in the config
- [ ] Clips authored against your armature and mapped to the states in [2.1](#21-how-the-skills-ask-for-animations)
- [ ] In-game: model visible in character select **and** in-run, breath leaving the mouth, hitboxes
      still landing (they are sized in `DeathwingBody.AddHitBoxes` and may need widening for wings)

## 4. When something does not show up

The mod fails soft and logs, so `BepInEx/LogOutput.log` names the problem:

| Symptom | Look for |
| --- | --- |
| Model invisible in-run | Nothing assigned materials — `baseRendererInfos` empty, or `ModelSkinController` still present and overwriting them |
| Model invisible in character select only | The display prefab strips components; see `DeathwingBody.CreateDisplayPrefab` and add yours to the keep-list |
| Breath starts in mid-air | `MuzzleCenter` missing or facing the wrong way |
| No animation at all | Layer or state name mismatch — Unity ignores a crossfade to a state that does not exist, silently |
| Animation plays at the wrong speed | Missing `<StateName>.playbackRate` parameter, or the state's speed multiplier is not bound to it |
