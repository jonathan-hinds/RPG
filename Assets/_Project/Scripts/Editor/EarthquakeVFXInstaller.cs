using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Shaman;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class EarthquakeVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/Earthquake";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialFolder = Root + "/Materials";
        private const string MeshFolder = Root + "/Meshes";
        private const string ProfileFolder = Root + "/Profiles";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/EarthquakeVFX_Default.asset";
        private const string PrefabPath = PrefabFolder + "/EarthquakeVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Earthquake_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Earthquake.asset";
        private const string ChargeMaterialFolder = "Assets/_Project/VFX/Charge/Materials";
        private const string LayeredShaderName = "RPG Clone/VFX/Earthquake Layered Unlit";
        private const string SurfaceShaderName = "RPG Clone/VFX/Earthquake Ground Surface";
        private const int GroundChunkPoolSize = 30;
        private const int TargetReactionPoolSize = 12;

        [MenuItem("Tools/RPG Clone/VFX/Build Earthquake VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            ConfigureTextureImporters();
            EarthquakeVFXProfile profile = CreateProfile();
            Dictionary<string, Mesh> meshes = CreateMeshes();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject prefab = CreatePrefab(profile, meshes, materials);
            WireDefinition(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Selection.activeObject = prefab;
            Debug.Log("Built Earthquake VFX: terrain-aware ground chunks, layered ground-hugging rings, pooled target reactions, and replicated ability presentation are ready.", prefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Earthquake VFX")]
        public static void Validate()
        {
            EarthquakeVFXProfile profile = AssetDatabase.LoadAssetAtPath<EarthquakeVFXProfile>(ProfilePath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (profile == null || prefab == null || definition == null || ability == null)
                throw new MissingReferenceException("Earthquake profile, prefab, ability, or VFX definition is missing. Run Build Earthquake VFX.");
            if (Mathf.Abs(profile.Radius - 6f) > 0.01f)
                throw new UnityException("Earthquake's production profile must remain aligned with the six-unit gameplay radius.");
            if (prefab.GetComponent<EarthquakeVFX>() == null || prefab.GetComponent<MMOAbilityVfxPoolable>() == null)
                throw new MissingComponentException("Earthquake's complete prefab is missing its controller or pool marker.");
            if (prefab.GetComponentsInChildren<EarthquakeTargetReactionVFX>(true).Length != TargetReactionPoolSize)
                throw new UnityException($"Earthquake must pre-author exactly {TargetReactionPoolSize} target reactions.");
            if (prefab.GetComponentsInChildren<MeshRenderer>(true).Length < GroundChunkPoolSize)
                throw new UnityException("Earthquake's authored ground-section pool is incomplete.");
            foreach (ParticleSystem particle in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.main.maxParticles > 384)
                    throw new UnityException($"Earthquake layer '{particle.name}' exceeds the bounded MMO particle budget.");
                if (particle.name != "Ground Tension Dust" && particle.main.simulationSpace != ParticleSystemSimulationSpace.World)
                    throw new UnityException($"Earthquake environmental layer '{particle.name}' must remain in world space.");
                if (particle.collision.enabled)
                    throw new UnityException($"Earthquake VFX layer '{particle.name}' must not use particle collision.");
            }
            if (prefab.GetComponentInChildren<Collider>(true) != null)
                throw new UnityException("Earthquake VFX must remain presentation-only and contain no colliders.");
            if (prefab.GetComponentInChildren<Light>(true) != null || prefab.GetComponentInChildren<Animator>(true) != null)
                throw new UnityException("Earthquake must stay procedural, light-free, and animator-free.");
            if (ability.VisualEffects != definition || definition.CastPrefab != prefab || definition.HitPrefab != null || !definition.CastPrefabControlsHitTiming)
                throw new MissingReferenceException("Earthquake is not wired through its replicated ability-release VFX definition.");
            foreach (string texture in RuntimeTexturePaths())
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(texture) == null) throw new MissingReferenceException($"Missing Earthquake runtime texture: {texture}");
            foreach (string meshName in MeshNames())
                if (AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/{meshName}.asset") == null) throw new MissingReferenceException($"Missing reusable Earthquake mesh: {meshName}");
            foreach (string layerName in ChargeEarthLayerNames())
                if (prefab.transform.Find($"World Space Environmental Layers/Charge Earth Library Layers/{layerName}") == null)
                    throw new MissingReferenceException($"Earthquake is missing its Charge earth-library layer: {layerName}");
            foreach (string materialName in ChargeEarthMaterialNames())
                if (AssetDatabase.LoadAssetAtPath<Material>($"{ChargeMaterialFolder}/{materialName}.mat") == null)
                    throw new MissingReferenceException($"Earthquake requires the shared Charge material: {materialName}");
            Debug.Log("Earthquake VFX validation passed: six-unit data parity, pooled terrain sections, world-space particles, target reactions, generated textures, reusable meshes, URP materials, and multiplayer wiring are valid.", prefab);
        }

        private static EarthquakeVFXProfile CreateProfile()
        {
            EarthquakeVFXProfile profile = AssetDatabase.LoadAssetAtPath<EarthquakeVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<EarthquakeVFXProfile>();
                profile.ResetToProductionDefaults();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader layered = Shader.Find(LayeredShaderName);
            Shader surface = Shader.Find(SurfaceShaderName);
            if (layered == null || surface == null) throw new MissingReferenceException("Earthquake URP shaders have not compiled.");
            string fracture = TextureFolder + "/Earthquake_GroundFractureAtlas.png";
            string dust = TextureFolder + "/Earthquake_DustHazeAtlas.png";
            string debris = TextureFolder + "/Earthquake_DebrisImpactAtlas.png";
            string terrain = TextureFolder + "/Earthquake_TerrainSurfaceAtlas.png";
            Dictionary<string, Material> materials = new()
            {
                ["Cracks"] = CreateLayeredMaterial("Earthquake_GroundCracks", layered, fracture, new Color(0.08f, 0.055f, 0.035f, 0.9f), false, 0.8f, new Vector4(0.5f, 0.5f, 0f, 0.5f)),
                ["Pressure"] = CreateLayeredMaterial("Earthquake_PressureRing", layered, fracture, new Color(0.94f, 0.78f, 0.48f, 0.32f), true, 1.15f, new Vector4(0.5f, 0.5f, 0.5f, 0.5f)),
                ["Impact"] = CreateLayeredMaterial("Earthquake_ImpactFlash", layered, fracture, new Color(1f, 0.82f, 0.44f, 0.78f), true, 1.8f, new Vector4(0.5f, 0.5f, 0f, 0f)),
                ["Dust"] = CreateLayeredMaterial("Earthquake_Dust", layered, dust, new Color(0.76f, 0.58f, 0.36f, 0.68f), false, 1f, new Vector4(1f, 1f, 0f, 0f)),
                ["Smoke"] = CreateLayeredMaterial("Earthquake_EarthSmoke", layered, dust, new Color(0.28f, 0.25f, 0.23f, 0.52f), false, 0.72f, new Vector4(1f, 1f, 0f, 0f)),
                ["Dirt"] = CreateLayeredMaterial("Earthquake_DirtDebris", layered, debris, new Color(0.53f, 0.32f, 0.16f, 0.96f), false, 0.9f, new Vector4(1f, 1f, 0f, 0f)),
                ["Rock"] = CreateLayeredMaterial("Earthquake_RockDebris", layered, debris, new Color(0.48f, 0.47f, 0.44f, 0.98f), false, 0.86f, new Vector4(1f, 1f, 0f, 0f)),
                ["SurfaceTop"] = CreateSurfaceMaterial("Earthquake_TerrainMatchedTop", surface, terrain, Color.white, 0.12f),
                ["SurfaceSide"] = CreateSurfaceMaterial("Earthquake_ExposedDirtSides", surface, terrain, new Color(0.62f, 0.44f, 0.28f, 1f), 0.36f)
            };
            materials["ChargeHeavyDust"] = LoadRequiredMaterial("Charge_HeavyDust");
            materials["ChargeFineDust"] = LoadRequiredMaterial("Charge_FineDust");
            materials["ChargeDirtDebris"] = LoadRequiredMaterial("Charge_DirtDebris");
            materials["ChargeRocks"] = LoadRequiredMaterial("Charge_Rocks");
            materials["ChargeGroundBursts"] = LoadRequiredMaterial("Charge_GroundBursts");
            materials["ChargeShockwaves"] = LoadRequiredMaterial("Charge_Shockwaves");
            return materials;
        }

        private static Material LoadRequiredMaterial(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>($"{ChargeMaterialFolder}/{name}.mat");
            if (material == null) throw new MissingReferenceException($"Required shared Charge VFX material is missing: {name}");
            return material;
        }

        private static Dictionary<string, Mesh> CreateMeshes()
        {
            Dictionary<string, Mesh> result = new();
            result["GroundCubeSmall"] = GetOrCreateChunkMesh("GroundCubeSmall", 0.72f, 0.72f, 0.55f, 0.06f);
            result["GroundCubeMedium"] = GetOrCreateChunkMesh("GroundCubeMedium", 1f, 0.9f, 0.66f, 0.08f);
            result["GroundBlockBroad"] = GetOrCreateChunkMesh("GroundBlockBroad", 1.55f, 1.18f, 0.36f, 0.12f);
            result["GroundSlabTilted"] = GetOrCreateChunkMesh("GroundSlabTilted", 1.38f, 0.82f, 0.24f, 0.16f);
            result["AngularRock"] = GetOrCreateChunkMesh("AngularRock", 0.62f, 0.48f, 0.72f, 0.2f);
            result["Pebble"] = GetOrCreateChunkMesh("Pebble", 0.24f, 0.2f, 0.16f, 0.05f);
            result["FlatRockChip"] = GetOrCreateChunkMesh("FlatRockChip", 0.48f, 0.32f, 0.1f, 0.1f);
            result["PressureRingStrip"] = GetOrCreateRingMesh("PressureRingStrip", 0.42f, 0.5f, 32);
            result["GroundQuad"] = GetOrCreateGroundQuad("GroundQuad");
            return result;
        }

        private static GameObject CreatePrefab(EarthquakeVFXProfile profile, IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("EarthquakeVFX");
            try
            {
                root.AddComponent<MMOAbilityVfxPoolable>().ConfigureAuthoring(12);
                Transform particles = CreateSection("World Space Environmental Layers", root.transform);
                ParticleSystem tension = CreateParticle("Ground Tension Dust", particles, materials["Dust"], true, 4, 4, -1f, 96, 4, ParticleSystemRenderMode.Billboard);
                ParticleSystem compression = CreateParticle("Central Ground Compression", particles, materials["Impact"], true, 1, 1, -1f, 4, 18, ParticleSystemRenderMode.HorizontalBillboard);
                ParticleSystem pressure = CreateParticle("Low Pressure Ring", particles, materials["Pressure"], true, 1, 1, -1f, 4, 13, ParticleSystemRenderMode.HorizontalBillboard);
                ParticleSystem dirtRing = CreateParticle("Outward Dirt Pressure Ring", particles, materials["Dirt"], true, 4, 4, -1f, 160, 10, ParticleSystemRenderMode.Stretch);
                ParticleSystem leading = CreateParticle("Leading Dust Edge", particles, materials["Dust"], true, 4, 4, -1f, 128, 12, ParticleSystemRenderMode.Stretch);
                ParticleSystem mainDust = CreateParticle("Main Rolling Dust Body", particles, materials["Dust"], true, 4, 4, -1f, 256, 8, ParticleSystemRenderMode.Billboard);
                ParticleSystem smoke = CreateParticle("Brown Gray Earth Haze", particles, materials["Smoke"], true, 4, 4, -1f, 128, 7, ParticleSystemRenderMode.Billboard);
                ParticleSystem wake = CreateParticle("Fine Suspended Dirt Wake", particles, materials["Dust"], true, 4, 4, -1f, 160, 6, ParticleSystemRenderMode.Billboard);
                ParticleSystem dirt = CreateParticle("Skidding Dirt And Earth Flakes", particles, materials["Dirt"], true, 4, 4, -1f, 192, 11, ParticleSystemRenderMode.Stretch);
                ParticleSystem rocks = CreateParticle("Rock And Debris Ring", particles, materials["Rock"], true, 4, 4, -1f, 96, 9, ParticleSystemRenderMode.Billboard);
                ConfigureParticleMotion(tension, 0.42f, 0f);
                ConfigureParticleMotion(compression, 0.35f, 0f); ConfigureScale(compression, 0.08f, 1.15f, 1.32f, 0.36f);
                ConfigureParticleMotion(pressure, profile.WaveDuration, 0f); ConfigureScale(pressure, 0.04f, 0.64f, 1f, 0.68f);
                ConfigureParticleMotion(dirtRing, profile.DirtLifetime, 0.8f);
                ConfigureParticleMotion(leading, profile.DustLifetime, 0f);
                ConfigureParticleMotion(mainDust, profile.DustLifetime, -0.04f);
                ConfigureParticleMotion(smoke, profile.SmokeFadeDuration, -0.08f);
                ConfigureParticleMotion(wake, profile.DustLifetime * 1.2f, -0.04f);
                ConfigureParticleMotion(dirt, profile.DirtLifetime, 0.95f);
                ConfigureParticleMotion(rocks, profile.RockLifetime, 1.2f);

                Transform chargeLayersRoot = CreateSection("Charge Earth Library Layers", particles);
                ParticleSystem[] chargeEarthLayers =
                {
                    CreateParticle("Charge Heavy Dust Surge", chargeLayersRoot, materials["ChargeHeavyDust"], true, 4, 2, -1f, 96, 14, ParticleSystemRenderMode.Billboard),
                    CreateParticle("Charge Fine Dust Clouds", chargeLayersRoot, materials["ChargeFineDust"], true, 4, 2, -1f, 128, 5, ParticleSystemRenderMode.Billboard),
                    CreateParticle("Charge Ground Scrape Dust", chargeLayersRoot, materials["ChargeHeavyDust"], true, 4, 2, -1f, 72, 15, ParticleSystemRenderMode.Stretch),
                    CreateParticle("Charge Dirt Chunks", chargeLayersRoot, materials["ChargeDirtDebris"], true, 4, 1, -1f, 64, 16, ParticleSystemRenderMode.Billboard),
                    CreateParticle("Charge Scrape Debris", chargeLayersRoot, materials["ChargeDirtDebris"], true, 4, 1, -1f, 48, 17, ParticleSystemRenderMode.Stretch),
                    CreateParticle("Charge Rocks", chargeLayersRoot, materials["ChargeRocks"], true, 4, 1, -1f, 32, 18, ParticleSystemRenderMode.Billboard),
                    CreateParticle("Charge Ground Burst", chargeLayersRoot, materials["ChargeGroundBursts"], true, 4, 1, -1f, 40, 19, ParticleSystemRenderMode.Billboard),
                    CreateParticle("Charge Ground Shockwave", chargeLayersRoot, materials["ChargeShockwaves"], true, 4, 1, -1f, 4, 13, ParticleSystemRenderMode.HorizontalBillboard)
                };
                ConfigureParticleMotion(chargeEarthLayers[0], 1.45f, -0.04f); ConfigureScale(chargeEarthLayers[0], 0.62f, 1f, 1.2f, 0.38f);
                ConfigureParticleMotion(chargeEarthLayers[1], 1.9f, -0.06f); ConfigureScale(chargeEarthLayers[1], 0.54f, 1f, 1.28f, 0.44f);
                ConfigureParticleMotion(chargeEarthLayers[2], 1.05f, 0f); ConfigureScale(chargeEarthLayers[2], 0.5f, 1f, 1.12f, 0.3f);
                ConfigureParticleMotion(chargeEarthLayers[3], 1.2f, 1.55f);
                ConfigureParticleMotion(chargeEarthLayers[4], 1f, 1.35f);
                ConfigureParticleMotion(chargeEarthLayers[5], 1.25f, 1.6f);
                ConfigureParticleMotion(chargeEarthLayers[6], 0.7f, 0.05f); ConfigureScale(chargeEarthLayers[6], 0.32f, 1f, 1.2f, 0.35f);
                ConfigureParticleMotion(chargeEarthLayers[7], profile.WaveDuration, 0f); ConfigureScale(chargeEarthLayers[7], 0.12f, 0.78f, 1f, 0.55f);
                ParticleSystem.MainModule chargeShockwaveMain = chargeEarthLayers[7].main; chargeShockwaveMain.startRotation3D = true;
                ParticleSystemRenderer chargeShockwaveRenderer = chargeEarthLayers[7].GetComponent<ParticleSystemRenderer>();
                chargeShockwaveRenderer.renderMode = ParticleSystemRenderMode.Mesh; chargeShockwaveRenderer.mesh = meshes["GroundQuad"];

                List<Renderer> cracks = new();
                Transform crackRoot = CreateSection("Expanding Non Glowing Cracks", root.transform);
                for (int i = 0; i < 7; i++)
                {
                    float angle = i / 7f * Mathf.PI * 2f + i * 0.29f;
                    float radius = Mathf.Lerp(0.25f, 4.8f, (i * 0.43f) % 1f);
                    MeshRenderer renderer = CreateMeshRenderer($"Crack Section {i + 1:00}", crackRoot, meshes["GroundQuad"], materials["Cracks"],
                        new Vector3(Mathf.Cos(angle) * radius, 0.012f + i * 0.001f, Mathf.Sin(angle) * radius), Vector3.one,
                        Quaternion.Euler(0f, angle * Mathf.Rad2Deg + i * 23f, 0f), 3 + i);
                    cracks.Add(renderer);
                }

                List<Transform> chunks = new();
                List<Renderer> chunkRenderers = new();
                Transform chunkRoot = CreateSection("Terrain Matched Ground Sections", root.transform);
                string[] chunkMeshes = { "GroundCubeSmall", "GroundCubeMedium", "GroundBlockBroad", "GroundSlabTilted" };
                System.Random random = new(9127);
                for (int i = 0; i < GroundChunkPoolSize; i++)
                {
                    float angle = (i + (float)random.NextDouble() * 0.72f) / GroundChunkPoolSize * Mathf.PI * 2f;
                    float radius = Mathf.Lerp(0.48f, profile.Radius * 0.92f, Mathf.Sqrt((float)random.NextDouble()));
                    GameObject chunk = new($"Ground Section {i + 1:00}");
                    chunk.transform.SetParent(chunkRoot, false);
                    chunk.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, -0.22f, Mathf.Sin(angle) * radius);
                    chunk.transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    float scale = Mathf.Lerp(profile.CubeSizeRange.x, profile.CubeSizeRange.y, (float)random.NextDouble());
                    chunk.transform.localScale = Vector3.one * scale;
                    MeshFilter filter = chunk.AddComponent<MeshFilter>(); filter.sharedMesh = meshes[chunkMeshes[i % chunkMeshes.Length]];
                    MeshRenderer renderer = chunk.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { materials["SurfaceTop"], materials["SurfaceSide"] };
                    renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    chunks.Add(chunk.transform); chunkRenderers.Add(renderer); chunk.SetActive(false);
                }

                EarthquakeTargetReactionVFX[] reactions = new EarthquakeTargetReactionVFX[TargetReactionPoolSize];
                Transform reactionRoot = CreateSection("Pooled Enemy Earth Impacts", root.transform);
                for (int i = 0; i < reactions.Length; i++)
                    reactions[i] = CreateTargetReaction($"Enemy Earth Impact {i + 1:00}", reactionRoot, meshes, materials);

                EarthquakeVFX controller = root.AddComponent<EarthquakeVFX>();
                controller.ConfigureAuthoring(profile,
                    new[] { tension, compression, pressure, dirtRing, leading, mainDust, smoke, wake, dirt, rocks },
                    chargeEarthLayers,
                    cracks.ToArray(), chunks.ToArray(), chunkRenderers.ToArray(), reactions);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new UnityException($"Failed to save Earthquake prefab at {PrefabPath}.");
                return saved;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static EarthquakeTargetReactionVFX CreateTargetReaction(string name, Transform parent, IReadOnlyDictionary<string, Mesh> meshes, IReadOnlyDictionary<string, Material> materials)
        {
            Transform root = CreateSection(name, parent);
            MeshRenderer crack = CreateMeshRenderer("Local Ground Crack", root, meshes["GroundQuad"], materials["Cracks"], new Vector3(0f, 0.012f, 0f), Vector3.one, Quaternion.identity, 20);
            MeshRenderer pulse = CreateMeshRenderer("Brown Gray Impact Pulse", root, meshes["GroundQuad"], materials["Pressure"], new Vector3(0f, 0.025f, 0f), Vector3.one, Quaternion.Euler(0f, 37f, 0f), 21);
            Transform[] blocks = new Transform[3]; Renderer[] renderers = new Renderer[3];
            for (int i = 0; i < blocks.Length; i++)
            {
                GameObject block = new($"Local Ground Block {i + 1}"); block.transform.SetParent(root, false);
                float angle = i / 3f * Mathf.PI * 2f; block.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.46f, -0.15f, Mathf.Sin(angle) * 0.46f);
                block.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 0.58f, i / 2f);
                block.AddComponent<MeshFilter>().sharedMesh = meshes[i == 2 ? "GroundBlockBroad" : "GroundCubeSmall"];
                MeshRenderer renderer = block.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { materials["SurfaceTop"], materials["SurfaceSide"] };
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; blocks[i] = block.transform; renderers[i] = renderer; block.SetActive(false);
            }
            ParticleSystem dust = CreateParticle("Local Low Dust Burst", root, materials["Dust"], true, 4, 4, -1f, 48, 24, ParticleSystemRenderMode.Stretch);
            ParticleSystem dirt = CreateParticle("Local Skidding Dirt", root, materials["Dirt"], true, 4, 4, -1f, 32, 25, ParticleSystemRenderMode.Stretch);
            ParticleSystem rocks = CreateParticle("Local Bouncing Rocks", root, materials["Rock"], true, 4, 4, -1f, 24, 26, ParticleSystemRenderMode.Billboard);
            ConfigureParticleMotion(dust, 0.68f, 0f); ConfigureParticleMotion(dirt, 0.78f, 0.8f); ConfigureParticleMotion(rocks, 0.9f, 1.2f);
            EarthquakeTargetReactionVFX reaction = root.gameObject.AddComponent<EarthquakeTargetReactionVFX>();
            reaction.ConfigureAuthoring(crack, pulse, blocks, renderers, dust, dirt, rocks); root.gameObject.SetActive(false); return reaction;
        }

        private static void WireDefinition(GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null) throw new MissingReferenceException("The existing Shaman Earthquake ability or VFX definition is missing.");
            definition.Configure(null, prefab, null, false, false, false, false, Vector3.zero, Vector3.zero, Vector3.zero, Vector3.zero, 0f, true);
            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(definition); EditorUtility.SetDirty(ability);
        }

        private static ParticleSystem CreateParticle(string name, Transform parent, Material material, bool worldSpace, int columns, int rows, float fixedFrame, int maxParticles, int order, ParticleSystemRenderMode mode)
        {
            GameObject child = new(name); child.transform.SetParent(parent, false);
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main; main.loop = false; main.playOnAwake = false; main.duration = 2.5f; main.startLifetime = 1f; main.startSpeed = 0f; main.startSize = 1f; main.maxParticles = maxParticles;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local; main.stopAction = ParticleSystemStopAction.None;
            ParticleSystem.EmissionModule emission = system.emission; emission.enabled = false;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.renderMode = mode; renderer.sortingOrder = order; renderer.enableGPUInstancing = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            if (mode == ParticleSystemRenderMode.Stretch) { renderer.velocityScale = 0.12f; renderer.lengthScale = 1.35f; }
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation; sheet.enabled = columns > 1 || rows > 1; sheet.mode = ParticleSystemAnimationMode.Grid; sheet.numTilesX = columns; sheet.numTilesY = rows; sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = fixedFrame >= 0f ? new ParticleSystem.MinMaxCurve(fixedFrame) : new ParticleSystem.MinMaxCurve(0f, 0.999f);
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime; color.enabled = true; Gradient gradient = new(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.82f, 0.62f), new GradientAlphaKey(0f, 1f) }); color.color = gradient;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime; rotation.enabled = true; rotation.z = new ParticleSystem.MinMaxCurve(-1.3f, 1.3f);
            return system;
        }

        private static void ConfigureParticleMotion(ParticleSystem system, float lifetime, float gravity)
        {
            ParticleSystem.MainModule main = system.main; main.startLifetime = Mathf.Max(0.05f, lifetime); main.gravityModifier = gravity;
            ParticleSystem.NoiseModule noise = system.noise; noise.enabled = true; noise.quality = ParticleSystemNoiseQuality.Low; noise.strength = 0.12f; noise.frequency = 0.45f; noise.scrollSpeed = 0.18f;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime; drag.enabled = true; drag.dampen = 0.18f; drag.limit = 12f;
            ParticleSystem.CollisionModule collision = system.collision; collision.enabled = false;
        }

        private static void ConfigureScale(ParticleSystem system, float start, float middle, float end, float middleTime)
        {
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, start), new Keyframe(middleTime, middle), new Keyframe(1f, end)));
        }

        private static MeshRenderer CreateMeshRenderer(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation, int order)
        {
            GameObject child = new(name); child.transform.SetParent(parent, false); child.transform.localPosition = position; child.transform.localScale = scale; child.transform.localRotation = rotation;
            child.AddComponent<MeshFilter>().sharedMesh = mesh; MeshRenderer renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = order; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; return renderer;
        }

        private static Material CreateLayeredMaterial(string name, Shader shader, string texturePath, Color tint, bool additive, float brightness, Vector4 atlas)
        {
            string path = $"{MaterialFolder}/{name}.mat"; Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.enableInstancing = true; material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)); material.SetTextureScale("_BaseMap", new Vector2(atlas.x, atlas.y)); material.SetTextureOffset("_BaseMap", new Vector2(atlas.z, atlas.w));
            material.SetColor("_Tint", tint); material.SetFloat("_Opacity", tint.a); material.SetFloat("_Brightness", brightness); material.SetFloat("_NoiseStrength", 0f); material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha); material.renderQueue = (int)RenderQueue.Transparent; EditorUtility.SetDirty(material); return material;
        }

        private static Material CreateSurfaceMaterial(string name, Shader shader, string texturePath, Color tint, float sideDarkening)
        {
            string path = $"{MaterialFolder}/{name}.mat"; Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader) { name = name }; AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.enableInstancing = true; material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)); material.SetColor("_Tint", tint); material.SetFloat("_SideDarkening", sideDarkening); EditorUtility.SetDirty(material); return material;
        }

        private static Mesh GetOrCreateGroundQuad(string name)
        {
            string path = $"{MeshFolder}/{name}.asset"; Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            mesh = new Mesh { name = name }; mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) }; mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up }; mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }; mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static Mesh GetOrCreateChunkMesh(string name, float width, float depth, float height, float inset)
        {
            string path = $"{MeshFolder}/{name}.asset"; Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            List<Vector3> vertices = new(); List<Vector3> normals = new(); List<Vector2> uv = new(); List<int> top = new(); List<int> sides = new();
            float x = width * 0.5f, z = depth * 0.5f, tx = Mathf.Max(0.05f, x - inset), tz = Mathf.Max(0.05f, z - inset), y0 = -height * 0.5f, y1 = height * 0.5f;
            AddQuad(vertices, normals, uv, top, new(-tx, y1, -tz), new(-tx, y1, tz), new(tx, y1, tz), new(tx, y1, -tz), Vector3.up);
            AddQuad(vertices, normals, uv, sides, new(-x, y0, z), new(-tx, y1, tz), new(-tx, y1, -tz), new(-x, y0, -z), Vector3.left);
            AddQuad(vertices, normals, uv, sides, new(x, y0, -z), new(tx, y1, -tz), new(tx, y1, tz), new(x, y0, z), Vector3.right);
            AddQuad(vertices, normals, uv, sides, new(-x, y0, -z), new(-tx, y1, -tz), new(tx, y1, -tz), new(x, y0, -z), Vector3.back);
            AddQuad(vertices, normals, uv, sides, new(x, y0, z), new(tx, y1, tz), new(-tx, y1, tz), new(-x, y0, z), Vector3.forward);
            AddQuad(vertices, normals, uv, sides, new(-x, y0, -z), new(x, y0, -z), new(x, y0, z), new(-x, y0, z), Vector3.down);
            mesh = new Mesh { name = name, subMeshCount = 2 }; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(top, 0); mesh.SetTriangles(sides, 1); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static Mesh GetOrCreateRingMesh(string name, float innerRadius, float outerRadius, int segments)
        {
            string path = $"{MeshFolder}/{name}.asset"; Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path); if (mesh != null) return mesh;
            List<Vector3> vertices = new(); List<Vector2> uv = new(); List<int> triangles = new();
            for (int i = 0; i <= segments; i++) { float t = i / (float)segments; float a = t * Mathf.PI * 2f; Vector3 radial = new(Mathf.Cos(a), 0f, Mathf.Sin(a)); vertices.Add(radial * innerRadius); vertices.Add(radial * outerRadius); uv.Add(new Vector2(t, 0f)); uv.Add(new Vector2(t, 1f)); if (i < segments) { int v = i * 2; triangles.AddRange(new[] { v, v + 1, v + 3, v, v + 3, v + 2 }); } }
            mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path); return mesh;
        }

        private static void AddQuad(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int start = vertices.Count; vertices.AddRange(new[] { a, b, c, d }); normals.AddRange(new[] { normal, normal, normal, normal }); uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }); triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
        }

        private static void ConfigureTextureImporters()
        {
            foreach (string path in RuntimeTexturePaths())
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                bool terrain = path.EndsWith("Earthquake_TerrainSurfaceAtlas.png", StringComparison.Ordinal);
                importer.textureType = TextureImporterType.Default; importer.alphaSource = TextureImporterAlphaSource.None; importer.alphaIsTransparency = false; importer.sRGBTexture = true; importer.wrapMode = terrain ? TextureWrapMode.Repeat : TextureWrapMode.Clamp; importer.filterMode = FilterMode.Bilinear; importer.mipmapEnabled = terrain; importer.maxTextureSize = 2048; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            }
        }

        private static IEnumerable<string> RuntimeTexturePaths()
        {
            yield return TextureFolder + "/Earthquake_GroundFractureAtlas.png"; yield return TextureFolder + "/Earthquake_DustHazeAtlas.png"; yield return TextureFolder + "/Earthquake_DebrisImpactAtlas.png"; yield return TextureFolder + "/Earthquake_TerrainSurfaceAtlas.png";
        }

        private static IEnumerable<string> MeshNames()
        {
            yield return "GroundCubeSmall"; yield return "GroundCubeMedium"; yield return "GroundBlockBroad"; yield return "GroundSlabTilted"; yield return "AngularRock"; yield return "Pebble"; yield return "FlatRockChip"; yield return "PressureRingStrip"; yield return "GroundQuad";
        }

        private static IEnumerable<string> ChargeEarthLayerNames()
        {
            yield return "Charge Heavy Dust Surge"; yield return "Charge Fine Dust Clouds"; yield return "Charge Ground Scrape Dust"; yield return "Charge Dirt Chunks";
            yield return "Charge Scrape Debris"; yield return "Charge Rocks"; yield return "Charge Ground Burst"; yield return "Charge Ground Shockwave";
        }

        private static IEnumerable<string> ChargeEarthMaterialNames()
        {
            yield return "Charge_HeavyDust"; yield return "Charge_FineDust"; yield return "Charge_DirtDebris";
            yield return "Charge_Rocks"; yield return "Charge_GroundBursts"; yield return "Charge_Shockwaves";
        }

        private static Transform CreateSection(string name, Transform parent) { GameObject child = new(name); child.transform.SetParent(parent, false); return child.transform; }
        private static void EnsureFolders() { EnsureFolder("Assets/_Project/VFX", "Earthquake"); EnsureFolder(Root, "Textures"); EnsureFolder(Root, "Materials"); EnsureFolder(Root, "Meshes"); EnsureFolder(Root, "Profiles"); EnsureFolder(Root, "Prefabs"); EnsureFolder(Root, "Documentation"); EnsureFolder(Root, "Shaders"); }
        private static void EnsureFolder(string parent, string name) { string path = parent + "/" + name; if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name); }
    }
}
