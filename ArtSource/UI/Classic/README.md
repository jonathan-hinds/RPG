# Classic Slot Skin Source

These bitmap sources were generated with the built-in ImageGen mode. Each distinct asset was generated in its own call; no sprite atlas or contact-sheet extraction is used.

The prompt set consistently requested an entirely original, front-facing early-2000s fantasy MMORPG UI with icon-first hierarchy, near-black recessed surfaces, narrow graphite/pewter edges, restrained antique-gold joints, no logos, no copied game symbols, no thick slot ornament, and no excessive bloom. The supplied screenshots were used only for hierarchy, spacing, material, and construction reference.

Transparent assets were generated against flat `#ff00ff`, processed with the Codex image-generation skill's `remove_chroma_key.py`, then resized by `Tools/PrepareClassicSlotUiAssets.py`.

## Asset intent

- `Slot_Background_Empty`: quiet dark recessed well; opaque.
- `Slot_Frame_Normal`: one hollow neutral outer frame.
- `Slot_State_Rim`: one hollow tintable inner rim reused by semantic states and item quality.
- `Slot_Glow_Proc`: restrained square warm glow for proc or attention feedback.
- `Slot_CategorySilhouette_Default`: faint generic equipment placeholder.
- `Panel_Background_Default`: heavily subdued dark leather surface.
- `Panel_Frame_Default`: thin hollow nine-slice frame.
- `Panel_Header_Default`: inset title plate.
- `Panel_Close_Normal`: compact close control; hover/pressed use color tint.
- `BagPanel_IconMedallion`: original satchel medallion overlapping the top-left corner.
- `BagPanel_CurrencyBar`: recessed currency well for the panel footer.
- `ActionBar_Background_Center`: low-profile scalable icon rail.
- `ActionBar_EndCap`: original carved guardian bracket mirrored for the right side.

Generated originals are kept in `Generated`, alpha-cleaned intermediates in `Alpha`, and Unity-ready outputs under `Assets/Resources/RPGClone/UI/SlotFramework`.
