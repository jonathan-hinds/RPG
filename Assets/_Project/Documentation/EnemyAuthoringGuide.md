# Enemy And Creature Authoring Guide

This guide explains how to add a new creature enemy starting from a model FBX and ending with a working enemy placed in the world.

The goal is that designers can follow the steps without needing to write code. The examples use the Wolf because it is the first quadruped creature in the project.

## What You Are Making

A working creature enemy needs four groups of assets:

1. Model and textures: the visible creature.
2. Animation clips: the seven standard creature animations.
3. Gameplay data: name, level, health, aggro behavior, loot, movement, and rewards.
4. Generated prefab: the scene-ready enemy object that has the model, capsule, NavMeshAgent, combat components, and Animator hookup.

Do not start by duplicating an old enemy prefab unless you are only making a quick temporary test. Prefabs can keep old model, Animator, Avatar, or enemy-definition references. The safer workflow is to create the data assets, then run the creature prefab rebuild tool.

## Folder Layout

Use this structure for each creature:

```text
Assets/Characters/<CreatureName>/
  Animations/
    Clips/
  Materials/
  Models/
  Prefabs/
  textures/
```

For example, the Wolf uses:

```text
Assets/Characters/Wolf/Models/wolf2.fbx
Assets/Characters/Wolf/textures/Meshy_AI_Geometric_Wolf_0609183830_texture.png
Assets/Characters/Wolf/textures/Meshy_AI_Geometric_Wolf_0609183830_texture_normal.png
Assets/Characters/Wolf/Animations/Clips/Wolf_AnimationSet.asset
Assets/Characters/Wolf/Prefabs/WolfEnemy.prefab
Assets/Characters/Wolf/Wolf_Visual.asset
```

## Required Animations

Every standard creature should have these seven animations:

1. Idle
2. Walk
3. Run
4. Attack 1
5. Attack 2
6. Damage
7. Death

Idle, Walk, and Run should loop.

Attack 1, Attack 2, Damage, and Death should not loop.

The Wolf FBX contains all seven takes in one file:

```text
IdleFinal
WalkFinal
RunFinal
AttackFinal
Attack2Final
DamageFinal
DeathFinal
```

## Step 1: Import The Model FBX

1. Drag the FBX into `Assets/Characters/<CreatureName>/Models`.
2. Select the FBX in the Project window.
3. In the Inspector, open the `Model` tab.
4. Leave the scale at `1` unless art tells you otherwise. Use the creature visual definition later to set final in-game height.
5. Turn off imported cameras and lights if those options appear.
6. Click `Apply` if you changed anything.

## Step 2: Set Up The Rig And Avatar

This is the step that is easy to miss.

1. Select the creature FBX.
2. Open the `Rig` tab in the Inspector.
3. Set `Animation Type` to `Generic`.
4. Set `Avatar Definition` to `Create From This Model`.
5. Leave optimization off unless an engineer or technical artist asks for it. It is easier to debug creature bones when the hierarchy is not optimized.
6. Click `Apply`.
7. Confirm the FBX now has an Avatar. If the Animator later shows no Avatar, the creature usually will not animate correctly.

Use `Generic` for normal creatures. Use `Humanoid` only when the creature is truly using a humanoid rig and the project needs humanoid retargeting.

## Step 3: Set Up The Animation Takes

1. Select the creature FBX.
2. Open the `Animation` tab.
3. Make sure `Import Animation` is enabled.
4. Confirm the clip list contains the seven required animations.
5. Rename clips in the clip list if the imported names are unclear.
6. Select the Idle clip and enable loop time.
7. Select the Walk clip and enable loop time.
8. Select the Run clip and enable loop time.
9. Select Attack 1, Attack 2, Damage, and Death and make sure loop time is disabled.
10. Click `Apply`.

If the FBX has all seven animations in one file, it is fine to use the FBX's embedded clips directly in the animation set.

