# Item Icon Upload Workflow

Place square item icon source files in this folder, then assign them on the item data object:

1. Import a square `PNG`, `PSD`, or `TGA` into `Assets/_Project/Art/Items/Icons`.
2. Select the texture in Unity and set `Texture Type` to `Sprite (2D and UI)`.
3. Use `Sprite Mode: Single`, enable alpha/transparency for icons with cutouts, and keep the source square such as `128x128`, `256x256`, or `512x512`.
4. Open the item asset in `Assets/_Project/Configs/Items`.
5. Drag the imported sprite into the item's `Presentation > Icon` field.

The same `Icon` field drives inventory slots, loot rows, quest rewards, vendor stock, equipment slots, and action bar item bindings. Update the sprite on the item asset once and every UI surface will pick it up.
