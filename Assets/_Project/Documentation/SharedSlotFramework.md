# Shared Slot Framework

## Purpose

`MMOSlotView` is a content-neutral uGUI presentation component for square game-content slots. It owns only reusable visual state. Inventory, equipment, action-bar, quest, loot, vendor, trainer, and ability systems continue to own gameplay rules and data.

The authored prefab is available at:

`Assets/Resources/RPGClone/UI/SlotFramework/SharedSlot.prefab`

Runtime-built interfaces may use `MMOSlotView.Attach(gameObject)` instead of instantiating the prefab. Both paths build the same hierarchy.

## Presentation

Create an `MMOSlotPresentation` through a content adapter and pass it to `MMOSlotView.Present`.

- `MMOItemSlotAdapter` maps item icon, quantity, quality tint, restriction state, and fallback text.
- `MMOAbilitySlotAdapter` maps ability icon, keybinding, active state, usability, range, attention, proc, and cooldown state.
- Future content types should add their own small adapter. They should not add gameplay-type checks to `MMOSlotView`.

Every major state is a separate child image. `SetLayerSprite` and `SetLayerTint` support per-view replacement. Replacing a sprite directly on an authored prefab layer is also supported because the runtime assigns layout and artwork defaults only when it creates a missing layer.

The default construction follows the Classic UI's functional separation:

- an inset embossed well remains visible when a slot is empty
- a neutral outer frame remains visible in every state
- one tintable inner state rim represents hover, pressed, selected, active, or drop validation
- the item-quality rim is visible only when no interaction state has priority
- cooldown is an independent dark radial sweep over the icon
- proc and attention feedback share one square glow role and never stack

Drop validation has highest priority, followed by pressed, active, selected, and hover. Exactly one semantic state rim can be visible at a time. This avoids the overlapping full-frame textures that made earlier iterations look thick and misaligned.

## Dragging and dropping

`MMOSlotDragPayload` carries a neutral category, stable Unity content reference, source context, source index, quantity, and requested operation. `MMOSlotDragState` owns the only cursor-carried visual and never removes source content.

Destinations implement `IMMOSlotDropTarget` for broad visual validation. Their existing drop callback still invokes the authoritative gameplay method:

- Inventory: `MMOInventoryContainer.TryMoveSlot`
- Equipment: `MMOCharacterEquipment.TryEquipFromInventory` / `TryUnequipToInventory`
- Action bar: `MMOActionBarPresenter.AcceptDrop`

Ending or cancelling a drag only clears presentation state. Failed mutations therefore leave the source model unchanged.

## Current consumers

- Inventory grid and bag shell
- Equipment slots
- Action bar
- Spellbook ability entries
- Loot rows
- Merchant stock
- Quest objective items and reward choices
- Class-trainer ability offers

Bank, shared storage, trade, crafting ingredient, talent, macro, pet, mount, and context-menu systems were not found and were not invented.

## Artwork

The thirteen default original artwork files are stored under `Assets/Resources/RPGClone/UI/SlotFramework`. Each source image was generated separately; the skin does not use an atlas. Generated and alpha-cleaned sources are retained under `ArtSource/UI/Classic`.

Run `python Tools/PrepareClassicSlotUiAssets.py` from the project root after replacing an individual source image, then run:

`Tools > RPG Clone > UI > Rebuild Shared Slot Assets`

The editor command configures the PNGs as sprites, applies nine-slice borders, and rebuilds the shared prefab.

The deliberately small physical asset set is:

- one recessed empty-slot well
- one neutral outer slot frame
- one tintable semantic-state and item-quality rim
- one restrained square proc glow
- one optional category silhouette
- one quiet panel surface
- one thin nine-sliced panel frame
- one inset title header
- one close button
- one overlapping bag medallion
- one recessed bag currency well
- one scalable action-bar center rail

The action-bar rail skins the existing `Bottom HUD` footprint (1080 x 96), not
the smaller `Action Bar` content child (642 x 58). The content child remains
transparent and owns only the twelve buttons. Decorative end caps are not used.
The bag similarly skins its existing 300 x 364 root for the standard
sixteen-slot inventory. A fully runtime-generated bag can expand for additional
rows, but an authored prefab keeps its saved root size. Panel and rail frames are nine-sliced so
corners and rail thickness remain stable while the center region absorbs the
size difference.

## Editable HUD prefabs

The scene uses these editable prefabs:

- `Assets/Resources/RPGClone/UI/Hud/BottomHUD.prefab`
- `Assets/Resources/RPGClone/UI/Hud/InventoryPanel.prefab`

Open either asset in Prefab Mode to adjust its layout. `HUD Background Art` and
`Bag Panel Background Art` are separate child Images, so their position and
size can be changed independently from the functional panel roots. The default
HUD background is shifted 32 pixels left. The default bag background and frame
extend 14 pixels beyond each side of the 300 x 364 interaction footprint.
The inventory prefab is active so it remains visible while authoring; its scene
instance has an explicit inactive override so the bag still starts closed.

These prefab assets are the visual and layout source of truth. The presenters
bind gameplay references, button actions, item icons, text, and temporary slot
states, but they do not restyle or reposition existing authored objects. They
create and position an element only when a required functional element is
missing. Moving or resizing the background, frame, title, close button,
currency well, action slots, inventory slots, menu buttons, or individual slot
layers in Prefab Mode therefore survives runtime initialization and refreshes.

Use `Tools > RPG Clone > UI > Sync HUD Scene Instances From Prefabs` if the
active scene contains an older HUD instance. This command replaces only the
scene instances with fresh instances of the current prefab assets, retains
gameplay references and action-slot bindings, and does not save or rebuild the
prefab assets.

`Tools > RPG Clone > UI > Rebuild Editable HUD Prefabs` intentionally replaces
both prefab layouts with their defaults and reconnects the active scene. Do not
run it after making manual prefab adjustments unless resetting those
adjustments is the goal.