If each animation is a separate FBX, import each animation FBX with the same Generic rig settings. If Unity lets that animation FBX create its own Avatar cleanly, that is fine. If the animation FBX does not create a usable Avatar, set `Avatar Definition` to `Copy From Other Avatar` and choose the Avatar from the main model FBX. Then use the clip from each source file.

## Step 4: Import And Check Textures

1. Put the base color texture in `Assets/Characters/<CreatureName>/textures`.
2. Put the normal map texture in the same folder if art supplied one.
3. Select the normal map texture.
4. In the Inspector, set `Texture Type` to `Normal map`.
5. Click `Apply`.

If the model appears gray, the material probably does not have the base color texture assigned yet.

If the model looks inside-out or too shiny, ask an artist or technical artist to check material settings before changing gameplay data.

## Step 5: Create The Gameplay Profile

The character profile controls the creature's name, level, health, mana, faction, and base stats.

1. Go to `Assets/_Project/Configs/Characters`.
2. Right-click and choose `Create > RPG Clone > Characters > Character Profile`.
3. Name it `<CreatureName>.asset`.
4. Set `Display Name` to the creature name players should see.
5. Set `Faction` to `Hostile` for normal enemies.
6. Set level, health, mana, and base stats.

For the Wolf, the profile is:

```text
Assets/_Project/Configs/Characters/Wolf.asset
```

## Step 6: Create Or Assign Loot

Loot is optional, but most enemies should have a loot table.

1. Go to `Assets/_Project/Configs/Loot`.
2. Right-click and choose `Create > RPG Clone > Loot > Loot Table`.
3. Name it `<CreatureName>_Trash_Loot.asset` or another clear name.
4. Add item entries.
5. Set each entry's drop chance and quantity.

For the Wolf, the loot table is:

```text
Assets/_Project/Configs/Loot/Wolf_Trash_Loot.asset
```

## Step 7: Create The Enemy Definition

The enemy definition controls how the creature behaves.

1. Go to `Assets/_Project/Configs/Enemies`.
2. Right-click and choose `Create > RPG Clone > Enemies > Enemy Definition`.
3. Name it `<CreatureName>_Aggressive.asset` for a normal hostile enemy.
4. Assign the character profile.
5. Set `Disposition`.
6. Assign `Assets/_Project/Configs/Abilities/Auto_Attack.asset` to `Auto Attack Ability`.
7. Add the same Auto Attack asset to the `Abilities` list.
8. Assign the loot table if the creature should drop loot.
9. Tune movement and aggro.

Common enemy-definition values:

```text
Disposition: Aggressive for enemies that attack first, Docile for enemies that fight back only after being hit.
Aggro Radius: how close the player must be before the enemy notices them.
Leash Radius: how far the enemy can chase before returning home.
Can Roam: enabled for wandering creatures.
Roam Radius: how far the enemy wanders from its placed home point.
Walk Speed: speed while roaming.
Chase Speed: speed while chasing a target.
Stopping Distance: how close the agent tries to stand while attacking.
Respawn Seconds: how long before the enemy returns after death.
```

For the Wolf, the enemy definition is:

```text
Assets/_Project/Configs/Enemies/Wolf_Aggressive.asset
```

## Step 8: Create The Animation Set

The animation set tells the shared creature Animator which seven clips to use.

1. Go to `Assets/Characters/<CreatureName>/Animations/Clips`.
2. Right-click and choose `Create > RPG Clone > Animation > Creature Animation Set`.
3. Name it `<CreatureName>_AnimationSet.asset`.
4. Assign `Assets/_Project/Animations/Creatures/MMOCreatureBase.controller` to `Base Controller`.
5. Assign the creature's Idle clip.
6. Assign the creature's Walk clip.
7. Assign the creature's Run clip.
8. Assign the creature's Attack 1 clip.
9. Assign the creature's Attack 2 clip.
10. Assign the creature's Damage clip.
11. Assign the creature's Death clip.
12. Set `Walk Speed` and `Run Speed` close to the enemy definition's walk and chase speeds.
13. Leave `Apply Root Motion` off unless an engineer specifically says the creature should move from animation root motion.

For the Wolf, the animation set uses all seven generated Wolf clips:

