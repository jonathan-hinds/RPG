# Frost Shock generated image sources

The five source images in `Textures/Sources` were produced with the built-in image-generation tool, then converted from a flat `#ff00ff` chroma key to RGBA PNGs with the image-generation skill's `remove_chroma_key.py` helper.

All prompts used the `stylized-concept` use case and requested classic fantasy MMORPG hand-painted frost art with white-hot centers, pale cyan, saturated blue, deep blue, sparse violet accents, no text, no watermark, and no environmental background.

1. `FrostShock_EnergyBurstAtlas_Source.png`: exact 4x4 grid of compact cores, right-facing frost spears, radial bursts, and jagged shock rings.
2. `FrostShock_IceShardAtlas_Source.png`: exact 4x4 grid of narrow, triangular, curved, broken, chipped, plated, foot-gripping, and lower-leg ice forms.
3. `FrostShock_CrackGroundPatchAtlas_Source.png`: exact 4x4 grid with a crack row, top-down ground-patch row, body-frost row, and broad highlight-mask row.
4. `FrostShock_MistSnowTrailAtlas_Source.png`: exact 4x4 grid with painterly mist, snow clusters, right-facing trail ribbons, and horizontal frozen-energy bands.
5. `FrostShock_MeshSurfaceAtlas_Source.png`: exact 2x2 atlas of horizontal crystalline spear flow, chunky ice facets, broken energy ribbons, and erosion/crack breakup, authored specifically for scrolling and dissolving across mesh volumes.

Each prompt required isolated cells on a perfectly flat solid `#ff00ff` background, generous padding where appropriate, no use of the key color in the frost art, and bold readable silhouettes instead of realistic transparent ice.
