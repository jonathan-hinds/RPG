using System;
using RPGClone.World.Foliage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TerrainTools;
using UnityEngine;

namespace RPGClone.EditorTools
{
    [CustomEditor(typeof(MMOTerrainDetailSlopeFilter))]
    public sealed class MMOTerrainDetailSlopeFilterEditor : Editor
    {
        private const string ProfileTypeFilter = "t:MMOClassicGrassFoliageProfile";

        private SerializedProperty foliageProfileProperty;
        private SerializedProperty detailThinningPrototypeIndexProperty;
        private SerializedProperty detailThinningRemovalPercentageProperty;
        private SerializedProperty detailThinningRandomSeedProperty;
        private string operationResult;
        private MessageType operationResultType = MessageType.Info;
        private TerrainData loadedScaleTerrainData;
        private int selectedDetailIndex;
        private int loadedScaleDetailIndex = -1;
        private float detailMinWidth;
        private float detailMaxWidth;
        private float detailMinHeight;
        private float detailMaxHeight;

        private void OnEnable()
        {
            foliageProfileProperty = serializedObject.FindProperty("foliageProfile");
            detailThinningPrototypeIndexProperty = serializedObject.FindProperty("detailThinningPrototypeIndex");
            detailThinningRemovalPercentageProperty = serializedObject.FindProperty("detailThinningRemovalPercentage");
            detailThinningRandomSeedProperty = serializedObject.FindProperty("detailThinningRandomSeed");
            AssignDefaultProfileIfMissing();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.HelpBox(
                "Paint normally with Unity's built-in Paint Details tool. Press the button below afterward to remove grass and bushes from Terrain cells steeper than the configured limit.",
                MessageType.Info);

            EditorGUILayout.PropertyField(
                foliageProfileProperty,
                new GUIContent("Foliage Profile", "The shared profile that owns the maximum allowed slope."));
            serializedObject.ApplyModifiedProperties();

            MMOTerrainDetailSlopeFilter slopeFilter = (MMOTerrainDetailSlopeFilter)target;
            DrawMaximumSlopeSetting(slopeFilter.FoliageProfile);
            DrawIndividualDetailScale(slopeFilter);
            DrawRandomDetailThinning(slopeFilter);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!CanProcess(slopeFilter)))
            {
                if (GUILayout.Button("Remove Disallowed Foliage", GUILayout.Height(36f)))
                {
                    RemoveDisallowedFoliage(slopeFilter);
                }

                if (GUILayout.Button("Validate Foliage Slope Rules"))
                {
                    ValidateFoliage(slopeFilter);
                }
            }

