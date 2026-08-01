#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using RPGClone.Abilities;
using RPGClone.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class PressTheAttackVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/PressTheAttack";
        private const string TextureFolder = Root + "/Textures";
        private const string AtlasFolder = TextureFolder + "/Atlases";
        private const string SurfaceTextureFolder = TextureFolder + "/SurfaceV2";
        private const string MaterialFolder = Root + "/Materials";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string ProfilePath = Root + "/Profiles/PressTheAttackVFX_Default.asset";
        private const string DefinitionPath = Root + "/PressTheAttack_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Press_The_Attack.asset";
        private const string CombinedPrefabPath = PrefabFolder + "/PressTheAttackVFX.prefab";
        private const string ActivationPrefabPath = PrefabFolder + "/PressTheAttackActivationVFX.prefab";
        private const string PersistentPrefabPath = PrefabFolder + "/PressTheAttackPersistentCharacterVFX.prefab";
        private const string MovementPrefabPath = PrefabFolder + "/PressTheAttackMovementAccentVFX.prefab";
        private const string AttackPrefabPath = PrefabFolder + "/PressTheAttackAttackAccentVFX.prefab";
        private const string HitPrefabPath = PrefabFolder + "/PressTheAttackConfirmedHitAccentVFX.prefab";

        [MenuItem("Tools/RPG Clone/VFX/Install Press the Attack VFX")]
        public static void Install()
        {
            ConfigureTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            PressTheAttackVFXProfile profile = GetOrCreateProfile();
            Dictionary<string, Material> materials = CreateMaterials(profile);
            GameObject movementPrefab = CreateMovementAccentPrefab(materials["MovementStreak"]);
            GameObject attackPrefab = CreateAttackAccentPrefab(materials["AttackAccent"]);
            GameObject hitPrefab = CreateConfirmedHitAccentPrefab(materials["ConfirmedHit"]);
            GameObject persistentPrefab = CreatePersistentPrefab(materials, movementPrefab, attackPrefab, hitPrefab);
            GameObject activationPrefab = CreateActivationPrefab(materials, profile);
            GameObject combinedPrefab = CreateCombinedPrefab(
                profile,
                materials,
                activationPrefab,
                persistentPrefab);
            CreateActivationOnlyPrefab(combinedPrefab);
            MMOAbilityVfxDefinition definition = ConfigureDefinition(combinedPrefab);
            ConfigureAbility(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Press the Attack VFX V2 installed: dense charged-rage surface material, separated conforming shells, crawling electricity, and replicated presentation are ready.");
        }

        private static PressTheAttackVFXProfile GetOrCreateProfile()
        {
            PressTheAttackVFXProfile profile = AssetDatabase.LoadAssetAtPath<PressTheAttackVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PressTheAttackVFXProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.ConfigureChargedRageDefaults();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials(PressTheAttackVFXProfile profile)
        {
            Shader rageShader = Shader.Find("RPG Clone/VFX/Press the Attack/Rage Overlay");
            Shader lightningShader = Shader.Find("RPG Clone/VFX/Press the Attack/Surface Lightning");
            Shader edgeShader = Shader.Find("RPG Clone/VFX/Press the Attack/Edge Streak");
            Shader particleShader = Shader.Find("RPG Clone/VFX/Press the Attack/Particle Unlit");
            if (rageShader == null || lightningShader == null || edgeShader == null || particleShader == null)
            {
                throw new InvalidOperationException("Press the Attack shaders must compile before installation.");
            }

            Material rage = CreateMaterial("PressTheAttack_CharacterRageOverlay", rageShader);
            SetTexture(rage, "_ChargeColorTex", LoadSurfaceTexture("PressTheAttack_RedChargeColor_V2.png"));
            SetTexture(rage, "_VeinMask", LoadSurfaceTexture("PressTheAttack_RageVeinNetwork_V2.png"));
            SetTexture(rage, "_FlowMask", LoadSurfaceTexture("PressTheAttack_DirectionalRageFlow_V2.png"));
            SetTexture(rage, "_BreakupMask", LoadSurfaceTexture("PressTheAttack_RageBreakup_V2.png"));
            SetTexture(rage, "_DistortionMap", LoadTexture("PressTheAttack_SoftDistortionNoise.png"));

            Material lightning = CreateMaterial("PressTheAttack_ShrinkWrappedLightning", lightningShader);
            SetTexture(lightning, "_LightningMaskA", LoadSurfaceTexture("PressTheAttack_CrawlingLightning_V2.png"));
            SetTexture(lightning, "_LightningMaskB", LoadSurfaceTexture("PressTheAttack_RageVeinNetwork_V2.png"));
            SetTexture(lightning, "_FlowMask", LoadSurfaceTexture("PressTheAttack_DirectionalRageFlow_V2.png"));
            SetTexture(lightning, "_DistortionMap", LoadTexture("PressTheAttack_SoftDistortionNoise.png"));
            SetTexture(lightning, "_BreakupMask", LoadSurfaceTexture("PressTheAttack_RageBreakup_V2.png"));

            Material edge = CreateMaterial("PressTheAttack_SilhouetteAndSurfaceStreaks", edgeShader);
            SetTexture(edge, "_StreakMask", LoadSurfaceTexture("PressTheAttack_DirectionalRageFlow_V2.png"));
            SetTexture(edge, "_SlashMask", LoadSurfaceTexture("PressTheAttack_CrawlingLightning_V2.png"));
            SetTexture(edge, "_EdgeBreakupMask", LoadSurfaceTexture("PressTheAttack_RageBreakup_V2.png"));
            SetTexture(edge, "_DistortionMap", LoadTexture("PressTheAttack_DistortedBodyEnergyNoise.png"));

            foreach (Material material in new[] { rage, lightning, edge })
            {
                SetColor(material, "_MainTint", profile.MainCrimson);
                SetColor(material, "_DarkTint", profile.DarkRed);
                SetColor(material, "_HighlightTint", profile.Highlight);
                SetFloat(material, "_EmissionIntensity", profile.PersistentOverlayIntensity);
                SetFloat(material, "_SurfaceLift", profile.SurfaceLift);
                SetFloat(material, "_PatternScale", profile.SurfacePatternScale);
                SetFloat(material, "_PulseSpeed", profile.SurfacePulseSpeed);
                SetFloat(material, "_TravelSpeed", profile.TravellingPulseSpeed);
                SetFloat(material, "_UndercoatIntensity", profile.RageUndercoatIntensity);
                EditorUtility.SetDirty(material);
            }

            Dictionary<string, Material> result = new()
            {
                ["RageOverlay"] = rage,
                ["LightningOverlay"] = lightning,
                ["EdgeOverlay"] = edge,
                ["GroundPressure"] = CreateParticleMaterial("PressTheAttack_GroundPressureRing", particleShader, LoadTexture("PressTheAttack_GroundPressureRing.png"), profile.DarkRed, 1.4f),
                ["ThinGroundRing"] = CreateParticleMaterial("PressTheAttack_ThinCrimsonGroundRing", particleShader, LoadTexture("PressTheAttack_ThinCrimsonGroundRing.png"), Color.white, 2.1f),
                ["GroundMarks"] = CreateParticleMaterial("PressTheAttack_BrokenGroundImpactMarks", particleShader, LoadTexture("PressTheAttack_BrokenGroundImpactMarks.png"), Color.white, 1.5f),
                ["SurfaceSpark"] = CreateParticleMaterial("PressTheAttack_SurfaceSparks", particleShader, LoadTexture("PressTheAttack_RedSpark.png"), Color.white, 2.4f),
                ["MovementStreak"] = CreateParticleMaterial("PressTheAttack_MovementStreaks", particleShader, LoadTexture("PressTheAttack_SurfaceEmissionStreak.png"), Color.white, 1.9f),
                ["AttackAccent"] = CreateParticleMaterial("PressTheAttack_AttackAccent", particleShader, LoadTexture("PressTheAttack_AttackArmAccent.png"), Color.white, 2.3f),
                ["ConfirmedHit"] = CreateParticleMaterial("PressTheAttack_ConfirmedHitAccent", particleShader, LoadTexture("PressTheAttack_ConfirmedHitAccent.png"), Color.white, 2.7f),
                ["RageBurstAtlas"] = CreateParticleMaterial("PressTheAttack_ActivationRageBurst", particleShader, LoadAtlas("PressTheAttack_ActivationRageBurst.png"), Color.white, 1.5f),
                ["ElectricalAtlas"] = CreateParticleMaterial("PressTheAttack_RedElectricalSnap", particleShader, LoadAtlas("PressTheAttack_RedElectricalSnap.png"), Color.white, 2.1f),
                ["BodyPulseAtlas"] = CreateParticleMaterial("PressTheAttack_BodyEnergyPulse", particleShader, LoadAtlas("PressTheAttack_BodyEnergyPulse.png"), Color.white, 1.8f),
                ["ImpactAtlas"] = CreateParticleMaterial("PressTheAttack_CrimsonImpactFlash", particleShader, LoadAtlas("PressTheAttack_CrimsonImpactFlash.png"), Color.white, 1.8f),
                ["GroundShockAtlas"] = CreateParticleMaterial("PressTheAttack_GroundShockBurst", particleShader, LoadAtlas("PressTheAttack_GroundShockBurst.png"), Color.white, 1.45f),
                ["VaporAtlas"] = CreateParticleMaterial("PressTheAttack_DissipatingRedVapor", particleShader, LoadAtlas("PressTheAttack_DissipatingRedVapor.png"), Color.white, 0.85f),
                ["FastStreakAtlas"] = CreateParticleMaterial("PressTheAttack_FastRedStreakBurst", particleShader, LoadAtlas("PressTheAttack_FastRedStreakBurst.png"), Color.white, 1.9f)
            };
            return result;
        }

        private static Material CreateMaterial(string name, Shader shader)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            return material;
        }

        private static Material CreateParticleMaterial(string name, Shader shader, Texture texture, Color tint, float emission)
        {
            Material material = CreateMaterial(name, shader);
            SetTexture(material, "_MainTex", texture);
            SetColor(material, "_Tint", tint);
            SetFloat(material, "_EmissionIntensity", emission);
            SetFloat(material, "_Opacity", 1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateMovementAccentPrefab(Material material)
        {
            GameObject root = new("Press the Attack Movement Accent");
            ParticleSystem particles = CreatePersistentParticles(
                "Foot and Lower Torso Backward Streaks",
                root.transform,
                material,
                0.3f,
                new Vector2(0.12f, 0.28f),
                new Vector2(1.5f, 3.2f),
                new Vector3(0f, 0.45f, -1.8f),
                true);
            ConfigureStretchRenderer(particles, 1.3f, 0.14f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MovementPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateAttackAccentPrefab(Material material)
        {
            GameObject root = new("Press the Attack Attack Accent");
            ParticleSystem particles = CreateManualBurstParticles(
                "Compact Attacking Arm Streak",
                root.transform,
                material,
                0.24f,
                new Vector2(0.28f, 0.5f),
                ParticleSystemSimulationSpace.Local);
            ConfigureStretchRenderer(particles, 1.7f, 0.18f);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AttackPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateConfirmedHitAccentPrefab(Material material)
        {
            GameObject root = new("Press the Attack Confirmed Hit Accent");
            ParticleSystem particles = CreateManualBurstParticles(
                "Compact Confirmed Hit Flash",
                root.transform,
                material,
                0.22f,
                new Vector2(0.38f, 0.62f),
                ParticleSystemSimulationSpace.World);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HitPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreatePersistentPrefab(
            IReadOnlyDictionary<string, Material> materials,
            GameObject movementPrefab,
            GameObject attackPrefab,
            GameObject hitPrefab)
        {
            GameObject root = new("Press the Attack Persistent Character Effect");
            CreateSection("Character Overlay", root.transform);
            CreateSection("Surface Lightning", root.transform);
            CreateSection("Silhouette Glow", root.transform);
            CreateSection("Surface Streaks", root.transform);

            Transform sparksRoot = CreateSection("Surface Sparks", root.transform);
            CreatePersistentParticles(
                "Restrained Surface Sparks",
                sparksRoot,
                materials["SurfaceSpark"],
                0.42f,
                new Vector2(0.055f, 0.13f),
                new Vector2(0.35f, 0.9f),
                new Vector3(0f, 0.48f, -0.2f),
                false);

            GameObject movement = (GameObject)PrefabUtility.InstantiatePrefab(movementPrefab);
            movement.name = "Movement Response";
            movement.transform.SetParent(root.transform, false);
            GameObject attack = (GameObject)PrefabUtility.InstantiatePrefab(attackPrefab);
            attack.name = "Attack Response";
            attack.transform.SetParent(root.transform, false);
            GameObject hit = (GameObject)PrefabUtility.InstantiatePrefab(hitPrefab);
            hit.name = "Confirmed Hit Response";
            hit.transform.SetParent(root.transform, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PersistentPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateActivationPrefab(
            IReadOnlyDictionary<string, Material> materials,
            PressTheAttackVFXProfile profile)
        {
            GameObject root = new("Press the Attack Activation Effect");
            Transform ground = CreateSection("Ground Burst", root.transform);
            CreateQuad("Broad Dark-red Pressure Ring", ground, materials["GroundPressure"], 28);
            CreateQuad("Thin Bright Crimson Ring", ground, materials["ThinGroundRing"], 29);
            CreateQuad("Broken Ground Impact Marks", ground, materials["GroundMarks"], 27);

            Transform body = CreateSection("Body Surge", root.transform);
            CreateFlipbookParticles("Activation Rage Burst", body, materials["RageBurstAtlas"], 0f, 0.62f, 1.25f, 2, ParticleSystemSimulationSpace.Local);
            CreateFlipbookParticles("Red Electrical Snap", body, materials["ElectricalAtlas"], 0.08f, 0.48f, 1.05f, 2, ParticleSystemSimulationSpace.Local);
            CreateFlipbookParticles("Body Energy Pulse", body, materials["BodyPulseAtlas"], 0.04f, 0.72f, 1.45f, 1, ParticleSystemSimulationSpace.Local);
            CreateFlipbookParticles("Crimson Impact Flash", body, materials["ImpactAtlas"], 0f, 0.5f, 0.9f, 1, ParticleSystemSimulationSpace.Local);
            CreateFlipbookParticles("Fast Red Streak Burst", body, materials["FastStreakAtlas"], 0.08f, 0.58f, 1.1f, 3, ParticleSystemSimulationSpace.Local);

            Transform world = CreateSection("World-locked Dissipation", root.transform);
            CreateFlipbookParticles("Ground Shock Burst", world, materials["GroundShockAtlas"], 0.02f, 0.7f, 1.65f, 1, ParticleSystemSimulationSpace.World);
            CreateFlipbookParticles("Dissipating Red Vapor", world, materials["VaporAtlas"], 0.2f, 0.86f, 0.62f, 2, ParticleSystemSimulationSpace.World);

            GameObject lightObject = new("Restrained Activation Light");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 1f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = profile.MainCrimson;
            light.intensity = profile.OptionalLightIntensity;
            light.range = 3.2f;
            light.shadows = LightShadows.None;
            light.enabled = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ActivationPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCombinedPrefab(
            PressTheAttackVFXProfile profile,
            IReadOnlyDictionary<string, Material> materials,
            GameObject activationPrefab,
            GameObject persistentPrefab)
        {
            GameObject root = new("Press the Attack VFX");
            MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
            poolable.ConfigureAuthoring(6);
            PressTheAttackVFX controller = root.AddComponent<PressTheAttackVFX>();
            Transform effectController = CreateSection("Effect Controller", root.transform);
            Transform dynamicOverlay = CreateSection("Dynamic Character-conforming Shells", effectController);

            GameObject activation = (GameObject)PrefabUtility.InstantiatePrefab(activationPrefab);
            activation.name = "Activation Effect";
            activation.transform.SetParent(root.transform, false);
            GameObject persistent = (GameObject)PrefabUtility.InstantiatePrefab(persistentPrefab);
            persistent.name = "Persistent Character Effect";
            persistent.transform.SetParent(root.transform, false);

            Transform groundRoot = activation.transform.Find("Ground Burst");
            Transform bodyRoot = activation.transform.Find("Body Surge");
            Transform worldRoot = activation.transform.Find("World-locked Dissipation");
            ParticleSystem surfaceSparks = persistent.transform.Find("Surface Sparks/Restrained Surface Sparks").GetComponent<ParticleSystem>();
            ParticleSystem movement = persistent.transform.Find("Movement Response/Foot and Lower Torso Backward Streaks").GetComponent<ParticleSystem>();
            ParticleSystem attack = persistent.transform.Find("Attack Response/Compact Attacking Arm Streak").GetComponent<ParticleSystem>();
            ParticleSystem confirmedHit = persistent.transform.Find("Confirmed Hit Response/Compact Confirmed Hit Flash").GetComponent<ParticleSystem>();

            controller.ConfigureAuthoring(
                profile,
                materials["RageOverlay"],
                materials["LightningOverlay"],
                materials["EdgeOverlay"],
                dynamicOverlay,
                groundRoot,
                groundRoot.Find("Broad Dark-red Pressure Ring").GetComponent<Renderer>(),
                groundRoot.Find("Thin Bright Crimson Ring").GetComponent<Renderer>(),
                groundRoot.Find("Broken Ground Impact Marks").GetComponent<Renderer>(),
                bodyRoot.Find("Activation Rage Burst").GetComponent<ParticleSystem>(),
                bodyRoot.Find("Red Electrical Snap").GetComponent<ParticleSystem>(),
                bodyRoot.Find("Body Energy Pulse").GetComponent<ParticleSystem>(),
                bodyRoot.Find("Crimson Impact Flash").GetComponent<ParticleSystem>(),
                worldRoot.Find("Ground Shock Burst").GetComponent<ParticleSystem>(),
                worldRoot.Find("Dissipating Red Vapor").GetComponent<ParticleSystem>(),
                bodyRoot.Find("Fast Red Streak Burst").GetComponent<ParticleSystem>(),
                activation.transform.Find("Restrained Activation Light").GetComponent<Light>(),
                surfaceSparks,
                movement,
                attack,
                confirmedHit);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CombinedPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void CreateActivationOnlyPrefab(GameObject combinedPrefab)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(combinedPrefab);
            instance.name = "Press the Attack Activation VFX";
            PressTheAttackVFX controller = instance.GetComponent<PressTheAttackVFX>();
            controller.ConfigureActivationOnly(true);
            Transform persistent = instance.transform.Find("Persistent Character Effect");
            if (persistent != null) persistent.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabFolder + "/PressTheAttackActivationOnlyVFX.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static MMOAbilityVfxDefinition ConfigureDefinition(GameObject combinedPrefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOAbilityVfxDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.Configure(
                null,
                combinedPrefab,
                null,
                true,
                false,
                true,
                false,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                Vector3.zero,
                0f,
                true);
            definition.ConfigureCasterBounce(0f, 0f);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void ConfigureAbility(MMOAbilityVfxDefinition definition)
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null)
            {
                throw new FileNotFoundException("Press the Attack ability is missing.", AbilityPath);
            }

            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(ability);
        }

        private static ParticleSystem CreateFlipbookParticles(
            string name,
            Transform parent,
            Material material,
            float delay,
            float lifetime,
            float size,
            int count,
            ParticleSystemSimulationSpace simulationSpace)
        {
            GameObject gameObject = new(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = name.Contains("Ground") ? new Vector3(0f, 0.04f, 0f) : new Vector3(0f, 1f, 0f);
            ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = Mathf.Max(0.05f, delay + lifetime);
            main.startLifetime = lifetime;
            main.startSpeed = name.Contains("Vapor") ? new ParticleSystem.MinMaxCurve(0.12f, 0.35f) : 0f;
            main.startSize = size;
            main.startColor = Color.white;
            main.simulationSpace = simulationSpace;
            main.maxParticles = Mathf.Max(4, count * 2);
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(delay, (short)count) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = 4;
            sheet.numTilesY = 4;
            sheet.cycleCount = 1;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static ParticleSystem CreatePersistentParticles(
            string name,
            Transform parent,
            Material material,
            float lifetime,
            Vector2 size,
            Vector2 speed,
            Vector3 velocity,
            bool stretched)
        {
            GameObject gameObject = new(name);
            gameObject.transform.SetParent(parent, false);
            ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = lifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startColor = Color.white;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 24;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = Vector3.one;
            ParticleSystem.VelocityOverLifetimeModule velocityModule = particles.velocityOverLifetime;
            velocityModule.enabled = true;
            velocityModule.space = ParticleSystemSimulationSpace.Local;
            velocityModule.x = velocity.x;
            velocityModule.y = velocity.y;
            velocityModule.z = velocity.z;
            ConfigureFadeAndShrink(particles);

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static ParticleSystem CreateManualBurstParticles(
            string name,
            Transform parent,
            Material material,
            float lifetime,
            Vector2 size,
            ParticleSystemSimulationSpace simulationSpace)
        {
            GameObject gameObject = new(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            ParticleSystem particles = gameObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.3f;
            main.startLifetime = lifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
            main.startColor = Color.white;
            main.simulationSpace = simulationSpace;
            main.maxParticles = 12;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;
            ConfigureFadeAndShrink(particles);
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static void ConfigureFadeAndShrink(ParticleSystem particles)
        {
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.45f, 1f, 0f));
        }

        private static void ConfigureStretchRenderer(ParticleSystem particles, float lengthScale, float velocityScale)
        {
            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = lengthScale;
            renderer.velocityScale = velocityScale;
            renderer.cameraVelocityScale = 0f;
        }

        private static Renderer CreateQuad(string name, Transform parent, Material material, int sortingOrder)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return renderer;
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static void ConfigureTextures()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in Directory.GetFiles(TextureFolder, "PressTheAttack_*.png", SearchOption.TopDirectoryOnly))
            {
                string assetPath = path.Replace('\\', '/');
                bool master = assetPath.EndsWith("_Master.png", StringComparison.Ordinal);
                ConfigureTextureImporter(assetPath, !master, !master, true, master ? 1024 : 256);
            }

            foreach (string path in Directory.GetFiles(AtlasFolder, "PressTheAttack_*.png", SearchOption.TopDirectoryOnly))
            {
                ConfigureTextureImporter(path.Replace('\\', '/'), false, false, false, 1024);
            }

            string sourceFolder = TextureFolder + "/Sources";
            foreach (string path in Directory.GetFiles(sourceFolder, "PressTheAttack_*.png", SearchOption.TopDirectoryOnly))
            {
                ConfigureTextureImporter(path.Replace('\\', '/'), false, false, false, 2048);
            }

            foreach (string path in Directory.GetFiles(SurfaceTextureFolder, "PressTheAttack_*.png", SearchOption.TopDirectoryOnly))
            {
                ConfigureTextureImporter(path.Replace('\\', '/'), false, true, true, 1024);
            }
        }

        private static void ConfigureTextureImporter(
            string path,
            bool alphaIsTransparency,
            bool repeat,
            bool mipmaps,
            int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("Required Press the Attack texture is missing.", path);
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = alphaIsTransparency;
            importer.isReadable = false;
            importer.mipmapEnabled = mipmaps;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = maxSize;
            importer.SaveAndReimport();
        }

        private static Texture2D LoadTexture(string fileName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + fileName);
            if (texture == null) throw new FileNotFoundException("Press the Attack texture is missing.", fileName);
            return texture;
        }

        private static Texture2D LoadAtlas(string fileName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasFolder + "/" + fileName);
            if (texture == null) throw new FileNotFoundException("Press the Attack atlas is missing.", fileName);
            return texture;
        }

        private static Texture2D LoadSurfaceTexture(string fileName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SurfaceTextureFolder + "/" + fileName);
            if (texture == null) throw new FileNotFoundException("Press the Attack surface texture is missing.", fileName);
            return texture;
        }

        private static void SetTexture(Material material, string property, Texture value)
        {
            if (material.HasProperty(property)) material.SetTexture(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
#endif
