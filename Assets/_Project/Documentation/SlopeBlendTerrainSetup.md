# Slope Blend Terrain Setup

This project now includes `RPG Clone/Terrain/MMO Slope Blend Terrain`, a URP shader for Barrens-style terrain that automatically blends a flat ground texture into a steep cliff texture using world-space normals.

## Assets

- Shader: `Assets/_Project/Shaders/MMOSlopeBlendTerrain.shader`
- Material: `Assets/_Project/Generated/Materials/Barrens_Slope_Blend_Terrain.mat`
- Flat texture: `Assets/RockGroundTexture.png`
- Flat variation texture: `Assets/RockGroundTextureVarient.png`
- Steep texture: `Assets/RockCliffFaceTexture.png`

No C# helper script is required. The shader works from object/world position and surface normals, so it can be used on Unity Terrain or static terrain-like meshes.

## Assign To Unity Terrain

1. Select the Terrain GameObject in the scene.
2. In the Terrain component, open Terrain Settings.
3. Set the terrain material mode to Custom, then assign `Barrens_Slope_Blend_Terrain`.
4. Confirm the flat texture is `RockGroundTexture` and the steep texture is `RockCliffFaceTexture`.
5. Move around the elevated Barrens areas and tune the slope controls while viewing the scene camera.

Unity TerrainLayers and splat painting are not used by this material. Treat this as a procedural base material for the terrain surface.

## Assign To Mesh Terrain

1. Select the terrain-like mesh renderer.
2. Assign `Barrens_Slope_Blend_Terrain` to the Mesh Renderer material slot.
3. Make sure the mesh has valid normals. Recalculate normals in the model importer or mesh authoring tool if cliffs do not blend correctly.

## Parameter Tuning

- `Flat Texture Tiling Size`: World units per tile for mostly horizontal ground. Increase it if the texture feels too busy; decrease it for more detail near paths and camps.
- `Flat Variation Texture Tiling Size`: World units per tile for the noise-mixed ground variant. Use a slightly different value than the main flat texture to avoid synchronized repetition.
- `Flat Variation Blend Strength`: Controls how strongly `RockGroundTextureVarient` replaces the base flat ground in noise patches.
- `Flat Variation Noise Size`: Controls the world-space size of variant patches. Larger values create broad Barrens color zones; smaller values create mottled detail.
- `Flat Variation Noise Softness`: Softens the mask between the base flat texture and variant texture. Increase this because the two textures are not seamless with each other.
- `Steep Texture Tiling Size`: World units per tile for slopes and cliff faces. Cliffs usually read better a bit tighter than flat ground.
- `Slope Blend Threshold`: Lower values put cliff texture on gentler slopes. Higher values reserve cliff texture for near-vertical terrain.
- `Slope Blend Softness`: Increases the width of the transition between ground and cliff. Use more softness for eroded mesas and less for sharper canyon walls.
- `Texture Rotation/Randomization Strength`: Rotates and offsets each world-space tile. Increase this when the grid pattern is visible.
- `Tile Edge Blending Strength`: Crossfades neighboring randomized tiles. Increase it if randomized tile boundaries are visible; reduce it if the surface becomes too soft or blurry.
- `Same-Level Variation Strength`: Adds broad color variation across large areas so flat ground does not become a uniform repeated carpet.
- `Same-Level Variation Size`: Controls the size of the broad variation patches. Larger values suit open Barrens plains.
- `Triplanar Projection Sharpness`: Higher values make the dominant projection stronger. Lower values make projection transitions softer.

## Suggested Barrens Starting Point

- Flat Texture Tiling Size: `7`
- Flat Variation Texture Tiling Size: `9`
- Flat Variation Blend Strength: `0.25` to `0.4`
- Flat Variation Noise Size: `30` to `60`
- Flat Variation Noise Softness: `0.15` to `0.25`
- Steep Texture Tiling Size: `5`
- Slope Blend Threshold: `0.36`
- Slope Blend Softness: `0.18`
- Texture Rotation/Randomization Strength: `0.65` to `0.75`
- Tile Edge Blending Strength: `0.25` to `0.35`
- Same-Level Variation Strength: `0.2` to `0.3`
- Same-Level Variation Size: `60` to `90`

For a World of Warcraft inspired zone, use the material as the broad terrain read, then add authored paths, rock props, scrub foliage, decals, and landmark color accents to break up travel routes and quest hubs.
