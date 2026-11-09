# Knight of the Living Dead: game plan

## Context
Turn the grey-box skeleton in `/Users/anjujiang/assignment2` into a polished 3D Tower Defense game, due **Tue Nov 17, 2026**, meeting every assignment requirement.

- **Inspirations:** Bloons TD (rounds, upgrade paths, hero levels) and SAS: Zombie Assault (hordes, screen-clearing strikes).
- **Your choices:** ruined medieval town; 3 towers with 2 paths of 3 tiers; AI-generated models.
- **Art:** low-poly stylized, chibi heads on every character. The references set the style; the modern outfits and city become medieval.

## Requirements coverage
| Requirement | How it's met |
|---|---|
| Place towers | Click open ground, with a ghost preview and range ring |
| Fire continuously at a set rate | Each tower's stats |
| Aim at an enemy, then predicted position | Intercept solver using position and `Enemy.Velocity` (replaces direct aim) |
| Non-straight predetermined path | Winding route through town |
| Game ends when an enemy completes its path | **One leak = game over** |
| 2+ tower types differing in rate, projectile speed, height | 3 towers, table below |
| Text overlay explains placement | Always-visible instruction panel |
| Limited towers | Hard cap of **10** on screen, separate from gold |
| 2+ enemy types differing in speed and HP | 6 zombie types |
| Non-basic assets | Generated models |
| 3D physics | Rigidbody projectiles, knockback, and physics corpse pieces |
| A+ | Upgrades, hero, abilities, boss, economy, polish |

**Rule clash to know about:** Bloons uses lives, but the assignment ends the game on a single leak. I'm following the assignment, so balance has to be forgiving and the Bloons-style lives are out.

## Game loop
Build, press **Start Wave**, zombies walk the route, earn gold, upgrade, repeat. **Survive 20 waves** to win; wave 20 is the boss. Includes a 2x speed toggle and restart.

## Towers
| Tower | Attack | Fire rate | Projectile speed | Height | Range | Cost |
|---|---|---|---|---|---|---|
| **Knight Bastion** | Thrown axes | 2/s | 14 | 2.5 | 9 | 100 |
| **Archer Tower** | Arrows | 1/s | 35 | 7 | 20 | 150 |
| **Mage Spire** | Splash orbs | 0.5/s | 10 | 5 | 13 | 225 |

Bloons-style rule: one path can reach tier 3, and the other stops at tier 1. Each tier has a visible model change.

| Tower | Path | Tier 1 | Tier 2 | Tier 3 |
|---|---|---|---|---|
| Knight | Steel | Sharpened Axes | Full Plate | Paladin (piercing hammers, knockback; gold armor) |
| Knight | Banner | War Banner (nearby fire rate) | War Horn (nearby range) | Crusader Standard (big aura) |
| Archer | Longbow | Yew Longbow (range) | Eagle Eye (damage, pierce) | Ballista (heavy piercing bolts) |
| Archer | Fire | Pitch Arrows (burn) | Volley (3 arrows) | Inferno Battery (burning area) |
| Mage | Frost | Chill Orbs (slow) | Ice Nova (brief freeze) | Blizzard Archmage (area slow) |
| Mage | Storm | Spark Chain (2 jumps) | Storm Staff (more jumps) | Thunder Spire (storm cloud) |

## Hero: Sir Galahead (working name)
One free hero, placed once, not counted in the tower cap. Throws a sword as a normal attack. "Turn" is treated as **wave**: gains XP automatically after every wave, with no kills needed, and reaches level 10 around wave 15.

| Level | Unlock |
|---|---|
| 1 | Sword throw |
| 3 | **Holy Smite** (active area strike on cooldown, like an SAS strike) |
| 5 | Aura of Valor (nearby fire rate +15%); new armor |
| 7 | Shield Wall (active: stops zombies in an area for 3s) |
| 10 | Excalibur Rain (huge active strike); gold armor |

## Zombies (starting numbers, tuned in playtesting)
| Type | HP | Speed | Notes |
|---|---|---|---|
| **Shambler** | 8 | 2.5 | Basic (your second reference, in peasant rags) |
| **Runner** | 4 | 5.5 | Fast and fragile |
| **Clubber** | 25 | 2 | Spiked club, arrows in its back (your first reference) |
| **Plague Bloater** | 15 | 2 | Bursts into 3 Runners, a nod to Bloons |
| **Armored Revenant** | 30 | 2.2 | Rusted knight; flat damage reduction per hit |
| **Zombie King** | 600 | 1.2 | Wave 20 boss |