```text
Wolf_Idle
Wolf_Walk
Wolf_Run
Wolf_Attack1
Wolf_Attack2
Wolf_Damage
Wolf_Death
```

## Step 9: Create The Creature Visual Definition

The creature visual definition connects the model, textures, animation set, enemy definition, and body shape.

1. Go to `Assets/Characters/<CreatureName>`.
2. Right-click and choose `Create > RPG Clone > Animation > Creature Visual Definition`.
3. Name it `<CreatureName>_Visual.asset`.
4. Set `Creature Id` to the creature name without spaces, such as `Wolf`.
5. Set `Display Name` to the readable name, such as `Wolf`.
6. Assign the main model FBX to `Model Prefab`.
7. Assign the base color texture to `Diffuse Texture`.
8. Assign the normal map texture to `Normal Texture` if the creature has one.
9. Assign the animation set.
10. Assign the default enemy definition.
11. Add the same enemy definition to `Matching Enemy Definitions`.
12. Add scene name prefixes if old placeholders should convert to this creature, such as `Wolf`.

The prefab rebuild tool looks for creature visual definitions under `Assets/Characters`, so make sure the visual definition is saved there.

## Step 10: Choose Biped Or Quadruped

The body shape controls the creature's generated capsule.

Use `Biped` for upright enemies. Biped enemies use a vertical capsule.

Good starting values for a biped:

```text
Body Type: Biped
Target Height: 2.25
Collider Radius: 0.6
Collider Length: 2.25
Collider Center: (0, 1.125, 0)
```

Use `Quadruped` for creatures that are longer than they are tall. Quadruped enemies use a horizontal capsule running front-to-back.

Good starting values for a medium quadruped:

```text
Body Type: Quadruped
Target Height: 1.15
Collider Radius: 0.38
Collider Length: 1.75
Collider Center: (0, 0.56, 0)
```

For quadrupeds, remember:

1. `Target Height` controls how tall the visual model is scaled.
2. `Collider Radius` controls body thickness.
3. `Collider Length` controls front-to-back body length.
4. `Collider Center` should sit around the creature's chest or body center, not at the feet.

The Wolf uses the medium quadruped values above.

## Step 11: Rebuild The Creature Prefab

1. Save the creature visual definition.
2. Open the Unity menu `Tools/RPG Clone/Creatures/Rebuild Creature Visual Prefabs`.
3. Wait for Unity to finish.
4. Check `Assets/Characters/<CreatureName>/Prefabs`.
5. Confirm `<CreatureName>Enemy.prefab` exists.

For the Wolf, the generated prefab is:

```text
Assets/Characters/Wolf/Prefabs/WolfEnemy.prefab
```

The generated prefab should have:

```text
Capsule Collider
NavMeshAgent
MMO Character Identity
MMO Combatant
MMO Ability System
MMO Character Regeneration
MMO Lootable Corpse
MMO Auto Attack Controller
MMO Enemy Controller
MMO Creature Animator
The creature model as a child object
```

Do not manually remove these components from the generated prefab.

## Step 12: Place The Enemy In The World

1. Open the target scene.
2. Drag `Assets/Characters/<CreatureName>/Prefabs/<CreatureName>Enemy.prefab` into the scene.
3. Put it under the scene's creature parent, such as `Starter World/Placeholder Creature Spawns`.
4. Rename the instance, such as `Wolf 01`.
5. Move it to its home position.
6. Rotate it toward the direction it should face at scene start.
7. Confirm the `MMO Enemy Controller` has the correct enemy definition.
8. Save the scene.

The placed position is the enemy's home. Roaming happens around that home position.

The Wolf scene instances are:

```text
Starter World/Placeholder Creature Spawns/Wolf 01
Starter World/Placeholder Creature Spawns/Wolf 02
```

## Step 13: Make Sure The Enemy Can Walk

Enemies need a valid NavMesh.

1. In the scene hierarchy, find `Starter World/Navigation`.
2. Make sure it is active.
3. Run `Tools/RPG Clone/Enemies/Rebuild Active Scene NavMesh`.
4. Save the scene.
5. Enter Play Mode and watch the enemy.