            if (!string.IsNullOrEmpty(operationResult))
            {
                EditorGUILayout.HelpBox(operationResult, operationResultType);
            }
        }

        [MenuItem("Tools/RPG Clone/Foliage/Add Slope Filter Component To Active Terrain")]
        public static void AddToActiveTerrain()
        {
            Terrain terrain = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<Terrain>()
                : null;
            terrain = terrain != null ? terrain : Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("Select a Terrain before adding the foliage slope filter component.");
                return;
            }

            MMOTerrainDetailSlopeFilter slopeFilter = terrain.GetComponent<MMOTerrainDetailSlopeFilter>();
            if (slopeFilter == null)
            {
                slopeFilter = Undo.AddComponent<MMOTerrainDetailSlopeFilter>(terrain.gameObject);
            }

            MMOClassicGrassFoliageProfile profile = FindDefaultProfile();
            if (slopeFilter.FoliageProfile == null && profile != null)
            {
                Undo.RecordObject(slopeFilter, "Assign Terrain Foliage Profile");
                slopeFilter.Configure(profile);
                EditorUtility.SetDirty(slopeFilter);
            }

            Selection.activeGameObject = terrain.gameObject;
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            Debug.Log($"Added {nameof(MMOTerrainDetailSlopeFilter)} to {terrain.name}. Use Remove Disallowed Foliage in the component Inspector after painting details with Unity's built-in tool.");
        }

        private void AssignDefaultProfileIfMissing()
        {
            serializedObject.Update();
            if (foliageProfileProperty.objectReferenceValue != null)
            {
                return;
            }

            MMOClassicGrassFoliageProfile profile = FindDefaultProfile();
            if (profile == null)
            {
                return;
            }

            foliageProfileProperty.objectReferenceValue = profile;
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawMaximumSlopeSetting(MMOClassicGrassFoliageProfile profile)
        {
            if (profile == null)
            {
                EditorGUILayout.HelpBox("Assign a foliage profile to enable slope cleanup.", MessageType.Warning);
                return;
            }

            SerializedObject profileObject = new(profile);
            profileObject.Update();
            SerializedProperty maximumSlopeProperty = profileObject.FindProperty("maximumDetailSlopeDegrees");
            EditorGUILayout.PropertyField(
                maximumSlopeProperty,
                new GUIContent("Maximum Allowed Slope", "Foliage above this unsigned slope angle is removed. Inclines and declines are treated identically."));
            profileObject.ApplyModifiedProperties();
        }

        private void DrawIndividualDetailScale(MMOTerrainDetailSlopeFilter slopeFilter)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Individual Detail Scale", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Choose one Terrain detail type, set its random width and height range, then apply it. This changes only that detail prototype; it does not repaint, remove, or modify any detail density maps.",
                MessageType.Info);

            Terrain terrain = slopeFilter != null ? slopeFilter.GetComponent<Terrain>() : null;
            TerrainData terrainData = terrain != null ? terrain.terrainData : null;
            DetailPrototype[] prototypes = terrainData != null ? terrainData.detailPrototypes : null;
            if (prototypes == null || prototypes.Length == 0)
            {
                EditorGUILayout.HelpBox("This Terrain does not have any detail prototypes to configure.", MessageType.Warning);
                return;
            }

            string[] detailNames = BuildDetailNames(prototypes, slopeFilter.FoliageProfile);
            selectedDetailIndex = Mathf.Clamp(selectedDetailIndex, 0, prototypes.Length - 1);
            int newDetailIndex = EditorGUILayout.Popup(
                new GUIContent("Detail To Configure", "The grass or bush prototype whose size range will be changed."),
                selectedDetailIndex,
                detailNames);
            if (newDetailIndex != selectedDetailIndex)
            {
                selectedDetailIndex = newDetailIndex;
                loadedScaleDetailIndex = -1;
            }

            LoadDetailScaleIfNeeded(terrainData, prototypes[selectedDetailIndex]);

            detailMinWidth = EditorGUILayout.FloatField(
                new GUIContent("Minimum Width", "Smallest X/Z footprint for this detail type."),
                detailMinWidth);
            detailMaxWidth = EditorGUILayout.FloatField(
                new GUIContent("Maximum Width", "Largest X/Z footprint for this detail type."),
                detailMaxWidth);
            detailMinHeight = EditorGUILayout.FloatField(
                new GUIContent("Minimum Height", "Smallest vertical size for this detail type."),
                detailMinHeight);
            detailMaxHeight = EditorGUILayout.FloatField(
                new GUIContent("Maximum Height", "Largest vertical size for this detail type."),
                detailMaxHeight);

            EditorGUILayout.HelpBox(
                "Applying this also resizes any already-painted instances of the selected type. Other types, including your existing grass, are unaffected.",
                MessageType.None);

            if (GUILayout.Button($"Apply {detailNames[selectedDetailIndex]} Scale", GUILayout.Height(30f)))
            {
                ApplySelectedDetailScale(slopeFilter, terrain, terrainData, prototypes, detailNames[selectedDetailIndex]);
            }
        }

        private void DrawRandomDetailThinning(MMOTerrainDetailSlopeFilter slopeFilter)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Random Detail Thinning", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Choose one Terrain detail type and remove an exact percentage of its painted instances at random. Other detail types are not changed. Nothing happens until you press the thinning button.",
                MessageType.Info);

            Terrain terrain = slopeFilter != null ? slopeFilter.GetComponent<Terrain>() : null;
            TerrainData terrainData = terrain != null ? terrain.terrainData : null;
            DetailPrototype[] prototypes = terrainData != null ? terrainData.detailPrototypes : null;
            if (prototypes == null || prototypes.Length == 0)
            {
                EditorGUILayout.HelpBox("This Terrain does not have any detail prototypes to thin.", MessageType.Warning);
                return;
            }

            string[] detailNames = BuildDetailNames(prototypes, slopeFilter.FoliageProfile);
            int selectedIndex = Mathf.Clamp(
                detailThinningPrototypeIndexProperty.intValue,
                0,
                prototypes.Length - 1);
            detailThinningPrototypeIndexProperty.intValue = EditorGUILayout.Popup(
                new GUIContent("Detail To Thin", "Only this grass or bush detail layer will be modified."),
                selectedIndex,
                detailNames);

            detailThinningRemovalPercentageProperty.floatValue = EditorGUILayout.Slider(
                new GUIContent("Removal Percentage", "Exact rounded percentage of the selected detail instances to remove."),
                detailThinningRemovalPercentageProperty.floatValue,
                0f,
                100f);
            EditorGUILayout.PropertyField(
                detailThinningRandomSeedProperty,
                new GUIContent("Random Seed", "The same terrain data, percentage, and seed produce the same random distribution."));

            serializedObject.ApplyModifiedProperties();

            float removalPercentage = detailThinningRemovalPercentageProperty.floatValue;
            int detailIndex = detailThinningPrototypeIndexProperty.intValue;
            using (new EditorGUI.DisabledScope(removalPercentage <= 0f))
            {
                if (GUILayout.Button(
                        $"Randomly Remove {removalPercentage:0.##}% of {detailNames[detailIndex]}",
                        GUILayout.Height(34f)))
                {
                    ThinSelectedDetail(
                        slopeFilter,
                        terrain,
                        terrainData,
                        detailIndex,
                        detailNames[detailIndex],
                        removalPercentage,
                        detailThinningRandomSeedProperty.intValue);
                }
            }

            EditorGUILayout.HelpBox(
                "Each run is recorded as one Unity Undo operation. Use Ctrl+Z or Edit > Undo immediately after a run to restore every removed instance, adjust the percentage, and try again.",
                MessageType.None);
        }

        private void ThinSelectedDetail(
            MMOTerrainDetailSlopeFilter slopeFilter,
            Terrain terrain,
            TerrainData terrainData,
            int detailIndex,
            string detailName,
            float removalPercentage,
            int randomSeed)
        {
            string undoName = $"Thin Terrain Detail {detailName}";
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            TerrainPaintUtilityEditor.UpdateTerrainDataUndo(terrainData, undoName);

            long removedInstances = MMOTerrainDetailSlopeProcessor.RemoveRandomPercentage(
                terrainData,
                detailIndex,
                removalPercentage,
                randomSeed);

            Undo.CollapseUndoOperations(undoGroup);
            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssetIfDirty(terrainData);
            terrain.Flush();
            SceneView.RepaintAll();

            operationResult = $"Randomly removed {removedInstances:N0} instances ({removalPercentage:0.##}%) of {detailName}. Use Ctrl+Z to undo this run.";
            operationResultType = MessageType.Info;
            Debug.Log($"{operationResult} Terrain: {terrain.name}. Seed: {randomSeed}.", slopeFilter);
        }

        private void LoadDetailScaleIfNeeded(TerrainData terrainData, DetailPrototype prototype)
        {
            if (loadedScaleTerrainData == terrainData && loadedScaleDetailIndex == selectedDetailIndex)
            {
                return;
            }

            loadedScaleTerrainData = terrainData;
            loadedScaleDetailIndex = selectedDetailIndex;
            detailMinWidth = prototype.minWidth;
            detailMaxWidth = prototype.maxWidth;
            detailMinHeight = prototype.minHeight;
            detailMaxHeight = prototype.maxHeight;
        }

        private void ApplySelectedDetailScale(
            MMOTerrainDetailSlopeFilter slopeFilter,
            Terrain terrain,
            TerrainData terrainData,
            DetailPrototype[] prototypes,
            string detailName)
        {
            detailMinWidth = Mathf.Max(0.01f, detailMinWidth);
            detailMaxWidth = Mathf.Max(detailMinWidth, detailMaxWidth);
            detailMinHeight = Mathf.Max(0.01f, detailMinHeight);
            detailMaxHeight = Mathf.Max(detailMinHeight, detailMaxHeight);

            TerrainPaintUtilityEditor.UpdateTerrainDataUndo(terrainData, $"Resize Terrain Detail {detailName}");
            DetailPrototype updatedPrototype = new(prototypes[selectedDetailIndex])
            {
                minWidth = detailMinWidth,
                maxWidth = detailMaxWidth,
                minHeight = detailMinHeight,
                maxHeight = detailMaxHeight
            };
            prototypes[selectedDetailIndex] = updatedPrototype;
            terrainData.detailPrototypes = prototypes;
            terrainData.RefreshPrototypes();

            bool profileUpdated = UpdateMatchingProfileVariation(
                slopeFilter.FoliageProfile,
                updatedPrototype,
                detailMinWidth,
                detailMaxWidth,
                detailMinHeight,
                detailMaxHeight);

            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssetIfDirty(terrainData);
            terrain.Flush();
            SceneView.RepaintAll();

            operationResult = $"Applied a width range of {detailMinWidth:0.##}-{detailMaxWidth:0.##} and height range of {detailMinHeight:0.##}-{detailMaxHeight:0.##} to {detailName}. Painted density maps and other detail types were not changed.";
            if (!profileUpdated && slopeFilter.FoliageProfile != null)
            {
                operationResult += " No matching profile variation was found, so only this Terrain's prototype was updated.";
            }

            operationResultType = profileUpdated || slopeFilter.FoliageProfile == null
                ? MessageType.Info
                : MessageType.Warning;
            Debug.Log(operationResult, slopeFilter);
        }

        private static bool UpdateMatchingProfileVariation(
            MMOClassicGrassFoliageProfile profile,
            DetailPrototype prototype,
            float minWidth,
            float maxWidth,
            float minHeight,
            float maxHeight)
        {
            int variationIndex = FindMatchingVariationIndex(profile, prototype);
            if (variationIndex < 0)
            {
                return false;
            }

            Undo.RecordObject(profile, "Update Foliage Detail Scale Profile");
            MMOClassicGrassFoliageVariation variation = profile.variations[variationIndex];
            variation.minWidth = minWidth;
            variation.maxWidth = maxWidth;
            variation.minHeight = minHeight;
            variation.maxHeight = maxHeight;
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            return true;
        }

        private static string[] BuildDetailNames(
            DetailPrototype[] prototypes,
            MMOClassicGrassFoliageProfile profile)
        {
            string[] names = new string[prototypes.Length];
            for (int index = 0; index < prototypes.Length; index++)
            {
                DetailPrototype prototype = prototypes[index];
                int variationIndex = FindMatchingVariationIndex(profile, prototype);
                string profileName = variationIndex >= 0
                    ? profile.variations[variationIndex].displayName
                    : null;
                string assetName = prototype.prototype != null
                    ? prototype.prototype.name
                    : prototype.prototypeTexture != null
                        ? prototype.prototypeTexture.name
                        : null;
                names[index] = !string.IsNullOrWhiteSpace(profileName)
                    ? profileName
                    : !string.IsNullOrWhiteSpace(assetName)
                        ? assetName
                        : $"Detail {index + 1}";
            }

            return names;
        }

        private static int FindMatchingVariationIndex(
            MMOClassicGrassFoliageProfile profile,
            DetailPrototype prototype)
        {
            if (profile == null || profile.variations == null)
            {
                return -1;
            }

            for (int index = 0; index < profile.variations.Count; index++)
            {
                MMOClassicGrassFoliageVariation variation = profile.variations[index];
                if (variation == null)
                {
                    continue;
                }

                bool sameModel = prototype.prototype != null &&
                                 variation.modelPrefab == prototype.prototype;
                bool sameTexture = prototype.prototypeTexture != null &&
                                   variation.texture == prototype.prototypeTexture;
                if (sameModel || sameTexture)
                {
                    return index;
                }
            }

            return -1;
        }

        private void RemoveDisallowedFoliage(MMOTerrainDetailSlopeFilter slopeFilter)
        {
            Terrain terrain = slopeFilter.GetComponent<Terrain>();
            TerrainData terrainData = terrain.terrainData;
            float maximumSlope = slopeFilter.MaximumAllowedSlopeDegrees;

            TerrainPaintUtilityEditor.UpdateTerrainDataUndo(terrainData, "Remove Disallowed Terrain Foliage");
            int removedInstances = MMOTerrainDetailSlopeProcessor.RemoveDisallowedDetails(
                terrainData,
                maximumSlope);

            EditorUtility.SetDirty(terrainData);
            AssetDatabase.SaveAssetIfDirty(terrainData);
            SceneView.RepaintAll();

            operationResult = $"Removed {removedInstances:N0} foliage instances above {maximumSlope:0.#} degrees.";
            operationResultType = MessageType.Info;
            Debug.Log($"{operationResult} Terrain: {terrain.name}.", slopeFilter);
        }

        private void ValidateFoliage(MMOTerrainDetailSlopeFilter slopeFilter)
        {
            Terrain terrain = slopeFilter.GetComponent<Terrain>();
            float maximumSlope = slopeFilter.MaximumAllowedSlopeDegrees;
            int disallowedInstances = MMOTerrainDetailSlopeProcessor.CountDisallowedDetails(
                terrain.terrainData,
                maximumSlope);

            bool passed = disallowedInstances == 0;
            operationResult = passed
                ? $"Validation passed. No foliage remains above {maximumSlope:0.#} degrees."
                : $"Validation found {disallowedInstances:N0} foliage instances above {maximumSlope:0.#} degrees.";
            operationResultType = passed ? MessageType.Info : MessageType.Warning;

            if (passed)
            {
                Debug.Log(operationResult, slopeFilter);
            }
            else
            {
                Debug.LogWarning(operationResult, slopeFilter);
            }
        }

        private static bool CanProcess(MMOTerrainDetailSlopeFilter slopeFilter)
        {
            if (slopeFilter == null || slopeFilter.FoliageProfile == null)
            {
                return false;
            }

            Terrain terrain = slopeFilter.GetComponent<Terrain>();
            return terrain != null && terrain.terrainData != null;
        }

        private static MMOClassicGrassFoliageProfile FindDefaultProfile()
        {
            string[] guids = AssetDatabase.FindAssets(ProfileTypeFilter);
            if (guids.Length == 0)
            {
                return null;
            }

            Array.Sort(guids, StringComparer.Ordinal);
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<MMOClassicGrassFoliageProfile>(path);
        }
    }

    public static class MMOTerrainDetailSlopeProcessor
    {
        public static long RemoveRandomPercentage(
            TerrainData terrainData,
            int detailPrototypeIndex,
            float removalPercentage,
            int randomSeed)
        {
            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            int prototypeCount = terrainData.detailPrototypes.Length;
            if (detailPrototypeIndex < 0 || detailPrototypeIndex >= prototypeCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(detailPrototypeIndex),
                    detailPrototypeIndex,
                    $"Detail prototype index must be between 0 and {prototypeCount - 1}.");
            }

            float clampedPercentage = Mathf.Clamp(removalPercentage, 0f, 100f);
            if (clampedPercentage <= 0f)
            {
                return 0L;
            }

            int width = terrainData.detailWidth;
            int height = terrainData.detailHeight;
            int[,] details = terrainData.GetDetailLayer(
                0,
                0,
                width,
                height,
                detailPrototypeIndex);

            long totalInstances = 0L;
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    totalInstances += details[z, x];
                }
            }

            long targetRemovalCount = (long)Math.Round(
                totalInstances * (clampedPercentage / 100d),
                MidpointRounding.AwayFromZero);
            targetRemovalCount = Math.Max(0L, Math.Min(totalInstances, targetRemovalCount));
            if (targetRemovalCount == 0L)
            {
                return 0L;
            }

            System.Random random = new(randomSeed);
            long remainingInstances = totalInstances;
            long remainingRemovals = targetRemovalCount;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    int density = details[z, x];
                    int removalsFromCell = 0;
                    for (int instanceIndex = 0; instanceIndex < density; instanceIndex++)
                    {
                        bool remove = remainingRemovals >= remainingInstances ||
                                      remainingRemovals > 0L &&
                                      random.NextDouble() < (double)remainingRemovals / remainingInstances;
                        if (remove)
                        {
                            removalsFromCell++;
                            remainingRemovals--;
                        }

                        remainingInstances--;
                    }

                    details[z, x] = density - removalsFromCell;
                }
            }

            terrainData.SetDetailLayer(0, 0, detailPrototypeIndex, details);
            return targetRemovalCount;
        }

        public static int RemoveDisallowedDetails(TerrainData terrainData, float maximumSlopeDegrees)
        {
            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            int width = terrainData.detailWidth;
            int height = terrainData.detailHeight;
            bool[,] disallowedCells = BuildDisallowedCellMap(terrainData, maximumSlopeDegrees);
            int removedInstances = 0;

            for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
            {
                int[,] details = terrainData.GetDetailLayer(0, 0, width, height, layer);
                for (int z = 0; z < height; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!disallowedCells[z, x])
                        {
                            continue;
                        }

                        removedInstances += details[z, x];
                        details[z, x] = 0;
                    }
                }

                terrainData.SetDetailLayer(0, 0, layer, details);
            }

            return removedInstances;
        }

        public static int CountDisallowedDetails(TerrainData terrainData, float maximumSlopeDegrees)
        {
            if (terrainData == null)
            {
                throw new ArgumentNullException(nameof(terrainData));
            }

            int width = terrainData.detailWidth;
            int height = terrainData.detailHeight;
            bool[,] disallowedCells = BuildDisallowedCellMap(terrainData, maximumSlopeDegrees);
            int disallowedInstances = 0;

            for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
            {
                int[,] details = terrainData.GetDetailLayer(0, 0, width, height, layer);
                for (int z = 0; z < height; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (disallowedCells[z, x])
                        {
                            disallowedInstances += details[z, x];
                        }
                    }
                }
            }

            return disallowedInstances;
        }

        private static bool[,] BuildDisallowedCellMap(TerrainData terrainData, float maximumSlopeDegrees)
        {
            int width = terrainData.detailWidth;
            int height = terrainData.detailHeight;
            bool[,] disallowedCells = new bool[height, width];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float normalizedX = (x + 0.5f) / width;
                    float normalizedZ = (z + 0.5f) / height;
                    disallowedCells[z, x] = MMOTerrainDetailSlopePolicy.EvaluateDensityMultiplier(
                        terrainData,
                        normalizedX,
                        normalizedZ,
                        maximumSlopeDegrees,
                        0f) <= 0f;
                }
            }

            return disallowedCells;
        }
    }
}
