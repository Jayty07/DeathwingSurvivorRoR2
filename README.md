# Deathwing — a Risk of Rain 2 survivor

A custom playable survivor for Risk of Rain 2: Deathwing, the Destroyer. He is slow, heavily armored,
hits like a siege engine, and every one of his skills leaves fire or broken rock behind. He can also
take to the air and come back down as a bomb.

Built as a BepInEx plugin against R2API, so it drops into an existing modded install with no Unity
project required. A Unity/ThunderKit project is only needed if you want to replace the placeholder
art (see [Replacing the model](#replacing-the-model)).

## Kit

| Slot | Skill | Behaviour |
| --- | --- | --- |
| Passive | **Molten Blood** | Up to +40 armor and +40% damage, scaling with missing health. The lower he gets, the hotter he burns. |
| Primary | **Molten Claw** | Slow two-step claw swipe (320% damage per swing) that ignites. No cancel window until the swing lands. |
| Secondary | **Molten Boulder** | Lobs an arcing boulder (600% damage) that explodes, ignites and leaves a lava pool. |
| Utility | **Elementium Charge** | Committed forward charge with +200 armor, trampling each enemy once for 500% damage and launching them. |
| Utility (alt) | **Wings of the Destroyer** | **Flight.** Up to 6s airborne, steered with the camera; hold the key to stay up, hold jump to climb. Press primary while airborne to enter **Dive Slam** — a meteor drop whose blast radius (12–26m) scales with the height fallen, for 700% damage plus lava pools. Unused flight time is partly refunded to the cooldown. |
| Special | **Cataclysm** | Roots him briefly, then splits the ground in three expanding rings of fissures (400% damage each). Enemies standing close eat every ring. |

Stats: 260 HP (+78/level), 30 armor, 6 move speed (vanilla is 7), 16 base damage (vanilla is 12),
0.8 attack speed, one jump, no hitstun and no freeze. Every number above is exposed in the config
file (`BepInEx/config/com.jayty07.deathwing.cfg`) — see `Modules/Tuning.cs`.

## Installing

1. Install [BepInEx](https://thunderstore.io/package/bbepis/BepInExPack/), then **all five** of these
   R2API packages — the plugin refuses to load if any is missing:
   [R2API_Core](https://thunderstore.io/package/RiskofThunder/R2API_Core/),
   [R2API_ContentManagement](https://thunderstore.io/package/RiskofThunder/R2API_ContentManagement/),
   [R2API_Prefab](https://thunderstore.io/package/RiskofThunder/R2API_Prefab/),
   [R2API_Language](https://thunderstore.io/package/RiskofThunder/R2API_Language/),
   [R2API_RecalculateStats](https://thunderstore.io/package/RiskofThunder/R2API_RecalculateStats/).
   Installing R2API_Core through a mod manager (r2modman / Thunderstore Mod Manager) pulls the rest in
   as dependencies.
2. Drop `Deathwing.dll` into `Risk of Rain 2/BepInEx/plugins/Deathwing/`.
3. Launch the game. Deathwing appears at the end of the character select list.

Multiplayer: the mod is marked `EveryoneMustHaveMod` / `EveryoneNeedSameModVersion`, so every player
in the lobby needs the same version.

## Building

Requires only the .NET SDK (8.0 works); the game assemblies come from NuGet, so no local game install
is needed to compile.

```bash
dotnet build src/Deathwing/Deathwing.csproj -c Release
```

The output lands in `src/Deathwing/bin/Release/Deathwing.dll`. To build straight into a game install:

```bash
dotnet build src/Deathwing/Deathwing.csproj -c Release \
  -p:GameDir="/path/to/Risk of Rain 2"
```

References used:

- `RiskOfRain2.GameLibs` — publicized, stripped game assemblies (from the BepInEx NuGet feed).
- `R2API.*` — content registration, prefab cloning, language tokens, stat hooks.

## How it is put together

```
src/Deathwing/
  DeathwingPlugin.cs          BepInEx entry point; orders module initialisation
  Modules/
    Tuning.cs                 every gameplay number, bound to the BepInEx config
    Tokens.cs                 language tokens and skill descriptions
    DeathwingAssets.cs        fault-tolerant Addressables lookups + molten materials
    Buffs.cs                  Elementium Plating (the +200 armor buff)
    Projectiles.cs            molten boulder and lava pool, cloned from vanilla projectiles
    DeathwingBody.cs          the body prefab: stats, capsule, camera, hitboxes, materials
    Skills.cs                 skill defs, skill families and the passive
    DeathwingSurvivor.cs      SurvivorDef registration and the Molten Blood stat hook
  SkillStates/                one EntityState per skill (plus DiveSlam, entered from flight)
```

Two deliberate implementation choices are worth knowing about:

**The body is a retuned clone of the Commando body.** `PrefabAPI.InstantiateClone` gives a prefab that
already has working networking, state machines, ragdoll, footsteps and a camera rig; the clone is then
rescaled (1.9x, including the character capsule and the KinematicCharacterMotor), restatted, retinted
and given its own hitbox groups and skill families. Nothing on the original Commando assets is
mutated. The trade-off is that the placeholder silhouette is a large, glowing, black-and-orange
Commando rather than a dragon.

**Nothing hard-fails on a missing asset.** Every vanilla asset is fetched through
`DeathwingAssets.Load<T>(params string[] keys)`, which tries each address in turn and returns null
with a warning instead of throwing. Skills null-check what they borrow — for example, Molten Boulder
falls back to a point blast at the aim position if the projectile prefab could not be cloned, and the
lava pools are simply skipped. A game update that renames an address costs visuals, not the survivor.

## Replacing the model

The placeholder art is a tinted Commando. To use a real Deathwing model:

1. Build an AssetBundle containing the rigged model (Unity 2021.3.x, matching the game's version,
   via ThunderKit or a plain Unity project).
2. Load the bundle in `DeathwingAssets.Init()` and set the mesh/materials in
   `DeathwingBody.ReplaceModel`, which is the single place that touches
   `CharacterModel.baseRendererInfos`.
3. Adjust `Tuning.modelScale` plus the hitbox offsets in `DeathwingBody.AddHitBoxes` to the new mesh.

No Blizzard assets are included in this repository. The World of Warcraft model, textures and sounds
are Blizzard's intellectual property — supply your own art if you intend to distribute a build.

Sound effects reuse vanilla Wwise event names (see `Modules/Sounds.cs`); a name that no longer exists
is silently ignored by the game, so they are safe to swap for a custom bank.

## Verification status

The mod compiles clean against the RoR2 1.4.1 assemblies, and all game API usage is therefore
type-checked. It has **not** been run inside the game — that needs a licensed Risk of Rain 2 install,
which is not available on the machine this was written on. Expect to tune skill timings, animation
state names and effect addresses on the first in-game run; the BepInEx log will name any address that
failed to resolve.