If all enemies show `Failed to create agent because there is no valid NavMesh`, the scene NavMesh is not active or was not built.

If only one enemy refuses to move, check that enemy's position. It may be off the NavMesh or floating above the terrain.

## Step 14: Play-Test Checklist

Before calling the creature done, check these items:

1. The model appears with the right texture.
2. The model is not floating or buried.
3. The capsule roughly covers the creature body.
4. Idle plays when the enemy stands still.
5. Walk plays while roaming.
6. Run plays while chasing.
7. Attack 1 and Attack 2 play during auto attacks.
8. Damage plays when the enemy is hit.
9. Death plays when the enemy dies.
10. The enemy returns or respawns according to the enemy definition.
11. The enemy drops expected loot if a loot table is assigned.

## Troubleshooting

### The creature has no Avatar

Select the FBX, open the `Rig` tab, set `Animation Type` to `Generic`, set `Avatar Definition` to `Create From This Model`, then click `Apply`.

### The model appears but does not animate

Check these:

1. The FBX has an Avatar.
2. The animation set has `MMOCreatureBase.controller` assigned.
3. All seven animation fields are assigned.
4. The generated prefab has `MMO Creature Animator`.
5. The visual child has an Animator component.

### The wrong model appears

Do not keep using a duplicated old prefab. Open the creature visual definition, assign the correct model, then run `Rebuild Creature Visual Prefabs`.

### The old enemy name or gameplay data appears

Check the generated prefab's `MMO Enemy Controller`. It should point at the new enemy definition, such as `Wolf_Aggressive`.

Also check the creature visual definition's `Default Enemy Definition` and `Matching Enemy Definitions`.

### The creature is too tall, tiny, floating, or buried

Change the creature visual definition, not the scene instance.

Use these fields:

```text
Target Height
Collider Radius
Collider Length
Collider Center
Visual Local Offset
Visual Local Euler Angles
Model Yaw Offset Degrees
```

After changing them, run `Rebuild Creature Visual Prefabs` again.

### The quadruped capsule is standing upright

Open the creature visual definition and set `Body Type` to `Quadruped`. Rebuild the creature visual prefab.

### The biped capsule is lying down

Open the creature visual definition and set `Body Type` to `Biped`. Rebuild the creature visual prefab.

### Idle, Walk, or Run stops after one cycle

Select the FBX, open the `Animation` tab, select that clip, enable loop time, and click `Apply`.

### Attack or Death repeats forever

Select the FBX, open the `Animation` tab, select that clip, disable loop time, and click `Apply`.

### The enemy does not roam

Check the enemy definition:

```text
Can Roam: enabled
Roam Radius: greater than 0
Walk Speed: greater than 0
```

Then check the scene:

```text
Starter World/Navigation is active
The NavMesh has been rebuilt
The enemy is placed on the NavMesh
```

## Wolf Reference

Use the Wolf as the current working example.

```text
Model:
Assets/Characters/Wolf/Models/wolf2.fbx

Textures:
Assets/Characters/Wolf/textures/Meshy_AI_Geometric_Wolf_0609183830_texture.png
Assets/Characters/Wolf/textures/Meshy_AI_Geometric_Wolf_0609183830_texture_normal.png

Animation Set:
Assets/Characters/Wolf/Animations/Clips/Wolf_AnimationSet.asset

Visual Definition:
Assets/Characters/Wolf/Wolf_Visual.asset

Enemy Definition:
Assets/_Project/Configs/Enemies/Wolf_Aggressive.asset

Generated Prefab:
Assets/Characters/Wolf/Prefabs/WolfEnemy.prefab

Scene Instances:
Starter World/Placeholder Creature Spawns/Wolf 01
Starter World/Placeholder Creature Spawns/Wolf 02
```

Wolf body setup:

```text
Body Type: Quadruped
Target Height: 1.15
Collider Radius: 0.38
Collider Length: 1.75
Collider Center: (0, 0.56, 0)
```