## Map
One ruined medieval town: a winding cobbled road from the graveyard to the broken town gate. Collapsed timber houses, walls, carts, a well, gravestones, lanterns. Moody overcast light, with URP post-processing.

## Art pipeline
**Phase 0 is a test before any bulk spending, because three things are unknown:**
1. **Generator availability.** Blender isn't connected yet, so I can't see which generators you have, or what they cost. One may need your API key or credits.
2. **Rigging.** Generated meshes often rig badly.
3. **Style match.** Whether results look chibi and low-poly enough.

**Estimated count: about 47 generations**
- Tower units: 21 (each tower's base plus 6 upgrades, as a separate model so it can turn to aim)
- Tower structures: 9 (base and both tier 3s per tower)
- Zombies: 6
- Hero: 3 (levels 1, 5, 10)
- Town props: about 8

Ground and road textures come from Poly Haven (CC0).

**I'll show you the cost per model before the bulk run.** To cut cost, tier 1 can reuse the base model with a scripted prop, saving 6 generations.

**Per-model steps:** generate from a shared style prompt, reduce to budget (characters about 3k triangles, structures about 5k), set the origin at the feet and the scale, export FBX to `Assets/Models/`, make a prefab.

**Animation:** procedural waddle, recoil and squash in Unity works on any mesh, so it's the guaranteed baseline. Real rigs get added only if the test shows they're reliable.

**Your references:** I'll use text prompts describing the style rather than uploading your images. They look like existing commercial art, and the assignment only allows outside graphics when the source permits it. I'll also check the generator's terms allow this use.

## Code (reuses the existing skeleton in `Assets/Scripts/`)
- **Extend:**
  - `Tower.cs`: predictive aim, modifiers, model swaps
  - `Enemy.cs`: armor, burn/slow/stun, split on death
  - `Projectile.cs`: pierce, splash, chain, burn
  - `TowerPlacer.cs`: cap, gold, ghost preview
  - `WaveSpawner.cs`: data-driven waves
  - `GameManager.cs`: win state, states; the old on-screen text goes away
- **Add:**
  - `UpgradeDefinition` and `TowerUpgrades`
  - `WaveDefinition`
  - `Economy`
  - `Hero`, `HeroLevelTable` and hero abilities
  - `ProceduralAnimator`
  - `CameraRig`
  - HUD and upgrade panel in uGUI with TextMeshPro
- **Data:** every stat lives in ScriptableObjects under `Assets/Data/`, so balancing needs no code changes.
- **Scene:** `GreyboxSceneBuilder.cs` stays only until the real town scene exists. Set the product name to "Knight of the Living Dead", and update the `CLAUDE.md` checklist as items are finished.

## Schedule
Gameplay comes first, so a complete, gradeable game exists early even if art runs late.

| Dates | Work |
|---|---|
| Nov 4-5 | **Phase 0:** connect Blender, test one zombie, you approve the style and cost |
| Nov 5-8 | All required systems in grey-box: tower types, overlay, cap, predictive aim, zombie types, waves. **Safe A-tier build by Nov 8** |
| Nov 8-10 | Upgrades, hero, abilities, HUD |
| Nov 8-12 | Bulk art generation and processing, in parallel |
| Nov 12-14 | Town scene, lighting, effects, sound (free licensed effects) |
| Nov 15-16 | Balance, your playtesting, build, README with credits |
| Nov 17 | Buffer; give the grader repo access and submit |

**Cut order if time runs short:** rigs, then sound, then hero level 7, then generated tier 1 models, then Inferno and Thunder special effects. Required features are never cut.

## Verification
- **Each system:** scripted Play mode checks through Unity MCP (the method used for the skeleton), checking state, console errors and screenshots.
- **Predictive aim:** compare hit rates against fast Runners with direct and predicted aim. Predicted should be clearly higher.
- **Balance:** scripted test layouts must clear all 20 waves, and no towers must lose.
- **Art:** Blender `look` and Unity Game view screenshots of each model, compared with your references.
- **By hand (you):** clicks and keys, since simulated input didn't reach the game earlier; this includes the click-to-place and R-to-restart test still open from the base setup. Then a full playthrough for fun and difficulty.
- **Final:** run a standalone build; walk the requirements table line by line; confirm the repo has the final commit and the grader has access.

## What I need from you
1. **Blender:** BlenderMCP tab (press N) > Connect. It isn't connected right now, and this blocks Phase 0.
2. **Generator:** add an API key or credits if the test shows one is needed.
3. **Unity CLI:** an update is available (1.0.0-beta.13 to beta.16). Approving this plan does not cover it; tell me if you want it. I'd hold off unless something breaks.
