# UI Window Authoring Standards

## Source Of Truth

Window prefab assets are the source of truth for authored UI layout. Runtime presenters may bind data, set text, wire button actions, and populate dynamic lists, but they must not move, resize, re-anchor, or restyle prefab-authored window elements.

Author window layout in these prefabs:

- `Assets/Resources/RPGClone/UI/Windows/GenericWindow.prefab`
- `Assets/Resources/RPGClone/UI/Windows/QuestWindow.prefab`
- `Assets/Resources/RPGClone/UI/Windows/MerchantWindow.prefab`
- `Assets/Resources/RPGClone/UI/Windows/TrainingWindow.prefab`
- `Assets/Resources/RPGClone/UI/Windows/CharacterWindow.prefab`
- `Assets/Resources/RPGClone/UI/Windows/SpellbookWindow.prefab`
- `Assets/Resources/RPGClone/UI/Windows/QuestLogWindow.prefab`

## Window Placement And Size

All standard MMO windows must keep the same root size, root scale, and close button pattern established by `GenericWindow` and `QuestWindow`.

- Standard window size comes from `MMOStandardWindow.WindowSize`.
- Standard window scale comes from `MMOStandardWindow.DefaultWindowScale`.
- Do not add per-window size multipliers, scale factors, or script-driven resizing.
- Do not let presenters or scene installers move prefab-backed windows at runtime.
- NPC windows such as quest dialog, gossip/dialog, vendor, and trainer open at the primary position: top-left anchored, slightly indented under the player raid/unit frame.
- Primary position is `MMOStandardWindow.PrimaryWindowPosition`.
- Only explicitly designated side-by-side windows may use the secondary position.
- Secondary position is `MMOStandardWindow.SecondaryWindowPosition`, the same height as the primary position but moved right enough to sit beside it with a small gap.
- Character currently uses the secondary position. Spellbook and Quest Log use the primary position unless a future design explicitly assigns them elsewhere.

If a setup script, installer, or presenter needs to rebuild a standard window, it must instantiate the matching prefab through `MMOWindowPrefabResolver` and preserve the prefab-authored root placement, root size, anchors, and child element positions.

## Required Window Pattern

Each authored window prefab should have:

- A root object with `RectTransform`, `Image`, and `MMOStandardWindow`.
- A `Title` text object.
- A `Close Button`.
- A `Content` RectTransform assigned to `MMOStandardWindow.contentRoot`.
- Optional named authoring anchors for presenter-owned content.

Examples of named authoring anchors:

- Quest window: `Dynamic Content`, `Goodbye Button`, `Accept Button`, `Decline Button`, `Back Button`, `Complete Button`
- Merchant window: `Stock`, `Status`, `Money`, `Previous Button`, `Next Button`, `Page`
- Training window: `Lessons`, `Status`, `Money`, `Train Button`
- Character window: `Paper Doll`, `Portrait`, `Name`, `Level`, `Stats`, `Left Slots`, `Right Slots`, `Bottom Slots`
- Spellbook window: `Ability Grid`, `Empty`
- Quest Log window: `Rows`

## Presenter Rules

Presenters should find named prefab elements first. If an element exists, use it as authored. Only create and position fallback elements when the prefab is missing that element.

Allowed presenter behavior:

- Set labels and dynamic text.
- Set button interactable state.
- Add or replace click listeners.
- Clear and rebuild children inside dynamic containers such as `Stock`, `Lessons`, or `Dynamic Content`.

Avoid presenter behavior:

- Do not call `anchoredPosition`, `sizeDelta`, `anchorMin`, `anchorMax`, `pivot`, `offsetMin`, or `offsetMax` on elements found in the prefab.
- Do not destroy prefab-authored window chrome or action buttons.
- Do not use scene placeholders as permanent window definitions.

## Installer Rules

Scene installers must instantiate the matching prefab through `MMOWindowPrefabResolver`. If an old generated placeholder exists, replace it with the prefab-backed instance.

Do not make installer scripts create custom versions of standard windows. Installers should connect scene references and presenters, not author the window layout.
