# Tower Defense (Video Game Design, Assignment 2)

3D-physics Tower Defense game in Unity 6000.6.6f1 (URP, Input System only). Due **Tuesday, November 17, 2026**, submitted through GitHub. This folder is the Unity project root.

## Requirements checklist
Thresholds are the same for CSCI 381 and 780 by the A tier; the tiers differ only in where non-straight paths and predictive aim first apply.

- [x] Player can place towers
- [x] Towers fire continuously at a set rate
- [x] Towers aim directly at an enemy (superseded by predictive aim below)
- [x] Enemies follow predetermined paths that are not straight lines
- [x] Game ends when an enemy completes its path
- [ ] At least two tower types with different rate of fire, projectile speed and height
- [ ] Text overlay clearly explains how to place towers
- [ ] Limited number of placeable towers
- [ ] Towers aim at the target's predicted location (`Enemy.Velocity` is already exposed)
- [ ] Multiple enemy types with different speed and HP
- [ ] Non-basic assets (imported graphics must permit reuse)
- [ ] A+: above and beyond

## Layout
- `Assets/Scripts/`: runtime code, namespace `TowerDefense`
- `Assets/Scripts/Editor/GreyboxSceneBuilder.cs`: menu **Tower Defense > Build Greybox Scene** regenerates `Assets/Scenes/Main.unity` and the prefabs. It overwrites manual changes to those files, so stop using it once the scene is hand-edited.
- `Assets/Data/`: `TowerStats` and `EnemyStats` assets. Add a tower or enemy type with a new asset and prefab, not new logic.

## Working in Unity
- Drive the Editor through the `unity` MCP server or `unity command`. Do not hand-edit `.unity`, `.prefab` or `.asset` YAML while an Editor is reachable.
- The Editor must be open for this project. If it can't connect, check for Safe Mode with `unity pipeline list` and fix compile errors first.
- Use `UnityEngine.InputSystem` (`Mouse.current`, `Keyboard.current`). The legacy `Input` class is disabled in this project.

## Working in Blender
- The `blender` MCP server needs Blender running, with **BlenderMCP sidebar tab (N) > Connect** clicked.
- Model at real-world scale, with the origin at the object's base, then export FBX to `Assets/Models/`.
- `mcp-for-blender` is pinned in `.mcp.json`. The add-on in Blender must match its protocol version, so update both together.

## Testing notes
- Play mode freezes when Unity isn't the focused app. For scripted tests, run `UnityEngine.Application.runInBackground = true` with `unity command eval` after entering Play mode.
- `unity command runtime simulate_pointer` and `simulate_key` report success but did not reach the Input System devices in this setup (the mouse position never changed). Test click and key input by hand in the Game view.
