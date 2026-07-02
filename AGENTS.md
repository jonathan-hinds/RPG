# Project Safety Instructions

This Unity project treats Git as the source of truth. Follow these rules for all automated work.

## Git

- Do not run destructive Git commands such as `git reset`, `git checkout --`, `git clean`, `git revert`, or branch switching unless the user explicitly asks for that exact operation.
- Do not commit, stage, or push unless the user explicitly asks.
- Before and after scene or serialized asset changes, run `Tools/VerifyUnityGitSource.ps1`.

## Unity Serialization

- Keep `ProjectSettings/EditorSettings.asset` set to Force Text serialization (`m_SerializationMode: 2`).
- Keep `.gitattributes` from treating Unity scene and serialized asset files as newline-normalized text. At minimum, `*.unity` and binary-prone `*.asset` files must have `text` unset.
- Do not copy files from `Temp/__Backupscenes`, Unity recovery folders, or binary scene blobs directly over `Assets/Scenes/*.unity`.
- If recovering a scene from a temp or backup file, copy it into `Assets/_Recovery`, import/open it in Unity, then save it through Unity so the final scene under `Assets/Scenes` is valid YAML beginning with `%YAML 1.1`.
- Do not hand-edit large Unity YAML files except for tightly scoped, reviewable repairs such as replacing a known bad GUID with a verified asset GUID.

## Scene Validation

- After restoring or editing a scene, verify the target scene opens in Unity and has nonzero root objects.
- Check the Unity console/editor log for `Invalid serialized file header`, `Problem detected while opening the Scene file`, and missing prefab GUID errors.
- Preserve scene `.meta` GUIDs when replacing scene contents, especially build-setting scenes.
