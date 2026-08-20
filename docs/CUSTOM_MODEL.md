# The Deathwing model and its animations

The survivor runs on a **Commando chassis**: networking, state machines, camera rig, hurtboxes,
footsteps and the aim origin all come from that clone, and only the *drawn* model is replaced.

There are two ways to get a real model in, and this repo ships the first:

1. **[Runtime payload](#1-the-runtime-payload-dwm1)** — a converted mesh/skeleton/animation file the mod
   turns into meshes, materials and clips while the game runs. **No Unity editor, no login, no bundle.**
2. **[Asset bundle](#3-alternative-an-asset-bundle)** — the conventional route, if you would rather
   author the model in Unity.

> **Redistribution.** Blizzard's models, textures and ability icons are not licensed for
> redistribution. Convert them for your own use and keep the art out of the repo and off Thunderstore.
> `.gitignore` excludes `art/`, `*.dwm` and `*.bundle` for exactly this reason.

---

## 1. The runtime payload (DWM1)

### 1.1 What it is

`tools/convert.py` reads a Warcraft 3 / Heroes of the Storm `.mdx` model plus its `.dds` textures and
writes a single `deathwing.dwm` file containing:

- the bone hierarchy and its rest pose (which doubles as the bind pose),
- one entry per geoset: positions, normals, UVs, four bone influences per vertex, triangles,
- the DXT texture blocks copied verbatim, mip chain included,
- the animation sequences listed in `WANTED`, keyed in seconds and thinned to the keys linear
  interpolation cannot reproduce.

`Modules/DeathwingModel.cs` decodes that at runtime and builds a `GameObject` per bone, a
`SkinnedMeshRenderer` per geoset, one material off the game's own character shader, and a **legacy**
`AnimationClip` per sequence. Legacy is not a shortcut: a modern clip needs an `AnimatorController`,
and a controller cannot be created outside the editor.

Coordinate handling lives in `convert.py`: MDX is right-handed Z-up X-forward, Unity is left-handed
Y-up Z-forward, so every vector is remapped `(x, y, z) -> (-y, z, x)`. That swap is a reflection,
hence quaternions flip `w` and triangle winding is reversed (verified against the model's own normals
rather than assumed).

### 1.2 Converting a model

```bash
python3 tools/convert.py model.mdx diffuse.dds emissive.dds art/deathwing.dwm
python3 tools/render.py art/deathwing.dwm check.png "Stand" 0.5   # offline sanity render
```

`render.py` skins and rasterises the payload with no game involved, which is the cheapest way to catch
a broken bind pose or an inverted axis before a 10-minute round trip through Risk of Rain 2.

### 1.3 Shipping it

The build embeds `art/deathwing.dwm` when the file exists, so a release is one DLL to install. The mod
also reads `deathwing.dwm` **next to the plugin** first, which is how you iterate on the art without
rebuilding:

```
Risk of Rain 2/BepInEx/plugins/Deathwing/
├─ Deathwing.dll
├─ deathwing.dwm      (optional; overrides the embedded copy)
└─ Icons/*.png        (optional; overrides the embedded icons)
```

With neither present the survivor keeps the placeholder chassis mesh and the chassis' skill icons, so
a build from a clean clone still runs.

### 1.4 Config dials

| Key (`BepInEx/config/com.jayty07.deathwing.cfg`) | Default | Effect |
| --- | --- | --- |
| `[Model] Use Real Model` | `true` | Set `false` to force the placeholder mesh — the fastest way to tell a model problem from a gameplay one |
| `[Model] Real Model Scale` | `0.006` | Source units → metres. He stands ~325 units tall, so this is ~3.7 m once `Model Scale` is applied |
| `[Model] Real Model Emission` | `2.2` | How hot the glowing cracks and eyes burn |
| `[Stats] Model Scale` | `1.9` | Scales the model **base**, and with it the capsule, hitboxes and camera distance |

### 1.5 What the mod expects from the rig

| Name | Where | Used by |
| --- | --- | --- |
| `Bone_Jaw_Main_00_M` | jaw bone | A `DeathwingMouth` locator is parented to it and registered with the body's `ChildLocator`; Molten Breath sprays from there |

`MoltenBreath` tries `DeathwingMouth`, then `MuzzleCenter`, `MuzzleGun`, `MuzzleLeft`, `HeadCenter`,
`Head`, and finally a point above his core. A different rig only needs `DeathwingClips.jawBoneName`
updated.

---

## 2. Animation

### 2.1 Clip mapping

`Modules/DeathwingAnimator.cs` holds the mapping, and every skill state asks for its clip through
`BaseDeathwingSkillState.PlayDragonAnimation(...)`, which **falls back to the chassis' animator** when
the real model is not loaded. That is why the same build works with and without the art.

| Skill / situation | Source clip | Constant |
| --- | --- | --- |
| Idle | `Stand` | `DeathwingClips.idle` |
| Walking | `Walk A` | `DeathwingClips.walk` |
| Molten Claw, hit 1 | `Attack A` | `clawA` |
| Molten Claw, hit 2 | `Attack B` | `clawB` |
| Molten Claw, finisher | `Attack C` | `clawC` |
| Molten Breath, windup | `Spell A Start` | `breathStart` |
| Molten Breath, channel (held) | `Spell A` | `breathLoop` |
| Molten Breath, release | `Spell A End` | `breathEnd` |
| Molten Boulder | `Spell D` | `boulder` |
| Elementium Charge | `Spell C` | `charge` |
| Flight, takeoff | `Spell H Start` | `flightStart` |
| Flight, airborne (held) | `Spell H` | `flightLoop` |
| Flight, landing | `Spell H End` | `flightLand` |
| Dive Slam | `Spell Z End` | `dive` |
| Cataclysm | `Spell J` | `cataclysm` |
| Death | `Death` | `death` |

The HotS sequences are named by slot (`Spell A`…`Spell Z`), not by what they depict, so this mapping is
a reading of the source animations and is meant to be retuned in game: change the constant, rebuild.

### 2.2 How playback works

- `PlayOnce(clip, duration)` scales the clip's speed to `clipLength / duration`, so a skill sped up by
  attack speed keeps its animation in step. Damage is never driven by the clip — the states fire on
  their own stopwatches — so a mistimed clip looks wrong but never breaks a skill.
- `PlayHeld(clip)` loops a clip for as long as a channel lasts; `Release(closingClip)` ends it.
- Locomotion (idle / walk / airborne) is chosen every frame from `CharacterBody`'s motor, and walk
  playback is scaled by ground speed.
- A clip named in `DeathwingClips` but absent from the payload is skipped silently, which is why a
  reduced `WANTED` list in `convert.py` costs animations rather than stability.

### 2.3 Adding a sequence

Add its name to `WANTED` in `tools/convert.py`, re-run the conversion, then reference it from a state
through `PlayDragonAnimation`. The source model carries 37 sequences; the shipped payload carries the
22 the kit uses plus spares.

---

## 3. Alternative: an asset bundle

Only worth it if you want to author or heavily edit the model in Unity. You need Unity Hub with the
exact Unity version the game shipped with (read it out of `Risk of Rain 2_Data/globalgamemanagers`),
and [ThunderKit](https://github.com/PassivePicasso/ThunderKit) to import the game's shaders.

Build a prefab shaped like this, with a bundle name of `deathwing`:

```
mdlDeathwing
├─ Animator                (controller duplicated from Commando's, clips swapped)
├─ CharacterModel          (Base Renderer Infos filled in, Ignore Overlays off)
├─ ChildLocator            (MuzzleCenter → an empty at the mouth, +Z out of the jaws)
├─ DeathwingArmature
└─ DeathwingMesh           (SkinnedMeshRenderer, Hopoo Games/Deferred/Standard material)
```

Then load it in `DeathwingBody.AttachRealModel` in place of `DeathwingModel.Build`, and drive it with
`PlayCrossfade` instead of `PlayDragonAnimation` — the fallback path in
`BaseDeathwingSkillState.PlayDragonAnimation` shows the layer/state/playback-rate names each skill
uses. Duplicating Commando's controller and swapping only the clips is the low-risk version: **copy the
parameter list verbatim**, because the game writes locomotion and aim parameters by name and a renamed
one silently stops being driven rather than erroring.

---

## 4. Icons

`Modules/DeathwingIcons.cs` loads PNGs from `art/icons/` (embedded at build time, or dropped into an
`Icons` folder beside the plugin) and falls back to the chassis' icon per slot. Current assignment:

| Slot | Icon |
| --- | --- |
| Passive — Molten Blood | `AspectofDeath` |
| Primary — Molten Claw | `Onslaught` |
| Secondary — Molten Boulder | `LavaBurst` |
| Secondary — Molten Breath | `MoltenFlame` |
| Utility — Elementium Charge | `Destroyer` |
| Utility — Wings of the Destroyer | `Dragonflight` |
| Special — Cataclysm | `Cataclysm` |
| Body portrait | `WorldBreaker` |

`EarthShatter`, `Incinerate` and `FormSwitch` are unused, and are there for the Heroes-of-the-Storm
ability pass.

---

## 5. When something does not show up

The mod fails soft and logs, so `BepInEx/LogOutput.log` names the problem:

| Symptom | Look for |
| --- | --- |
| Placeholder mesh instead of the dragon | `No 'deathwing.dwm' found …` or `Could not decode …` — the payload is missing, or was written by a different converter revision |
| Dragon the wrong size | `[Model] Real Model Scale`; the capsule follows `[Stats] Model Scale`, not this |
| Textures scrambled or mirrored | The UV V flip in `convert.py`, which cancels against the DDS row order — flip it there, not in the shader |
| Invisible in character select only | The display prefab strips components; see the keep-list in `DeathwingBody.CreateDisplayPrefab` |
| Breath starts in mid-air | The jaw bone name in `DeathwingClips.jawBoneName`, or the mouth offset in `DeathwingBody.AddMouthLocator` |
| No animation at all | `Deathwing model loaded: … animations` in the log, then the clip names in `DeathwingClips` against the sequence names `convert.py` printed |
| Blank skill icons | `Icon '<name>' is not installed` — the icon PNGs were not embedded and none sit beside the plugin |
