using System;
using System.Collections.Generic;
using System.Linq;
using RPGClone.Characters;
using RPGClone.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RPGClone.EditorTools
{
    [CustomEditor(typeof(MMONpcVisualAuthoring))]
    [CanEditMultipleObjects]
    public sealed class MMONpcVisualAuthoringEditor : UnityEditor.Editor
    {
        private static readonly Dictionary<MMOEquipmentSlotType, ArmorOption[]> ArmorOptionsBySlot = new();

        private SerializedProperty appearanceCatalog;
        private SerializedProperty hairstyleId;
        private SerializedProperty hairColorId;
        private SerializedProperty faceId;
        private SerializedProperty chestArmor;
        private SerializedProperty gloves;
        private SerializedProperty pants;
        private SerializedProperty boots;

        private void OnEnable()
        {
            appearanceCatalog = serializedObject.FindProperty("appearanceCatalog");
            hairstyleId = serializedObject.FindProperty("hairstyleId");
            hairColorId = serializedObject.FindProperty("hairColorId");
            faceId = serializedObject.FindProperty("faceId");
            chestArmor = serializedObject.FindProperty("chestArmor");
            gloves = serializedObject.FindProperty("gloves");
            pants = serializedObject.FindProperty("pants");
            boots = serializedObject.FindProperty("boots");
            EditorApplication.projectChanged += ClearArmorCache;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= ClearArmorCache;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Choose the NPC's player-compatible appearance. Hair styles and colors use the shared player catalog. Default Player Skin uses the first shared option and leaves that armor body part unequipped. NPC locomotion is locked to Idle.",
                MessageType.Info);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorGUILayout.HelpBox(
                    "NPC appearance authoring is read-only during Play Mode. Stop Play Mode before changing an outfit.",
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                EditorGUILayout.PropertyField(appearanceCatalog, new GUIContent("Appearance Catalog"));

                if (appearanceCatalog.hasMultipleDifferentValues)
                {
                    EditorGUILayout.HelpBox(
                        "Selected NPCs must use the same Appearance Catalog before hair and face can be edited together.",
                        MessageType.Warning);
                }
                else
                {
                    MMOCharacterAppearanceCatalog catalog =
                        appearanceCatalog.objectReferenceValue as MMOCharacterAppearanceCatalog;
                    DrawHairstylePopup(catalog);
                    DrawHairColorPopup(catalog);
                    DrawFacePopup(catalog);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Armor", EditorStyles.boldLabel);
                DrawArmorPopup(chestArmor, MMOEquipmentSlotType.Chest, "Chest Armor");
                DrawArmorPopup(gloves, MMOEquipmentSlotType.Hands, "Gloves");
                DrawArmorPopup(pants, MMOEquipmentSlotType.Legs, "Pants");
                DrawArmorPopup(boots, MMOEquipmentSlotType.Feet, "Boots");
            }

            bool changed = serializedObject.ApplyModifiedProperties();

            if (changed)
            {
                foreach (UnityEngine.Object selectedTarget in targets)
                {
                    if (selectedTarget is MMONpcVisualAuthoring npcVisual)
                    {
                        npcVisual.ApplySelections();
                        PrefabUtility.RecordPrefabInstancePropertyModifications(npcVisual);
                        EditorUtility.SetDirty(npcVisual);
                        if (npcVisual.gameObject.scene.IsValid() && npcVisual.gameObject.scene.isLoaded)
                        {
                            EditorSceneManager.MarkSceneDirty(npcVisual.gameObject.scene);
                        }
                    }
                }

                SceneView.RepaintAll();
            }
        }

        private void DrawHairstylePopup(MMOCharacterAppearanceCatalog catalog)
        {
            if (catalog == null || catalog.Hairstyles.Count == 0)
            {
                EditorGUILayout.PropertyField(hairstyleId, new GUIContent("Hair ID"));
                return;
            }

            string[] labels = new[] { "Default Player Skin" }
                .Concat(catalog.Hairstyles.Select(option => option?.DisplayName ?? "Missing Hair Option"))
                .ToArray();
            int currentIndex = FindHairstyleIndex(catalog, hairstyleId.stringValue) + 1;
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = hairstyleId.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Hair", currentIndex, labels);
            bool selectionChanged = EditorGUI.EndChangeCheck();
            string selectedId = selectedIndex <= 0
                ? string.Empty
                : catalog.Hairstyles[selectedIndex - 1]?.HairstyleId ?? string.Empty;
            MMONpcVisualPopupAssignment.SetStringIfChanged(hairstyleId, selectionChanged, selectedId);
            EditorGUI.showMixedValue = previousMixedValue;
        }

        private void DrawFacePopup(MMOCharacterAppearanceCatalog catalog)
        {
            if (catalog == null || catalog.Faces.Count == 0)
            {
                EditorGUILayout.PropertyField(faceId, new GUIContent("Face ID"));
                return;
            }

            string[] labels = new[] { "Default Player Skin" }
                .Concat(catalog.Faces.Select(option => option?.DisplayName ?? "Missing Face Option"))
                .ToArray();
            int currentIndex = FindFaceIndex(catalog, faceId.stringValue) + 1;
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = faceId.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Face", currentIndex, labels);
            bool selectionChanged = EditorGUI.EndChangeCheck();
            string selectedId = selectedIndex <= 0
                ? string.Empty
                : catalog.Faces[selectedIndex - 1]?.FaceId ?? string.Empty;
            MMONpcVisualPopupAssignment.SetStringIfChanged(faceId, selectionChanged, selectedId);
            EditorGUI.showMixedValue = previousMixedValue;
        }

        private void DrawHairColorPopup(MMOCharacterAppearanceCatalog catalog)
        {
            if (catalog == null || catalog.HairColors.Count == 0)
            {
                EditorGUILayout.PropertyField(hairColorId, new GUIContent("Hair Color ID"));
                return;
            }

            string[] labels = new[] { "Default Player Color" }
                .Concat(catalog.HairColors.Select(option => option?.DisplayName ?? "Missing Hair Color"))
                .ToArray();
            int currentIndex = FindHairColorIndex(catalog, hairColorId.stringValue) + 1;
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = hairColorId.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup("Hair Color", currentIndex, labels);
            bool selectionChanged = EditorGUI.EndChangeCheck();
            string selectedId = selectedIndex <= 0
                ? string.Empty
                : catalog.HairColors[selectedIndex - 1]?.HairColorId ?? string.Empty;
            MMONpcVisualPopupAssignment.SetStringIfChanged(hairColorId, selectionChanged, selectedId);
            EditorGUI.showMixedValue = previousMixedValue;
        }

        private static void DrawArmorPopup(
            SerializedProperty property,
            MMOEquipmentSlotType slot,
            string label)
        {
            ArmorOption[] options = GetArmorOptions(slot);
            int currentIndex = Array.FindIndex(options, option => option.Visual == property.objectReferenceValue);
            bool previousMixedValue = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            int selectedIndex = EditorGUILayout.Popup(
                label,
                Mathf.Max(0, currentIndex),
                options.Select(option => option.Label).ToArray());
            bool selectionChanged = EditorGUI.EndChangeCheck();
            MMONpcVisualPopupAssignment.SetObjectIfChanged(
                property,
                selectionChanged,
                options[Mathf.Max(0, selectedIndex)].Visual);
            EditorGUI.showMixedValue = previousMixedValue;
        }

        private static ArmorOption[] GetArmorOptions(MMOEquipmentSlotType slot)
        {
            if (ArmorOptionsBySlot.TryGetValue(slot, out ArmorOption[] cachedOptions))
            {
                return cachedOptions;
            }

            List<ArmorOption> options = new()
            {
                new ArmorOption("Default Player Skin", null)
            };

            foreach (string guid in AssetDatabase.FindAssets("t:MMOEquipmentVisualDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MMOEquipmentVisualDefinition visual = AssetDatabase.LoadAssetAtPath<MMOEquipmentVisualDefinition>(path);
                if (visual == null
                    || visual.EquipmentSlot != slot
                    || visual.BindingMode != MMOEquipmentVisualBindingMode.BodyPart)
                {
                    continue;
                }

                string displayName = ObjectNames.NicifyVariableName(
                    visual.name.StartsWith("EV_", StringComparison.Ordinal) ? visual.name[3..] : visual.name);
                options.Add(new ArmorOption(displayName, visual));
            }

            cachedOptions = options
                .Skip(1)
                .OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)
                .Prepend(options[0])
                .ToArray();
            ArmorOptionsBySlot[slot] = cachedOptions;
            return cachedOptions;
        }

        private static int FindHairstyleIndex(MMOCharacterAppearanceCatalog catalog, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return -1;
            }

            for (int i = 0; i < catalog.Hairstyles.Count; i++)
            {
                if (catalog.Hairstyles[i] != null
                    && string.Equals(catalog.Hairstyles[i].HairstyleId, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindFaceIndex(MMOCharacterAppearanceCatalog catalog, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return -1;
            }

            for (int i = 0; i < catalog.Faces.Count; i++)
            {
                if (catalog.Faces[i] != null
                    && string.Equals(catalog.Faces[i].FaceId, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindHairColorIndex(MMOCharacterAppearanceCatalog catalog, string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return -1;
            }

            for (int i = 0; i < catalog.HairColors.Count; i++)
            {
                if (catalog.HairColors[i] != null
                    && string.Equals(catalog.HairColors[i].HairColorId, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void ClearArmorCache()
        {
            ArmorOptionsBySlot.Clear();
        }

        private readonly struct ArmorOption
        {
            public ArmorOption(string label, MMOEquipmentVisualDefinition visual)
            {
                Label = label;
                Visual = visual;
            }

            public string Label { get; }
            public MMOEquipmentVisualDefinition Visual { get; }
        }
    }

    internal static class MMONpcVisualPopupAssignment
    {
        public static void SetStringIfChanged(SerializedProperty property, bool changed, string value)
        {
            if (changed && property != null)
            {
                property.stringValue = value ?? string.Empty;
            }
        }

        public static void SetObjectIfChanged(
            SerializedProperty property,
            bool changed,
            UnityEngine.Object value)
        {
            if (changed && property != null)
            {
                property.objectReferenceValue = value;
            }
        }
    }
}
