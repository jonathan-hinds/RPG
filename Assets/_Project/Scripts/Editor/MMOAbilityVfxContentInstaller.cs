using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class MMOAbilityVfxContentInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string DefinitionFolder = RootFolder + "/Definitions";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string SoftParticleTexturePath = TextureFolder + "/VFX_Soft_Radial.png";
        private const string AbilityFolder = "Assets/_Project/Configs/Abilities";
        private const string HealingBeamPrefabPath = "Assets/_Project/VFX/HealingBeam/Prefabs/HealingBeamVFX.prefab";
        private const string HealingBeamChargePrefabPath = "Assets/_Project/VFX/HealingBeam/Prefabs/HealingBeamChargeVFX.prefab";
        private const string BashPrefabPath = "Assets/_Project/VFX/Bash/Prefabs/BashVFX.prefab";
        private const string BerzerkitisPrefabPath = "Assets/_Project/VFX/Berzerkitis/Prefabs/BerzerkitisVFX.prefab";

        [MenuItem("Tools/RPG Clone/VFX/Install Ability VFX Content")]
        public static void InstallAbilityVfxContent()
        {
            EnsureFolders();
            Texture2D softParticleTexture = CreateOrUpdateSoftParticleTexture();
            Dictionary<string, Material> particleMaterials = CreateParticleMaterials();
            Dictionary<string, Material> lineMaterials = CreateLineMaterials();
            ApplyParticleTexture(particleMaterials, softParticleTexture);

            VfxPrefabs prefabs = CreatePrefabs(particleMaterials, lineMaterials);
            Dictionary<string, MMOAbilityVfxDefinition> definitions = CreateDefinitions(prefabs);
            AssignDefinitions(definitions);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Ability VFX content installed and assigned to existing ability assets.");
        }

        private static Dictionary<string, Material> CreateParticleMaterials()
        {
            return new Dictionary<string, Material>
            {
                ["Fire"] = CreateMaterial("VFX_Fire_Additive", new Color(1f, 0.34f, 0.05f, 0.82f), true),
                ["Frost"] = CreateMaterial("VFX_Frost_Additive", new Color(0.42f, 0.86f, 1f, 0.78f), true),
                ["Nature"] = CreateMaterial("VFX_Nature_Additive", new Color(0.35f, 0.95f, 0.28f, 0.76f), true),
                ["Holy"] = CreateMaterial("VFX_Holy_Additive", new Color(1f, 0.92f, 0.36f, 0.82f), true),
                ["Arcane"] = CreateMaterial("VFX_Arcane_Additive", new Color(0.72f, 0.38f, 1f, 0.78f), true),
                ["Water"] = CreateMaterial("VFX_Water_Additive", new Color(0.25f, 0.72f, 1f, 0.72f), true),
                ["Physical"] = CreateMaterial("VFX_Physical_Additive", new Color(0.9f, 0.76f, 0.48f, 0.72f), true),
                ["Blood"] = CreateMaterial("VFX_Blood_Additive", new Color(1f, 0.12f, 0.04f, 0.78f), true),
                ["Earth"] = CreateMaterial("VFX_Earth_Additive", new Color(0.76f, 0.48f, 0.22f, 0.7f), true)
            };
        }

        private static Dictionary<string, Material> CreateLineMaterials()
        {
            return new Dictionary<string, Material>
            {
                ["Fire"] = CreateMaterial("VFX_Fire_Line", new Color(1f, 0.46f, 0.08f, 0.9f), false),
                ["Frost"] = CreateMaterial("VFX_Frost_Line", new Color(0.55f, 0.92f, 1f, 0.86f), false),
                ["Nature"] = CreateMaterial("VFX_Nature_Line", new Color(0.55f, 1f, 0.28f, 0.88f), false),
                ["Holy"] = CreateMaterial("VFX_Holy_Line", new Color(1f, 0.95f, 0.36f, 0.9f), false),
                ["Arcane"] = CreateMaterial("VFX_Arcane_Line", new Color(0.76f, 0.4f, 1f, 0.88f), false),
                ["Physical"] = CreateMaterial("VFX_Physical_Line", new Color(0.95f, 0.82f, 0.54f, 0.78f), false)
            };
        }

        private static VfxPrefabs CreatePrefabs(Dictionary<string, Material> particleMaterials, Dictionary<string, Material> lineMaterials)
        {
            VfxPrefabs prefabs = new()
            {
                FireCasting = CreateAuraPrefab("Fire_Casting_Aura", particleMaterials["Fire"], new Color(1f, 0.24f, 0f, 0.78f), new Color(1f, 0.82f, 0.16f, 0.42f), 0.26f, 96f, 0.075f),
                FrostCasting = CreateAuraPrefab("Frost_Casting_Aura", particleMaterials["Frost"], new Color(0.4f, 0.92f, 1f, 0.76f), new Color(0.82f, 1f, 1f, 0.36f), 0.25f, 84f, 0.065f),
                NatureCasting = CreateAuraPrefab("Nature_Casting_Aura", particleMaterials["Nature"], new Color(0.32f, 1f, 0.18f, 0.74f), new Color(1f, 0.92f, 0.24f, 0.36f), 0.28f, 98f, 0.068f),
                HolyCasting = CreateAuraPrefab("Holy_Casting_Aura", particleMaterials["Holy"], new Color(0.55f, 1f, 0.28f, 0.64f), new Color(1f, 0.92f, 0.22f, 0.48f), 0.3f, 104f, 0.075f),
                ArcaneCasting = CreateAuraPrefab("Arcane_Casting_Aura", particleMaterials["Arcane"], new Color(0.72f, 0.26f, 1f, 0.74f), new Color(0.35f, 0.72f, 1f, 0.34f), 0.25f, 90f, 0.064f),

                Fireball = CreateProjectilePrefab("Fireball_Projectile", particleMaterials["Fire"], new Color(1f, 0.28f, 0.03f, 0.86f), lineMaterials["Fire"], 18f, 0f, 0.075f, 0.18f),
                FireBurst = CreateBurstPrefab("Fire_Impact_Burst", particleMaterials["Fire"], new Color(1f, 0.22f, 0.02f, 0.86f), new Color(1f, 0.82f, 0.12f, 0.58f), 0.58f, 96, 0.62f, 0.07f),
                FireBlast = CreateBeamPrefab("Fire_Blast_Short", lineMaterials["Fire"], particleMaterials["Fire"], 0.18f, 7, 0.035f, 18f, true, 0.06f, 0.38f),
                Flamestrike = CreateColumnPrefab("Flamestrike_Column", particleMaterials["Fire"], new Color(1f, 0.22f, 0.02f, 0.8f), new Color(1f, 0.75f, 0.08f, 0.48f), 0.92f, 180, 1.05f),

                HealingBeam = AssetDatabase.LoadAssetAtPath<GameObject>(HealingBeamPrefabPath)
                    ?? CreateBeamPrefab("Healing_Beam", lineMaterials["Holy"], particleMaterials["Holy"], 0.48f, 9, 0.045f, 26f, true, 0.045f, 0.5f),
                HealingImpact = CreateAuraBurstPrefab("Healing_Target_Aura", particleMaterials["Holy"], new Color(0.5f, 1f, 0.25f, 0.58f), new Color(1f, 0.92f, 0.18f, 0.52f), 0.46f, 112, 0.82f),
                LightningBeam = CreateBeamPrefab("Lightning_Bolt_Beam", lineMaterials["Nature"], particleMaterials["Nature"], 0.36f, 10, 0.08f, 38f, true, 0.042f, 0.36f),
                LightningImpact = CreateBurstPrefab("Lightning_Impact", particleMaterials["Nature"], new Color(0.5f, 1f, 0.22f, 0.82f), new Color(1f, 0.96f, 0.24f, 0.58f), 0.5f, 88, 0.46f, 0.062f),
                FrostShock = CreateBeamPrefab("Frost_Shock_Bolt", lineMaterials["Frost"], particleMaterials["Frost"], 0.22f, 7, 0.04f, 20f, true, 0.04f, 0.32f),
                FrostImpact = CreateBurstPrefab("Frost_Impact", particleMaterials["Frost"], new Color(0.42f, 0.9f, 1f, 0.8f), new Color(0.9f, 1f, 1f, 0.52f), 0.48f, 82, 0.58f, 0.052f),
                WaterShield = CreateAuraBurstPrefab("Water_Shield_Aura", particleMaterials["Water"], new Color(0.22f, 0.66f, 1f, 0.54f), new Color(0.78f, 1f, 1f, 0.32f), 0.52f, 96, 1.05f),

                ArcaneMissile = CreateBeamPrefab("Arcane_Missile_Channel", lineMaterials["Arcane"], particleMaterials["Arcane"], 5f, 9, 0.055f, 24f, false, 0.042f, 0.58f),
                ArcaneImpact = CreateBurstPrefab("Arcane_Impact", particleMaterials["Arcane"], new Color(0.72f, 0.32f, 1f, 0.82f), new Color(0.32f, 0.82f, 1f, 0.5f), 0.44f, 76, 0.52f, 0.052f),
                MageArmor = CreateAuraBurstPrefab("Mage_Armor_Aura", particleMaterials["Arcane"], new Color(0.35f, 0.72f, 1f, 0.46f), new Color(0.7f, 0.42f, 1f, 0.48f), 0.62f, 98, 1.05f),

                Thunderclap = CreateGroundRingPrefab("Thunderclap_Ring", particleMaterials["Physical"], lineMaterials["Physical"], new Color(0.95f, 0.82f, 0.42f, 0.58f), 2.2f, 0.48f),
                Earthquake = CreateGroundRingPrefab("Earthquake_Dust_Ring", particleMaterials["Earth"], lineMaterials["Physical"], new Color(0.74f, 0.46f, 0.18f, 0.54f), 3.0f, 0.95f),
                PhysicalImpact = CreateBurstPrefab("Physical_Impact", particleMaterials["Physical"], new Color(1f, 0.82f, 0.42f, 0.58f), new Color(1f, 1f, 0.7f, 0.38f), 0.36f, 54, 0.36f, 0.04f),
                ChargeDust = CreateTrailPrefab("Charge_Dust_Trail", particleMaterials["Earth"], new Color(0.74f, 0.52f, 0.28f, 0.38f), 0.42f),
                BloodFury = CreateAuraBurstPrefab("Blood_Fury_Aura", particleMaterials["Blood"], new Color(1f, 0.08f, 0.02f, 0.62f), new Color(1f, 0.42f, 0.08f, 0.32f), 0.52f, 100, 0.78f),
                Regeneration = CreateAuraBurstPrefab("Regeneration_Aura", particleMaterials["Nature"], new Color(0.18f, 1f, 0.18f, 0.5f), new Color(1f, 0.95f, 0.2f, 0.34f), 0.5f, 92, 0.98f)
            };

            prefabs.HealingCharge = AssetDatabase.LoadAssetAtPath<GameObject>(HealingBeamChargePrefabPath);
            if (prefabs.HealingCharge == null)
            {
                throw new MissingReferenceException($"Build Healing Beam VFX before installing ability VFX content. Missing: {HealingBeamChargePrefabPath}");
            }

            return prefabs;
        }

        private static Dictionary<string, MMOAbilityVfxDefinition> CreateDefinitions(VfxPrefabs prefabs)
        {
            Dictionary<string, MMOAbilityVfxDefinition> definitions = new()
            {
                ["mage_fireball"] = ConfigureDefinition("Mage_Fireball_VFX", prefabs.FireCasting, prefabs.Fireball, prefabs.FireBurst, 0.18f, true),
                ["mage_fire_blast"] = ConfigureDefinition("Mage_Fire_Blast_VFX", null, prefabs.FireBlast, prefabs.FireBurst, 0.03f, true),
                ["mage_flamestrike"] = ConfigureDefinition("Mage_Flamestrike_VFX", prefabs.FireCasting, null, prefabs.Flamestrike, 0.05f, false, false),
                ["mage_arcane_missile"] = ConfigureDefinition("Mage_Arcane_Missile_VFX", prefabs.ArcaneCasting, prefabs.ArcaneMissile, prefabs.ArcaneImpact, 0.35f, false),
                ["mage_mage_armor"] = ConfigureDefinition("Mage_Mage_Armor_VFX", null, null, prefabs.MageArmor, 0.02f, false),
                ["shaman_healing_beam"] = ConfigureDefinition(
                    "Shaman_Healing_Beam_VFX",
                    prefabs.HealingCharge,
                    prefabs.HealingBeam,
                    null,
                    0f,
                    false,
                    useHandCastingAnchors: false,
                    alignCastPrefabToTarget: false),
                ["shaman_lightning_bolt"] = ConfigureDefinition("Shaman_Lightning_Bolt_VFX", prefabs.NatureCasting, prefabs.LightningBeam, prefabs.LightningImpact, 0.08f, true),
                ["shaman_frost_shock"] = ConfigureDefinition("Shaman_Frost_Shock_VFX", null, prefabs.FrostShock, prefabs.FrostImpact, 0.04f, true),
                ["shaman_water_shield"] = ConfigureDefinition("Shaman_Water_Shield_VFX", null, null, prefabs.WaterShield, 0.02f, false),
                ["shaman_earthquake"] = ConfigureDefinition("Shaman_Earthquake_VFX", null, null, prefabs.Earthquake, 0.02f, false, false),
                ["warrior_thunderclap"] = ConfigureDefinition("Warrior_Thunderclap_VFX", null, null, prefabs.Thunderclap, 0.02f, false, false),
                ["warrior_gouge"] = ConfigureDefinition("Warrior_Gouge_VFX", null, null, prefabs.PhysicalImpact, 0.04f, false),
                ["warrior_bash"] = ConfigureDefinition(
                    "Warrior_Bash_VFX",
                    null,
                    null,
                    AssetDatabase.LoadAssetAtPath<GameObject>(BashPrefabPath) ?? prefabs.PhysicalImpact,
                    0.04f,
                    false),
                ["warrior_charge"] = ConfigureDefinition("Warrior_Charge_VFX", null, prefabs.ChargeDust, prefabs.PhysicalImpact, 0.02f, false),
                ["warrior_berzerkitis"] = ConfigureDefinition(
                    "Warrior_Berzerkitis_VFX",
                    null,
                    null,
                    AssetDatabase.LoadAssetAtPath<GameObject>(BerzerkitisPrefabPath) ?? prefabs.BloodFury,
                    0.02f,
                    false),
                ["orc_blood_fury"] = ConfigureDefinition("Orc_Blood_Fury_VFX", null, null, prefabs.BloodFury, 0.02f, false),
                ["troll_regeneration"] = ConfigureDefinition("Troll_Regeneration_VFX", null, null, prefabs.Regeneration, 0.02f, false)
            };

            return definitions;
        }

        private static MMOAbilityVfxDefinition ConfigureDefinition(
            string assetName,
            GameObject castingPrefab,
            GameObject castPrefab,
            GameObject hitPrefab,
            float hitDelaySeconds,
            bool castPrefabControlsHitTiming,
            bool attachHitToTarget = true,
            bool useHandCastingAnchors = true,
            bool alignCastPrefabToTarget = true)
        {
            string path = $"{DefinitionFolder}/{assetName}.asset";
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(path);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOAbilityVfxDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.Configure(
                castingPrefab,
                castPrefab,
                hitPrefab,
                true,
                useHandCastingAnchors,
                attachHitToTarget,
                alignCastPrefabToTarget,
                new Vector3(0f, 1.15f, 0.42f),
                Vector3.zero,
                new Vector3(0f, 1.18f, 0.48f),
                new Vector3(0f, 0.85f, 0f),
                hitDelaySeconds,
                castPrefabControlsHitTiming);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void AssignDefinitions(Dictionary<string, MMOAbilityVfxDefinition> definitions)
        {
            string[] guids = AssetDatabase.FindAssets("t:MMOAbilityDefinition", new[] { AbilityFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
                if (ability == null || !definitions.TryGetValue(ability.AbilityId, out MMOAbilityVfxDefinition definition))
                {
                    continue;
                }

                ability.SetVisualEffects(definition);
                EditorUtility.SetDirty(ability);
            }
        }

        private static GameObject CreateAuraPrefab(string name, Material material, Color colorA, Color colorB, float radius, float rate, float size)
        {
            GameObject root = new(name);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles, material, true, 1f, rate, 0, radius, size, colorA, colorB, ParticleSystemShapeType.Sphere);
            root.AddComponent<MMOAbilityVfxLifetime>().Configure(0f, false, true);
            return SavePrefab(root, name);
        }

        private static GameObject CreateAuraBurstPrefab(string name, Material material, Color colorA, Color colorB, float radius, int burstCount, float lifetime)
        {
            GameObject root = new(name);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles, material, false, lifetime, 0f, burstCount, radius, 0.06f, colorA, colorB, ParticleSystemShapeType.Sphere);
            root.AddComponent<MMOAbilityVfxLifetime>().Configure(lifetime + 0.25f, true, true);
            return SavePrefab(root, name);
        }

        private static GameObject CreateBurstPrefab(string name, Material material, Color colorA, Color colorB, float radius, int burstCount, float lifetime, float size)
        {
            GameObject root = new(name);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles, material, false, lifetime, 0f, burstCount, radius, size, colorA, colorB, ParticleSystemShapeType.Sphere);
            root.AddComponent<MMOAbilityVfxLifetime>().Configure(lifetime + 0.2f, true, true);
            return SavePrefab(root, name);
        }

        private static GameObject CreateColumnPrefab(string name, Material material, Color colorA, Color colorB, float radius, int burstCount, float lifetime)
        {
            GameObject root = new(name);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles, material, false, lifetime, 0f, burstCount, radius, 0.08f, colorA, colorB, ParticleSystemShapeType.Cone);
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.angle = 18f;
            shape.length = 2.5f;
            root.AddComponent<MMOAbilityVfxLifetime>().Configure(lifetime + 0.3f, true, true);
            return SavePrefab(root, name);
        }

        private static GameObject CreateProjectilePrefab(
            string name,
            Material particleMaterial,
            Color particleColor,
            Material trailMaterial,
            float speed,
            float arcHeight,
            float trailWidth,
            float particleSize)
        {
            GameObject root = new(name);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles, particleMaterial, true, 0.45f, 26f, 0, 0.08f, particleSize, particleColor, particleColor, ParticleSystemShapeType.Sphere);

            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = 0.28f;
            trail.widthMultiplier = trailWidth;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            root.AddComponent<MMOAbilityVfxProjectile>().Configure(speed, arcHeight, 4f, true);
            return SavePrefab(root, name);
        }

        private static GameObject CreateBeamPrefab(
            string name,
            Material lineMaterial,
            Material particleMaterial,
            float duration,
            int points,
            float noise,
            float frequency,
            bool requestHitOnStart,
            float width,
            float particleLifetime)
        {
            GameObject root = new(name);
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial;
            line.positionCount = points;
            line.widthMultiplier = width;
            line.numCornerVertices = 1;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;

            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles, particleMaterial, true, particleLifetime, 20f, 0, 0.08f, 0.08f, Color.white, Color.white, ParticleSystemShapeType.Sphere);

            root.AddComponent<MMOAbilityVfxBeam>().Configure(duration, points, noise, frequency, requestHitOnStart);
            return SavePrefab(root, name);
        }

        private static GameObject CreateGroundRingPrefab(string name, Material particleMaterial, Material lineMaterial, Color color, float radius, float lifetime)
        {
            GameObject root = new(name);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles, particleMaterial, false, lifetime, 0f, 128, radius, 0.055f, color, color, ParticleSystemShapeType.Circle);
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.radiusThickness = 0.18f;

            LineRenderer ring = root.AddComponent<LineRenderer>();
            ring.sharedMaterial = lineMaterial;
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.widthMultiplier = 0.04f;
            ring.positionCount = 96;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i / (float)ring.positionCount * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0.04f, Mathf.Sin(angle) * radius));
            }

            root.AddComponent<MMOAbilityVfxLifetime>().Configure(lifetime, true, true);
            return SavePrefab(root, name);
        }

        private static GameObject CreateTrailPrefab(string name, Material material, Color color, float lifetime)
        {
            GameObject root = new(name);
            ParticleSystem particles = root.AddComponent<ParticleSystem>();
            ConfigureParticleSystem(particles, material, true, lifetime, 96f, 0, 0.28f, 0.055f, color, color, ParticleSystemShapeType.Sphere);
            root.AddComponent<MMOAbilityVfxLifetime>().Configure(lifetime, true, true);
            return SavePrefab(root, name);
        }

        private static void ConfigureParticleSystem(
            ParticleSystem particles,
            Material material,
            bool loop,
            float lifetime,
            float rate,
            int burstCount,
            float radius,
            float size,
            Color colorA,
            Color colorB,
            ParticleSystemShapeType shapeType)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = loop;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.startLifetime = new ParticleSystem.MinMaxCurve(Mathf.Max(0.08f, lifetime * 0.55f), Mathf.Max(0.12f, lifetime));
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.16f, 0.72f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.35f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
            main.maxParticles = Mathf.Max(96, burstCount * 3, Mathf.CeilToInt(rate * 3f));
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = loop ? rate : 0f;
            emission.SetBursts(!loop && burstCount > 0
                ? new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(burstCount, 0, short.MaxValue)) }
                : System.Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = radius;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(colorA, 0f),
                    new GradientColorKey(colorB, 0.6f),
                    new GradientColorKey(colorA, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(colorA.a, 0f),
                    new GradientAlphaKey(colorB.a, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.08f;
        }

        private static GameObject SavePrefab(GameObject root, string name)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateMaterial(string name, Color color, bool additive)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find(additive ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader)
            {
                material.shader = shader;
            }

            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
            ConfigureTransparentMaterial(material, additive);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTransparentMaterial(Material material, bool additive)
        {
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", additive ? 1f : 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.enableInstancing = true;
        }

        private static Texture2D CreateOrUpdateSoftParticleTexture()
        {
            const int size = 128;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "VFX_Soft_Cloud",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 uv = new((x + 0.5f) / size, (y + 0.5f) / size);
                    float alpha = EvaluateCloudAlpha(uv);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            System.IO.File.WriteAllBytes(SoftParticleTexturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SoftParticleTexturePath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(SoftParticleTexturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(SoftParticleTexturePath);
        }

        private static float EvaluateCloudAlpha(Vector2 uv)
        {
            float alpha = 0f;
            alpha += SoftBlob(uv, new Vector2(0.5f, 0.5f), 0.52f, 0.95f);
            alpha += SoftBlob(uv, new Vector2(0.36f, 0.55f), 0.32f, 0.45f);
            alpha += SoftBlob(uv, new Vector2(0.64f, 0.46f), 0.34f, 0.42f);
            alpha += SoftBlob(uv, new Vector2(0.5f, 0.34f), 0.28f, 0.28f);
            float edgeFade = Mathf.SmoothStep(0f, 0.18f, uv.x)
                * Mathf.SmoothStep(0f, 0.18f, uv.y)
                * Mathf.SmoothStep(0f, 0.18f, 1f - uv.x)
                * Mathf.SmoothStep(0f, 0.18f, 1f - uv.y);
            float noise = 0.78f
                + Hash01(Mathf.FloorToInt(uv.x * 18f), Mathf.FloorToInt(uv.y * 18f)) * 0.16f
                + Hash01(Mathf.FloorToInt(uv.x * 37f) + 11, Mathf.FloorToInt(uv.y * 37f) + 7) * 0.06f;
            return Mathf.Clamp01(alpha * edgeFade * noise);
        }

        private static float SoftBlob(Vector2 uv, Vector2 center, float radius, float strength)
        {
            float distance = Vector2.Distance(uv, center) / Mathf.Max(0.001f, radius);
            float value = Mathf.Clamp01(1f - distance);
            value = Mathf.SmoothStep(0f, 1f, value);
            return value * value * strength;
        }

        private static float Hash01(int x, int y)
        {
            unchecked
            {
                int hash = x * 73856093 ^ y * 19349663;
                hash = (hash << 13) ^ hash;
                return 1f - ((hash * (hash * hash * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824f;
            }
        }

        private static void ApplyParticleTexture(Dictionary<string, Material> particleMaterials, Texture texture)
        {
            if (texture == null)
            {
                return;
            }

            foreach (Material material in particleMaterials.Values)
            {
                SetTextureIfPresent(material, "_BaseMap", texture);
                SetTextureIfPresent(material, "_MainTex", texture);
                EditorUtility.SetDirty(material);
            }
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, value);
            }
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(MaterialFolder);
            CreateFolderIfMissing(PrefabFolder);
            CreateFolderIfMissing(DefinitionFolder);
            CreateFolderIfMissing(TextureFolder);
        }

        private static void CreateFolderIfMissing(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                CreateFolderIfMissing(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private sealed class VfxPrefabs
        {
            public GameObject FireCasting;
            public GameObject FrostCasting;
            public GameObject NatureCasting;
            public GameObject HolyCasting;
            public GameObject ArcaneCasting;
            public GameObject Fireball;
            public GameObject FireBurst;
            public GameObject FireBlast;
            public GameObject Flamestrike;
            public GameObject HealingBeam;
            public GameObject HealingCharge;
            public GameObject HealingImpact;
            public GameObject LightningBeam;
            public GameObject LightningImpact;
            public GameObject FrostShock;
            public GameObject FrostImpact;
            public GameObject WaterShield;
            public GameObject ArcaneMissile;
            public GameObject ArcaneImpact;
            public GameObject MageArmor;
            public GameObject Thunderclap;
            public GameObject Earthquake;
            public GameObject PhysicalImpact;
            public GameObject ChargeDust;
            public GameObject BloodFury;
            public GameObject Regeneration;
        }
    }
}
