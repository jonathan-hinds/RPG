# Enemy Authoring Guide

This project uses data-driven enemy definitions. Designers create and tune enemies with ScriptableObject assets, then place an enemy prefab in the scene.

## One-time setup

1. Open the Unity menu: `Tools/RPG Clone/Enemies/Create Enemy Authoring Assets`.
2. Open the active scene and run: `Tools/RPG Clone/Enemies/Rebuild Active Scene NavMesh`.
3. Optional for the starter world: run `Tools/RPG Clone/Enemies/Convert Starter World Enemy Placeholders`.

## Creating a new enemy type

1. Create or duplicate a character profile in `Assets/_Project/Configs/Characters`.
2. Set the profile's `Faction` to `Hostile` for any creature the player can fight.
3. Create or duplicate an enemy definition in `Assets/_Project/Configs/Enemies`.
4. Assign the character profile to `Character Profile`.
5. Assign `Assets/_Project/Configs/Abilities/Auto_Attack.asset` to `Auto Attack Ability`.
6. Add the same auto attack asset to the `Abilities` list.
7. Tune the enemy definition fields:
   - `Disposition`: `Aggressive` attacks valid targets inside aggro radius. `Docile` only fights back after being damaged.
   - `Aggro Radius`: distance where aggressive enemies notice players.
   - `Leash Radius`: max distance from home before the enemy resets and returns.
   - `Can Roam` and `Roam Radius`: small idle wandering around the placed home position.
   - `Walk Speed` and `Chase Speed`: roaming and pursuit speeds.
   - `Stopping Distance`: how close the NavMeshAgent tries to stand while attacking.

## Placing enemies

1. Drag `Assets/_Project/Prefabs/Enemies/EnemyCapsule.prefab` into the scene.
2. Move it to the enemy's home position.
3. Assign the desired `Enemy Definition` on the `MMO Enemy Controller`.
4. Rebuild the NavMesh after changing walkable terrain or static geometry.

At runtime, the placed position becomes the enemy's home. Roaming stays near that home, aggressive enemies scan for hostile targets, and docile enemies wait until hit before retaliating.

## Current combat behavior

Enemy ability pools are already data-driven through the `Abilities` list. For now, enemies use `Auto Attack` only. Future enemy abilities should be added as new `MMOAbilityDefinition` assets and then referenced from the enemy definition, without changing placed scene objects.
