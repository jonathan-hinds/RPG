using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Warrior;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class ThunderClapVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/ThunderClap";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialFolder = Root + "/Materials";
        private const string ProfileFolder = Root + "/Profiles";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string DocumentationFolder = Root + "/Documentation";
        private const string ShaderFolder = Root + "/Shaders";
        private const string ProfilePath = ProfileFolder + "/ThunderClapVFX_Default.asset";
        private const string CastPrefabPath = PrefabFolder + "/ThunderClapCastVFX.prefab";
        private const string ImpactPrefabPath = PrefabFolder + "/ThunderClapImpactVFX.prefab";
        private const string ShockwavePrefabPath = PrefabFolder + "/ThunderClapShockwaveVFX.prefab";
        private const string TargetReactionPrefabPath = PrefabFolder + "/ThunderClapTargetReactionVFX.prefab";
        private const string AftermathPrefabPath = PrefabFolder + "/ThunderClapAftermathVFX.prefab";
        private const string CompletePrefabPath = PrefabFolder + "/ThunderClapVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Warrior_Thunderclap_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Thunderclap.asset";
        private const string LayeredShaderName = "RPG Clone/VFX/Thunder Clap Layered Unlit";
        private const string DistortionShaderName = "RPG Clone/VFX/Thunder Clap Distortion";
        private const int TargetPoolSize = 12;

        [MenuItem("Tools/RPG Clone/VFX/Build Thunder Clap VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh();
            ConfigureTextureImporters();
            ThunderClapVFXProfile profile = CreateProfile();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject cast = CreateCastPrefab(materials);
            GameObject impact = CreateImpactPrefab(materials);
            GameObject shockwave = CreateShockwavePrefab(materials);
            GameObject targetReaction = CreateTargetReactionPrefab(materials);
            GameObject aftermath = CreateAftermathPrefab(materials);
            GameObject complete = CreateCompletePrefab(profile, cast, impact, shockwave, targetReaction, aftermath);
            WireDefinition(complete);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Selection.activeObject = complete;
            Debug.Log("Built Thunder Clap VFX: generated art, layered URP materials, pooled target reactions, world-space aftermath, and replicated combat-event presentation are ready.", complete);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Thunder Clap VFX")]
        public static void Validate()
        {
            ThunderClapVFXProfile profile = AssetDatabase.LoadAssetAtPath<ThunderClapVFXProfile>(ProfilePath);
            GameObject cast = AssetDatabase.LoadAssetAtPath<GameObject>(CastPrefabPath);
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
            GameObject shockwave = AssetDatabase.LoadAssetAtPath<GameObject>(ShockwavePrefabPath);
            GameObject targetReaction = AssetDatabase.LoadAssetAtPath<GameObject>(TargetReactionPrefabPath);
            GameObject aftermath = AssetDatabase.LoadAssetAtPath<GameObject>(AftermathPrefabPath);
            GameObject complete = AssetDatabase.LoadAssetAtPath<GameObject>(CompletePrefabPath);
            if (profile == null || cast == null || impact == null || shockwave == null || targetReaction == null || aftermath == null || complete == null)
            {
                throw new MissingReferenceException("Thunder Clap profile or one or more reusable phase prefabs are missing. Run Build Thunder Clap VFX.");
            }

            ThunderClapVFX controller = complete.GetComponent<ThunderClapVFX>();
            ThunderClapVFXPackage package = complete.GetComponent<ThunderClapVFXPackage>();
            if (controller == null || package == null || package.CompletePrefab != complete
                || cast.GetComponent<ThunderClapCastVFX>() == null
                || impact.GetComponent<ThunderClapImpactVFX>() == null
                || shockwave.GetComponent<ThunderClapShockwaveVFX>() == null
                || targetReaction.GetComponent<ThunderClapTargetReactionVFX>() == null
                || aftermath.GetComponent<ThunderClapAftermathVFX>() == null)
            {
                throw new MissingComponentException("Thunder Clap phase or package controllers are not wired consistently.");
            }

            if (complete.GetComponentsInChildren<ThunderClapTargetReactionVFX>(true).Length != TargetPoolSize)
            {
                throw new UnityException($"Thunder Clap must pre-author exactly {TargetPoolSize} pooled target reactions.");
            }

            if (Mathf.Abs(profile.RingRadius - 6f) > 0.01f)
            {
                throw new UnityException("Thunder Clap default shockwave radius must remain aligned with the six-unit gameplay radius.");
            }

            foreach (ParticleSystem particle in complete.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.main.maxParticles > 512)
                {
                    throw new UnityException($"Thunder Clap particle layer '{particle.name}' exceeds the bounded MMO budget.");
                }

                if ((particle.name.Contains("Dust") || particle.name.Contains("Debris") || particle.name.Contains("Rock")
                    || particle.name.Contains("Dirt") || particle.name.Contains("Wake") || particle.name.Contains("Foot Burst"))
                    && particle.name != "Attached Anticipation Sparks"
                    && particle.main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    throw new UnityException($"Thunder Clap environmental layer '{particle.name}' must simulate in world space.");
                }
            }

            if (complete.GetComponentInChildren<Light>(true) != null || complete.GetComponentInChildren<Animator>(true) != null)
            {
                throw new UnityException("Thunder Clap must remain procedural, light-free, and animator-free for MMO performance.");
            }

            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null || ability.VisualEffects != definition
                || definition.CastPrefab != complete || definition.HitPrefab != null || !definition.CastPrefabControlsHitTiming)
            {
                throw new MissingReferenceException("Warrior Thunderclap is not wired through its existing VFX definition to the complete ThunderClapVFX prefab.");
            }

            foreach (string texturePath in RuntimeTexturePaths())
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath) == null)
                {
                    throw new MissingReferenceException($"Thunder Clap runtime texture is missing: {texturePath}");
                }
            }

            Debug.Log("Thunder Clap VFX validation passed: phase package, six-unit shockwave, world-space environment, bounded particles, pooled target reactions, URP materials, and multiplayer definition wiring are valid.", complete);
        }

        private static ThunderClapVFXProfile CreateProfile()
        {
            ThunderClapVFXProfile profile = AssetDatabase.LoadAssetAtPath<ThunderClapVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ThunderClapVFXProfile>();
                profile.ResetToProductionDefaults();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader layered = Shader.Find(LayeredShaderName);
            Shader distortion = Shader.Find(DistortionShaderName);
            if (layered == null || distortion == null)
            {
                throw new MissingReferenceException("Thunder Clap URP shaders are missing or unsupported. Refresh scripts and shaders before rebuilding.");
            }

            string heavy = TextureFolder + "/ThunderClap_HeavyDustAtlas.png";
            string fine = TextureFolder + "/ThunderClap_FineDustSmokeAtlas.png";
            string debris = TextureFolder + "/ThunderClap_DebrisAtlas.png";
            string rings = TextureFolder + "/ThunderClap_GroundRingsAtlas.png";
            string electrical = TextureFolder + "/ThunderClap_ElectricalAtlas.png";
            string sparks = TextureFolder + "/ThunderClap_ElectricalSparksAtlas.png";
            string core = TextureFolder + "/ThunderClap_LightningCore.png";
            string noise = TextureFolder + "/ThunderClap_HeatDistortion.png";

            return new Dictionary<string, Material>
            {
                ["HeavyDust"] = CreateMaterial("ThunderClap_HeavyDust", layered, heavy, noise, new Color(0.72f, 0.5f, 0.28f, 0.78f), false, 1f),
                ["FineDust"] = CreateMaterial("ThunderClap_FineDust", layered, fine, noise, new Color(0.86f, 0.68f, 0.42f, 0.52f), false, 1f),
                ["Dirt"] = CreateMaterial("ThunderClap_DirtDebris", layered, debris, noise, new Color(0.56f, 0.35f, 0.17f, 0.96f), false, 1f),
                ["Rocks"] = CreateMaterial("ThunderClap_Rocks", layered, debris, noise, new Color(0.65f, 0.66f, 0.68f, 0.96f), false, 0.95f),
                ["Pressure"] = CreateMaterial("ThunderClap_PressureRing", layered, rings, noise, new Color(0.82f, 0.94f, 1f, 0.3f), false, 1.15f),
                ["Shockwave"] = CreateMaterial("ThunderClap_ShockwaveRing", layered, rings, noise, new Color(0.78f, 0.58f, 0.32f, 0.72f), false, 1f),
                ["LightningCore"] = CreateMaterial("ThunderClap_LightningCore", layered, core, noise, new Color(1f, 0.98f, 0.82f, 1f), true, 3.4f, 26f, 0.22f),
                ["LightningBranches"] = CreateMaterial("ThunderClap_LightningBranches", layered, electrical, noise, new Color(0.28f, 0.9f, 1f, 0.95f), true, 2.6f, 22f, 0.18f),
                ["GroundElectricity"] = CreateMaterial("ThunderClap_GroundElectricity", layered, core, noise, new Color(0.16f, 0.64f, 1f, 0.8f), true, 2.1f, 19f, 0.28f),
                ["Sparks"] = CreateMaterial("ThunderClap_Sparks", layered, sparks, noise, new Color(0.82f, 0.98f, 1f, 0.95f), true, 2.8f, 0f, 0f),
                ["Flash"] = CreateMaterial("ThunderClap_ElectricalFlash", layered, electrical, noise, new Color(1f, 0.96f, 0.78f, 1f), true, 3.8f, 18f, 0.1f),
                ["Smoke"] = CreateMaterial("ThunderClap_Smoke", layered, fine, noise, new Color(0.28f, 0.24f, 0.22f, 0.58f), false, 0.8f),
                ["Distortion"] = CreateDistortionMaterial(distortion, rings, noise)
            };
        }

        private static GameObject CreateCastPrefab(IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ThunderClapCastVFX");
            try
            {
                ParticleSystem liftingDust = CreateParticle("Lifting Foot Dust", root.transform, materials["FineDust"], true, 2, 2, 0f, 64, 3);
                ParticleSystem stones = CreateParticle("Vibrating Tiny Stones", root.transform, materials["Dirt"], true, 4, 4, -1f, 48, 4);
                ParticleSystem sparks = CreateParticle("Attached Anticipation Sparks", root.transform, materials["Sparks"], false, 4, 4, -1f, 64, 8, ParticleSystemRenderMode.Stretch);
                ConfigureFadeAndScale(liftingDust, 0.35f, 1f, 1.25f, 0.05f, 0.55f);
                ConfigureFadeAndScale(stones, 0.7f, 1f, 0.85f, 0.02f, 0.6f);
                ConfigureFadeAndScale(sparks, 0.65f, 1.15f, 0.1f, 0.02f, 0.68f);
                root.AddComponent<ThunderClapCastVFX>().ConfigureAuthoring(liftingDust, stones, sparks);
                return SavePrefab(root, CastPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateImpactPrefab(IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ThunderClapImpactVFX");
            try
            {
                ParticleSystem compression = CreateParticle("Ground Compression", root.transform, materials["Shockwave"], true, 2, 2, 0.75f, 4, 2, ParticleSystemRenderMode.HorizontalBillboard);
                ParticleSystem flash = CreateParticle("Central Impact Flash", root.transform, materials["Flash"], true, 2, 2, 0.25f, 8, 20, ParticleSystemRenderMode.HorizontalBillboard);
                ParticleSystem heavy = CreateParticle("Heavy Earth Explosion Dust", root.transform, materials["HeavyDust"], true, 2, 2, -1f, 192, 5);
                ParticleSystem fine = CreateParticle("Fine Suspended Dust", root.transform, materials["FineDust"], true, 2, 2, -1f, 256, 4);
                ParticleSystem dirt = CreateParticle("Dirt Chunks", root.transform, materials["Dirt"], true, 4, 4, 0f, 128, 6);
                ParticleSystem rocks = CreateParticle("Rock Fragments", root.transform, materials["Rocks"], true, 4, 4, 0.5f, 96, 7);
                ParticleSystem sparks = CreateParticle("Impact Electrical Sparks", root.transform, materials["Sparks"], true, 4, 4, -1f, 192, 14, ParticleSystemRenderMode.Stretch);

                ConfigureMain(compression, 0.18f, 1f, 0f);
                ConfigureMain(flash, 0.13f, 1f, 0f);
                ConfigureMain(heavy, 1.55f, 1f, 0.1f);
                ConfigureMain(fine, 2.2f, 1f, -0.03f);
                ConfigureDebris(dirt, 1.45f, 0.85f, 0.3f);
                ConfigureDebris(rocks, 1.7f, 1.15f, 0.42f);
                ConfigureMain(sparks, 0.38f, 1f, 0.15f);
                ConfigureFadeAndScale(compression, 0.18f, 1f, 1.2f, 0f, 0.52f);
                ConfigureFadeAndScale(flash, 0.2f, 1f, 1.35f, 0f, 0.32f);
                ConfigureFadeAndScale(heavy, 0.55f, 1.1f, 1.48f, 0.04f, 0.58f);
                ConfigureFadeAndScale(fine, 0.45f, 1f, 1.7f, 0.08f, 0.64f);
                ConfigureFadeAndScale(sparks, 1f, 0.78f, 0.05f, 0f, 0.55f);
                root.AddComponent<ThunderClapImpactVFX>().ConfigureAuthoring(new[] { compression, flash, heavy, fine, dirt, rocks, sparks });
                return SavePrefab(root, ImpactPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateShockwavePrefab(IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ThunderClapShockwaveVFX");
            try
            {
                Transform rings = CreateSection("Expanding Ground Layers", root.transform);
                ParticleSystem pressure = CreateRing("Primary Pressure Ring", rings, materials["Pressure"], 0f, 12);
                ParticleSystem shockwave = CreateRing("Physical Shockwave Ring", rings, materials["Shockwave"], 0.25f, 10);
                ParticleSystem dustWall = CreateRing("Rolling Dust Wall", rings, materials["Shockwave"], 0.5f, 8);
                ParticleSystem lightningRing = CreateRing("Circular Lightning Ring", rings, materials["LightningBranches"], 0f, 15);
                ParticleSystem distortion = CreateRing("Air Compression Distortion", rings, materials["Distortion"], 0f, 18);
                ParticleSystem wake = CreateParticle("World Dirt Wake", root.transform, materials["FineDust"], true, 2, 2, -1f, 192, 5);
                ParticleSystem sparks = CreateParticle("Electrical Ring Sparks", root.transform, materials["Sparks"], true, 4, 4, -1f, 256, 16, ParticleSystemRenderMode.Stretch);
                ConfigureMain(wake, 1.2f, 1f, 0.18f);
                ConfigureMain(sparks, 0.42f, 1f, 0.12f);
                ConfigureFadeAndScale(wake, 0.5f, 1f, 1.45f, 0.04f, 0.54f);
                ConfigureFadeAndScale(sparks, 1f, 0.7f, 0.05f, 0f, 0.56f);

                Transform crawlerRoot = CreateSection("Ground Crawling Electricity", root.transform);
                LineRenderer[] crawlers = new LineRenderer[8];
                for (int i = 0; i < crawlers.Length; i++) crawlers[i] = CreateLine($"Ground Crawler {i + 1:00}", crawlerRoot, materials["GroundElectricity"], 20 + i);
                Transform strikeRoot = CreateSection("Secondary Lightning Strikes", root.transform);
                LineRenderer[] strikes = new LineRenderer[6];
                for (int i = 0; i < strikes.Length; i++) strikes[i] = CreateLine($"Secondary Strike {i + 1:00}", strikeRoot, materials["LightningCore"], 30 + i);

                root.AddComponent<ThunderClapShockwaveVFX>().ConfigureAuthoring(
                    new[] { pressure, shockwave, dustWall, lightningRing, distortion, wake, sparks }, crawlers, strikes);
                return SavePrefab(root, ShockwavePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateTargetReactionPrefab(IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ThunderClapTargetReactionVFX");
            try
            {
                ParticleSystem flash = CreateParticle("Body Impact Flash", root.transform, materials["Flash"], false, 2, 2, 0.25f, 8, 28);
                ParticleSystem foot = CreateParticle("World Foot Burst", root.transform, materials["FineDust"], true, 2, 2, 0.25f, 48, 5);
                ParticleSystem bands = CreateParticle("Debuff Confirmation Bands", root.transform, materials["LightningBranches"], false, 2, 2, 0f, 8, 20, ParticleSystemRenderMode.HorizontalBillboard);
                ParticleSystem sparks = CreateParticle("Debuff Break Sparks", root.transform, materials["Sparks"], false, 4, 4, -1f, 64, 22, ParticleSystemRenderMode.Stretch);
                ConfigureMain(flash, 0.16f, 1f, 0f);
                ConfigureMain(foot, 0.62f, 1f, 0.2f);
                ConfigureMain(bands, 0.5f, 1f, 0f);
                ConfigureMain(sparks, 0.38f, 1f, 0.15f);
                ConfigureFadeAndScale(flash, 0.2f, 1f, 1.3f, 0f, 0.32f);
                ConfigureFadeAndScale(foot, 0.5f, 1f, 1.28f, 0.03f, 0.52f);
                ConfigureFadeAndScale(bands, 0.7f, 1f, 1.12f, 0f, 0.7f);
                ConfigureFadeAndScale(sparks, 1f, 0.72f, 0.05f, 0f, 0.58f);
                Transform arcsRoot = CreateSection("Electrical Body Wrap", root.transform);
                LineRenderer[] arcs = new LineRenderer[6];
                for (int i = 0; i < arcs.Length; i++) arcs[i] = CreateLine($"Body Arc {i + 1:00}", arcsRoot, materials["LightningCore"], 30 + i);
                root.AddComponent<ThunderClapTargetReactionVFX>().ConfigureAuthoring(new[] { flash, foot, bands, sparks }, arcs);
                return SavePrefab(root, TargetReactionPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateAftermathPrefab(IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ThunderClapAftermathVFX");
            try
            {
                ParticleSystem rolling = CreateParticle("Rolling Aftermath Dust", root.transform, materials["HeavyDust"], true, 2, 2, 0.25f, 128, 3);
                ParticleSystem suspended = CreateParticle("Suspended Aftermath Dust", root.transform, materials["FineDust"], true, 2, 2, 0f, 192, 4);
                ParticleSystem debris = CreateParticle("Settling Rock Debris", root.transform, materials["Rocks"], true, 4, 4, -1f, 64, 5);
                ParticleSystem smoke = CreateParticle("Settling Dark Smoke", root.transform, materials["Smoke"], true, 2, 2, 0.5f, 48, 6);
                ParticleSystem flickers = CreateParticle("Residual Ground Flickers", root.transform, materials["Sparks"], true, 4, 4, -1f, 48, 12);
                ConfigureMain(rolling, 1.7f, 1f, 0.14f);
                ConfigureMain(suspended, 2.1f, 1f, -0.04f);
                ConfigureDebris(debris, 1.7f, 0.9f, 0.38f);
                ConfigureMain(smoke, 1.8f, 1f, -0.06f);
                ConfigureMain(flickers, 0.34f, 1f, 0.05f);
                ConfigureFadeAndScale(rolling, 0.62f, 1.1f, 1.48f, 0.05f, 0.62f);
                ConfigureFadeAndScale(suspended, 0.48f, 1f, 1.62f, 0.08f, 0.65f);
                ConfigureFadeAndScale(smoke, 0.58f, 1f, 1.5f, 0.08f, 0.58f);
                ConfigureFadeAndScale(flickers, 0.8f, 1f, 0.05f, 0f, 0.45f);
                Transform arcRoot = CreateSection("Residual Electrical Ground Arcs", root.transform);
                LineRenderer[] arcs = new LineRenderer[3];
                for (int i = 0; i < arcs.Length; i++) arcs[i] = CreateLine($"Residual Arc {i + 1:00}", arcRoot, materials["GroundElectricity"], 14 + i);
                root.AddComponent<ThunderClapAftermathVFX>().ConfigureAuthoring(new[] { rolling, suspended, debris, smoke, flickers }, arcs);
                return SavePrefab(root, AftermathPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateCompletePrefab(
            ThunderClapVFXProfile profile,
            GameObject castPrefab,
            GameObject impactPrefab,
            GameObject shockwavePrefab,
            GameObject targetReactionPrefab,
            GameObject aftermathPrefab)
        {
            GameObject root = new("ThunderClapVFX");
            try
            {
                ThunderClapCastVFX cast = InstantiatePhase<ThunderClapCastVFX>(castPrefab, root.transform, "Cast Anticipation");
                ThunderClapImpactVFX impact = InstantiatePhase<ThunderClapImpactVFX>(impactPrefab, root.transform, "Ground Impact");
                ThunderClapShockwaveVFX shockwave = InstantiatePhase<ThunderClapShockwaveVFX>(shockwavePrefab, root.transform, "Expanding Shockwave");
                ThunderClapAftermathVFX aftermath = InstantiatePhase<ThunderClapAftermathVFX>(aftermathPrefab, root.transform, "Battlefield Aftermath");
                Transform poolRoot = CreateSection("Pooled Target Reactions", root.transform);
                ThunderClapTargetReactionVFX[] pool = new ThunderClapTargetReactionVFX[TargetPoolSize];
                for (int i = 0; i < pool.Length; i++)
                {
                    pool[i] = InstantiatePhase<ThunderClapTargetReactionVFX>(targetReactionPrefab, poolRoot, $"Target Reaction {i + 1:00}");
                }

                root.AddComponent<ThunderClapVFX>().ConfigureAuthoring(profile, cast, impact, shockwave, aftermath, pool);
                ThunderClapVFXPackage package = root.AddComponent<ThunderClapVFXPackage>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, CompletePrefabPath);
                package.Configure(profile, castPrefab, impactPrefab, shockwavePrefab, targetReactionPrefab, aftermathPrefab, saved);
                saved = PrefabUtility.SaveAsPrefabAsset(root, CompletePrefabPath);
                if (saved == null)
                {
                    throw new UnityException($"Failed to save Thunder Clap complete prefab at {CompletePrefabPath}.");
                }

                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static T InstantiatePhase<T>(GameObject prefab, Transform parent, string name) where T : Component
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            T component = instance.GetComponent<T>();
            if (component == null)
            {
                throw new MissingComponentException($"Phase prefab '{prefab.name}' is missing {typeof(T).Name}.");
            }

            instance.SetActive(false);
            return component;
        }

        private static void WireDefinition(GameObject complete)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null)
            {
                throw new MissingReferenceException("The existing Warrior Thunderclap ability or VFX definition is missing.");
            }

            definition.Configure(
                null,
                complete,
                null,
                false,
                false,
                false,
                false,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                0f,
                true);
            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(ability);
        }

        private static ParticleSystem CreateRing(string name, Transform parent, Material material, float fixedFrame, int order)
        {
            ParticleSystem system = CreateParticle(name, parent, material, true, 2, 2, fixedFrame, 4, order, ParticleSystemRenderMode.HorizontalBillboard);
            ConfigureMain(system, 0.42f, 1f, 0f);
            ConfigureFadeAndScale(system, 0.055f, 0.78f, 1f, 0f, 0.48f);
            return system;
        }

        private static ParticleSystem CreateParticle(
            string name,
            Transform parent,
            Material material,
            bool worldSpace,
            int columns,
            int rows,
            float fixedFrame,
            int maxParticles,
            int order,
            ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem system = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 2.5f;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 1f;
            main.startColor = Color.white;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.sortingOrder = order;
            renderer.enableGPUInstancing = true;
            if (renderMode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.18f;
                renderer.lengthScale = 1.8f;
            }

            ConfigureAtlas(system, columns, rows, fixedFrame);
            return system;
        }

        private static LineRenderer CreateLine(string name, Transform parent, Material material, int order)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = true;
            line.sharedMaterial = material;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 1;
            line.numCapVertices = 0;
            line.positionCount = 2;
            line.widthMultiplier = 0.08f;
            line.sortingOrder = order;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static void ConfigureMain(ParticleSystem system, float lifetime, float size, float gravity)
        {
            ParticleSystem.MainModule main = system.main;
            main.startLifetime = Mathf.Max(0.04f, lifetime);
            main.startSize = Mathf.Max(0.01f, size);
            main.gravityModifier = gravity;
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.16f;
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.22f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
        }

        private static void ConfigureDebris(ParticleSystem system, float lifetime, float gravity, float bounce)
        {
            ConfigureMain(system, lifetime, 1f, gravity);
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.bounce = bounce;
            collision.dampen = 0.48f;
            collision.lifetimeLoss = 0.18f;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.25f;
            drag.limit = 7f;
        }

        private static void ConfigureFadeAndScale(
            ParticleSystem system,
            float startScale,
            float middleScale,
            float endScale,
            float fadeInEnd,
            float fadeOutStart)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(fadeInEnd > 0f ? 0f : 1f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp01(fadeInEnd)),
                    new GradientAlphaKey(1f, Mathf.Clamp01(fadeOutStart)),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, startScale),
                new Keyframe(0.52f, middleScale),
                new Keyframe(1f, endScale)));
        }

        private static void ConfigureAtlas(ParticleSystem system, int columns, int rows, float fixedFrame)
        {
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = columns > 1 || rows > 1;
            if (!sheet.enabled)
            {
                return;
            }

            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = Mathf.Max(1, columns);
            sheet.numTilesY = Mathf.Max(1, rows);
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = fixedFrame >= 0f
                ? new ParticleSystem.MinMaxCurve(Mathf.Clamp01(fixedFrame))
                : new ParticleSystem.MinMaxCurve(0f, 0.999f);
        }

        private static Material CreateMaterial(
            string name,
            Shader shader,
            string texturePath,
            string noisePath,
            Color tint,
            bool additive,
            float brightness,
            float flickerSpeed = 0f,
            float flickerAmount = 0f)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.enableInstancing = true;
            SetTexture(material, "_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            SetTexture(material, "_NoiseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath));
            SetColor(material, "_Tint", tint);
            SetFloat(material, "_Opacity", tint.a);
            SetFloat(material, "_Brightness", brightness);
            SetFloat(material, "_NoiseStrength", additive ? 0.08f : 0.16f);
            SetFloat(material, "_FlickerSpeed", flickerSpeed);
            SetFloat(material, "_FlickerAmount", flickerAmount);
            SetFloat(material, "_PulseSpeed", additive ? 10f : 1.5f);
            SetFloat(material, "_PulseAmount", additive ? 0.08f : 0.03f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateDistortionMaterial(Shader shader, string ringPath, string noisePath)
        {
            const string name = "ThunderClap_Distortion";
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.enableInstancing = true;
            SetTexture(material, "_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(ringPath));
            SetTexture(material, "_DistortionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath));
            SetColor(material, "_Tint", new Color(0.72f, 0.9f, 1f, 0.24f));
            SetFloat(material, "_Opacity", 0.22f);
            SetFloat(material, "_Brightness", 0.8f);
            SetFloat(material, "_DistortionStrength", 0.014f);
            material.renderQueue = (int)RenderQueue.Transparent + 20;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTextureImporters()
        {
            foreach (string path in RuntimeTexturePaths())
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                bool repeat = path.EndsWith("ThunderClap_LightningCore.png", StringComparison.Ordinal)
                    || path.EndsWith("ThunderClap_HeatDistortion.png", StringComparison.Ordinal);
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = path.EndsWith("ThunderClap_HeatDistortion.png", StringComparison.Ordinal)
                    ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = importer.alphaSource != TextureImporterAlphaSource.None;
                importer.sRGBTexture = !path.EndsWith("ThunderClap_HeatDistortion.png", StringComparison.Ordinal);
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static IEnumerable<string> RuntimeTexturePaths()
        {
            yield return TextureFolder + "/ThunderClap_HeavyDustAtlas.png";
            yield return TextureFolder + "/ThunderClap_FineDustSmokeAtlas.png";
            yield return TextureFolder + "/ThunderClap_DebrisAtlas.png";
            yield return TextureFolder + "/ThunderClap_GroundRingsAtlas.png";
            yield return TextureFolder + "/ThunderClap_ElectricalAtlas.png";
            yield return TextureFolder + "/ThunderClap_ElectricalSparksAtlas.png";
            yield return TextureFolder + "/ThunderClap_LightningCore.png";
            yield return TextureFolder + "/ThunderClap_HeatDistortion.png";
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null)
            {
                throw new UnityException($"Failed to save prefab at {path}.");
            }

            return saved;
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX", "ThunderClap");
            EnsureFolder(Root, "Textures");
            EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Profiles");
            EnsureFolder(Root, "Prefabs");
            EnsureFolder(Root, "Documentation");
            EnsureFolder(Root, "Shaders");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property)) material.SetTexture(property, texture);
        }

        private static void SetColor(Material material, string property, Color color)
        {
            if (material.HasProperty(property)) material.SetColor(property, color);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
