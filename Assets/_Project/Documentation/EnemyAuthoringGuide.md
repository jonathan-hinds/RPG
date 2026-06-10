# Enemy Authoring Guide

This project uses data-driven enemy definitions. Designers create and tune enemies with ScriptableObject assets, then place an enemy prefab in the scene.

## Creature visual workflow

Creature enemies use two layers of data:

1. Gameplay data says what the enemy is: name, level, health, aggro behavior, roam radius, loot, and rewards.
2. Visual data says what the enemy looks like: model, textures, animation clips, body shape, capsule size, and prefab output.

The standard creature animation setup expects seven animations:

1. Idle
2. Walk
3. Run
4. Attack 1
5. Attack 2
6. Damage
7. Death

These seven clips feed the shared creature controller, so designers do not need to make a new Animator Controller for every creature.

## One-time setup

1. Open the Unity menu: `Tools/RPG Clone/Enemies/Create Enemy Authoring Assets`.
2. Open the Unity menu: `Tools/RPG Clone/Creatures/Create Standard Creature Visual Definitions`.
3. Open the Unity menu: `Tools/RPG Clone/Creatures/Rebuild Creature Visual Prefabs`.
4. Open the active scene and run: `Tools/RPG Clone/Enemies/Rebuild Active Scene NavMesh`.
5. Optional for the starter world: run `Tools/RPG Clone/Enemies/Convert Starter World Enemy Placeholders`.

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

## Importing a new creature FBX

Use this checklist when a new creature arrives from art.

1. Put the main model FBX in `Assets/Characters/<CreatureName>/Models`.
2. Put the base color and normal textures in `Assets/Characters/<CreatureName>/textures`.
3. Confirm the FBX has the seven needed animation takes: Idle, Walk, Run, Attack 1, Attack 2, Damage, and Death.
4. If the FBX contains all seven takes, the installer can extract those clips into `Assets/Characters/<CreatureName>/Animations/Clips`.
5. If art provides one FBX per animation, keep those source FBXs in an `Animations/Source` folder and extract one clip from each source.
6. Use Generic animation import for creatures. Humanoid import is for player-like rigs, not most creatures.
7. Loop only Idle, Walk, and Run. Attack, Damage, and Death should not loop.

For the Wolf, the imported model is `Assets/Characters/Wolf/Models/wolf2.fbx`. Its seven takes are named `IdleFinal`, `WalkFinal`, `RunFinal`, `AttackFinal`, `Attack2Final`, `DamageFinal`, and `DeathFinal`. The generated clips live in `Assets/Characters/Wolf/Animations/Clips`.

## Body Shape

Choose the body shape in the creature visual definition.

`Biped` is for upright creatures. It uses a vertical capsule, like the existing Bristleback and Ash Canyon creatures.

`Quadruped` is for creatures that are longer than they are tall. It uses a horizontal capsule running front-to-back, while the NavMeshAgent still uses the creature height for navigation.

For quadrupeds, tune these fields together:

1. `Target Height`: how tall the visual model should appear after scaling.
2. `Collider Radius`: how thick the body capsule is.
3. `Collider Length`: how long the front-to-back capsule is.
4. `Collider Center`: where the capsule sits above the ground.

The Wolf uses `Quadruped`, a 1.15 target height, 0.38 collider radius, 1.75 collider length, and a collider center of `(0, 0.56, 0)`.

## Creating creature visual assets

The standard path is:

1. Create or update the gameplay assets: character profile, enemy definition, and loot table.
2. Create a creature animation set in `Assets/Characters/<CreatureName>/Animations/Clips`.
3. Create a creature visual definition at `Assets/Characters/<CreatureName>/<CreatureName>_Visual.asset`.
4. Assign the model, textures, animation set, default enemy definition, and matching enemy definitions.
5. Set the body shape and capsule values.
6. Run `Tools/RPG Clone/Creatures/Rebuild Creature Visual Prefabs`.
7. Confirm the prefab appears at `Assets/Characters/<CreatureName>/Prefabs/<CreatureName>Enemy.prefab`.

The generated prefab should already include the expected runtime pieces: capsule collider, NavMeshAgent, identity, combat, abilities, lootable corpse, enemy controller, auto attack, and creature animator.

## Placing visual creature enemies

1. Drag `Assets/Characters/<CreatureName>/Prefabs/<CreatureName>Enemy.prefab` into the scene.
2. Place it under the scene's creature spawn parent, such as `Starter World/Placeholder Creature Spawns`.
3. Move it to the enemy's home position.
4. Rotate it toward the direction it should face when the scene starts.
5. Confirm the `MMO Enemy Controller` uses the right enemy definition.
6. Save the scene.
7. Rebuild the NavMesh if terrain, static obstacles, or walkable areas changed.

The placed position becomes the creature's home. If `Can Roam` is enabled, it wanders around that spot using the enemy definition's roam radius.

## Placing enemies

1. Drag `Assets/_Project/Prefabs/Enemies/EnemyCapsule.prefab` into the scene.
2. Move it to the enemy's home position.
3. Assign the desired `Enemy Definition` on the `MMO Enemy Controller`.
4. Rebuild the NavMesh after changing walkable terrain or static geometry.

At runtime, the placed position becomes the enemy's home. Roaming stays near that home, aggressive enemies scan for hostile targets, and docile enemies wait until hit before retaliating.

## Current combat behavior

Enemy ability pools are already data-driven through the `Abilities` list. For now, enemies use `Auto Attack` only. Future enemy abilities should be added as new `MMOAbilityDefinition` assets and then referenced from the enemy definition, without changing placed scene objects.
