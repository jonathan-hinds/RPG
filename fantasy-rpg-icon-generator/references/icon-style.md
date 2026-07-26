# Icon Style Bible

## Target

Create original inventory and ability artwork with the compact, hand-painted readability of classic MMORPG interface icons. Use `assets/style-references/target_wow_inventory_reference.png` as the principal presentation reference. Use `assets/style-references/rejected_overrendered_reference.png` as a negative example.

The target is **small game UI artwork**, not a high-resolution fantasy product render.

## Correct visual language

- Square icon composition designed for 32x32 and 64x64 display.
- One immediately recognizable subject.
- Tight crop; the subject usually fills 75-95% of the canvas.
- Chunky silhouette and simplified construction.
- Hand-painted, graphic forms with visible shape grouping.
- Moderate stylization and exaggerated proportions.
- Bold local color with clear material separation.
- A few deliberate highlights rather than reflections on every edge.
- Dark, simple, abstract, or softly painted background with a gentle vignette toward the canvas edges.
- Slightly uneven painted edges are acceptable and desirable.
- Detail is concentrated around the focal feature and simplified elsewhere.

## Explicitly avoid the rejected direction

The rejected reference is too cinematic, realistic, and over-rendered. Do not reproduce its traits:

- No photorealistic or near-photorealistic metal.
- No physically based material showcase.
- No thousands of chain links, scratches, cracks, rivets, or micro-details.
- No dramatic studio product photography.
- No full object presentation when a tighter symbolic crop reads better.
- No high-frequency texture covering the whole icon.
- No harsh black cinematic vignette swallowing the silhouette. Use only a soft painted edge vignette that supports readability.
- No concept-art canvas intended to be admired at 1024px.

At full resolution the painting may look intentionally simplified. That is correct because the final target is a tiny interface slot.

## Composition hierarchy

1. Primary silhouette: identify the item or action.
2. One focal feature: blade edge, gem, flame, eye, crack, potion liquid, or impact point.
3. Secondary material cue: leather wrap, metal trim, cloth fold, bone, wood, or magical accent.
4. Background: only enough to separate the silhouette.

For item icons, prefer a strong diagonal or three-quarter angle. Crop long weapons near the edges. Armor may be shown as a simplified torso, helm, glove, boot, or shoulder silhouette rather than a complete product render.

## Rendering scale

Paint as though using a broad brush:

- Large color blocks first.
- Two or three major value groups.
- One concentrated highlight family.
- A limited number of interior marks.
- Shapes should survive downscaling without depending on texture.

## Lighting

- Use one readable light direction.
- Reserve the brightest values for the focal feature.
- Use selective rim light only where it improves the silhouette.
- Keep shadows broad and painted.
- Avoid HDR reflections, realistic bounce lighting, and full-surface specular noise.

## Color

- Use 2-4 dominant colors.
- Let the item carry stronger local color than the background.
- Use saturation selectively; do not wash the whole icon in one color.
- Keep low-rarity items restrained and high-rarity items more expressive according to `rarity-direction.md`.

## Materials

Represent materials with shorthand rather than simulation:

- Metal: one dark plane, one midtone, a few sharp highlights.
- Leather: warm block color, one soft highlight, one seam or fold.
- Cloth: two or three large folds, clear hue identity.
- Wood: broad grain marks only.
- Stone: large facets and one or two cracks.
- Bone: pale block shapes with dark joints or cavities.
- Magic: a concentrated core with controlled glow, not fog over the entire image.

## Background

Use a simple painted field, radial value shift, elemental smear, or abstract complementary shape. Add a soft painted vignette: gradually darken or desaturate the outer 10-20% of the image while keeping the subject readable. The vignette must remain irregular and painterly, not a geometric border. Do not create a room, landscape, pedestal, horizon, narrative scene, metallic frame, beveled slot edge, or hard rectangular outline.

## Originality

Use genre-level principles only. Do not copy an existing icon, named item, logo, character, exact composition, or interface frame.


## Border prohibition

- Generate artwork only.
- Never include a metallic, stone, colored, beveled, embossed, or painted inventory-slot border.
- Never leave a visible rectangular frame line around the artwork.
- Let the artwork continue naturally to every canvas edge beneath the soft vignette.
- UI borders and rarity borders belong in Unity and are not part of the generated icon artwork.
