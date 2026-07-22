using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Physical;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class GougeVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/Gouge";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialFolder = Root + "/Materials";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string ProfileFolder = Root + "/Profiles";
        private const string ShaderPath = Root + "/Shaders/GougeSpriteUnlit.shader";
        private const string DocumentationFolder = Root + "/Documentation";
        private const string ProfilePath = ProfileFolder + "/GougeVFX_Default.asset";
        private const string CastPrefabPath = PrefabFolder + "/GougeCastVFX.prefab";
        private const string HitPrefabPath = PrefabFolder + "/GougeVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Warrior_Gouge_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Warrior_Gouge.asset";

        private const string WoundTexturePath = TextureFolder + "/Gouge_Wound_Atlas_v2.png";
        private const string WeaponTrailTexturePath = TextureFolder + "/Gouge_Weapon_Trail.png";
        private const string TearingTrailTexturePath = TextureFolder + "/Gouge_Tearing_Trail.png";
        private const string ContactFlashTexturePath = TextureFolder + "/Gouge_Contact_Flash_Atlas_v2.png";
        private const string BloodTexturePath = TextureFolder + "/Gouge_Blood_Atlas_v2.png";
        private const string DebrisSparkTexturePath = TextureFolder + "/Gouge_Debris_Spark_Atlas.png";
        private const string CriticalRingTexturePath = TextureFolder + "/Gouge_Critical_Reset_Ring.png";
        private const string WoundMistTexturePath = TextureFolder + "/Gouge_Wound_Mist_Atlas.png";
        private const string DustRingTexturePath = "Assets/_Project/VFX/Bash/Textures/Bash_DustRing.png";
        private const string ChargeHeavyDustMaterialPath = "Assets/_Project/VFX/Charge/Materials/Charge_HeavyDust.mat";
        private const string ChargeFineDustMaterialPath = "Assets/_Project/VFX/Charge/Materials/Charge_FineDust.mat";
        private const string ChargeGroundBurstMaterialPath = "Assets/_Project/VFX/Charge/Materials/Charge_GroundBursts.mat";
        private const string ChargeDirtMaterialPath = "Assets/_Project/VFX/Charge/Materials/Charge_DirtDebris.mat";

        [MenuItem("Tools/RPG Clone/VFX/Build Gouge VFX")]
        public static void BuildGougeVFX()
        {
            EnsureFolders();
            ImportTextures();
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null)
            {
                throw new MissingReferenceException($"Missing Gouge shader at {ShaderPath}.");
            }

            GougeVFXProfile profile = GetOrCreateProfile();
            Dictionary<string, Material> materials = CreateMaterials(shader);
            GameObject castPrefab = BuildCastPrefab(profile, materials);
            GameObject hitPrefab = BuildHitPrefab(profile, materials);
            AssignAbilityVfx(castPrefab, hitPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Built Gouge VFX package, assigned Warrior_Gouge, and preserved the shared replicated ability presentation path.");
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Gouge VFX")]
        public static void ValidateGougeVFX()
        {
            List<string> failures = new();
            ValidateTexture(WoundTexturePath, failures);
            ValidateTexture(WeaponTrailTexturePath, failures);
            ValidateTexture(TearingTrailTexturePath, failures);
            ValidateTexture(ContactFlashTexturePath, failures);
            ValidateTexture(BloodTexturePath, failures);
            ValidateTexture(DebrisSparkTexturePath, failures);
            ValidateTexture(CriticalRingTexturePath, failures);
            ValidateTexture(WoundMistTexturePath, failures);
            ValidateMaterial(ChargeHeavyDustMaterialPath, failures);
            ValidateMaterial(ChargeFineDustMaterialPath, failures);
            ValidateMaterial(ChargeGroundBurstMaterialPath, failures);
            ValidateMaterial(ChargeDirtMaterialPath, failures);

            GameObject castPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastPrefabPath);
            GameObject hitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefabPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (castPrefab == null || castPrefab.GetComponent<GougeCastVFX>() == null)
            {
                failures.Add("GougeCastVFX prefab or runtime component is missing.");
            }

            if (hitPrefab == null || hitPrefab.GetComponent<GougeVFX>() == null || hitPrefab.GetComponent<MMOAbilityVfxPoolable>() == null)
            {
                failures.Add("GougeVFX prefab, runtime component, or pool marker is missing.");
            }

            if (definition == null || definition.CastPrefab != castPrefab || definition.HitPrefab != hitPrefab || definition.HitDelaySeconds > 0f)
            {
                failures.Add("Warrior_Gouge_VFX is not configured for the Gouge cast/hit prefabs with zero presentation delay.");
            }

            if (ability == null || ability.VisualEffects != definition)
            {
                failures.Add("Warrior_Gouge does not reference Warrior_Gouge_VFX.");
            }

            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported)
            {
                failures.Add("The Gouge layered shader is missing or unsupported by the active render pipeline.");
            }

            ValidateRenderers(castPrefab, failures);
            ValidateRenderers(hitPrefab, failures);
            ValidateParticleVelocityModes(castPrefab, failures);
            ValidateParticleVelocityModes(hitPrefab, failures);

            if (failures.Count > 0)
            {
                throw new InvalidOperationException("Gouge VFX validation failed:\n- " + string.Join("\n- ", failures));
            }

            Debug.Log("Gouge VFX validation passed: textures, pooled prefabs, zero-delay combat-event listener, definition, and ability assignment are valid.");
        }

        [MenuItem("Tools/RPG Clone/VFX/Preview Gouge VFX In Play Mode")]
        public static void PreviewGougeVFXInPlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("Enter Play Mode before staging the Gouge VFX preview.");
                return;
            }

            Camera camera = Camera.main;
            GameObject castPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastPrefabPath);
            GameObject hitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitPrefabPath);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (camera == null || castPrefab == null || hitPrefab == null || ability == null || definition == null)
            {
                Debug.LogError("Cannot stage Gouge preview because its camera or authored assets are missing.");
                return;
            }

            GameObject previous = GameObject.Find("__GougeVFXPreview");
            if (previous != null)
            {
                UnityEngine.Object.Destroy(previous);
            }

            GameObject previewRoot = new("__GougeVFXPreview");
            previewRoot.transform.SetParent(camera.transform, false);
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            target.name = "Gouge Preview Target";
            target.transform.SetParent(previewRoot.transform, false);
            target.transform.localPosition = new Vector3(0f, 0f, 5f);
            target.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            target.transform.localScale = new Vector3(0.8f, 1.15f, 0.8f);
            target.AddComponent<RPGClone.Characters.MMOCharacterIdentity>();
            target.AddComponent<RPGClone.Combat.MMOCombatant>();

            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader != null)
            {
                Material previewMaterial = new(litShader) { color = new Color(0.09f, 0.1f, 0.12f, 1f) };
                target.GetComponent<Renderer>().material = previewMaterial;
            }

            GameObject source = new("Gouge Preview Source");
            source.transform.SetParent(previewRoot.transform, false);
            source.transform.localPosition = new Vector3(0f, 0f, 1.2f);
            source.transform.localRotation = Quaternion.identity;

            MMOAbilityVfxContext context = new(
                null,
                ability,
                definition,
                source.transform,
                target.transform,
                source.transform.position,
                target.transform.TransformPoint(new Vector3(0f, 0.32f, 0f)),
                false,
                null);

            GameObject cast = UnityEngine.Object.Instantiate(castPrefab, source.transform.position, source.transform.rotation, previewRoot.transform);
            cast.GetComponent<GougeCastVFX>().Initialize(context);
            GameObject hit = UnityEngine.Object.Instantiate(hitPrefab, target.transform.position, Quaternion.identity, target.transform);
            hit.GetComponent<GougeVFX>().Initialize(context);
            Debug.Log("Staged a runtime Gouge VFX preview in front of the Game camera.");
        }

        private static GougeVFXProfile GetOrCreateProfile()
        {
            GougeVFXProfile profile = AssetDatabase.LoadAssetAtPath<GougeVFXProfile>(ProfilePath);
            if (profile != null)
            {
                if (profile.UpgradeToLatestDefaults())
                {
                    EditorUtility.SetDirty(profile);
                }

                return profile;
            }

            profile = ScriptableObject.CreateInstance<GougeVFXProfile>();
            profile.UpgradeToLatestDefaults();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials(Shader shader)
        {
            Texture2D wound = LoadTexture(WoundTexturePath);
            Texture2D weaponTrail = LoadTexture(WeaponTrailTexturePath);
            Texture2D tearingTrail = LoadTexture(TearingTrailTexturePath);
            Texture2D flash = LoadTexture(ContactFlashTexturePath);
            Texture2D blood = LoadTexture(BloodTexturePath);
            Texture2D debrisSpark = LoadTexture(DebrisSparkTexturePath);
            Texture2D ring = LoadTexture(CriticalRingTexturePath);
            Texture2D mist = LoadTexture(WoundMistTexturePath);
            Texture2D dustRing = LoadTexture(DustRingTexturePath);

            return new Dictionary<string, Material>
            {
                ["WeaponTrail"] = CreateMaterial("Gouge_WeaponTrail", shader, weaponTrail, new Color(1f, 0.92f, 0.68f, 0.9f), true, 1.1f),
                ["TearingTrail"] = CreateMaterial("Gouge_TearingTrail", shader, tearingTrail, Color.white, false, 1f),
                ["Flash"] = CreateMaterial("Gouge_ContactFlash_Additive", shader, flash, Color.white, true, 1.35f),
                ["Blood"] = CreateMaterial("Gouge_Blood_Alpha", shader, blood, Color.white, false, 1.08f),
                ["BloodAdd"] = CreateMaterial("Gouge_Blood_Additive", shader, blood, Color.white, true, 1.16f),
                ["WoundBase"] = CreateMaterial("Gouge_WoundBase", shader, wound, Color.white, false, 1.08f),
                ["WoundInner"] = CreateMaterial("Gouge_WoundInner_Additive", shader, wound, Color.white, true, 1.18f),
                ["Mist"] = CreateMaterial("Gouge_WoundMist", shader, mist, Color.white, false, 0.92f),
                ["DustRing"] = CreateMaterial("Gouge_ImpactDustRing", shader, dustRing, Color.white, false, 1f),
                ["ChargeHeavyDust"] = LoadMaterial(ChargeHeavyDustMaterialPath),
                ["ChargeFineDust"] = LoadMaterial(ChargeFineDustMaterialPath),
                ["ChargeGroundBurst"] = LoadMaterial(ChargeGroundBurstMaterialPath),
                ["ChargeDirt"] = LoadMaterial(ChargeDirtMaterialPath),
                ["Debris"] = CreateMaterial("Gouge_TornFragments", shader, debrisSpark, Color.white, false, 0.96f),
                ["Spark"] = CreateMaterial("Gouge_MetallicSparks_Additive", shader, debrisSpark, new Color(1f, 0.9f, 0.62f, 0.94f), true, 1.25f),
                ["Ring"] = CreateMaterial("Gouge_CriticalResetRing_Additive", shader, ring, new Color(1f, 0.88f, 0.5f, 0.92f), true, 1.2f)
            };
        }

        private static Material CreateMaterial(string name, Shader shader, Texture texture, Color tint, bool additive, float brightness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                // Preserve the material asset/GUID while removing serialized properties left by
                // Gouge's retired layered shader. This keeps the generated content as clean as
                // the Bash and Charge material assets that use this same sprite workflow.
                Material cleanMaterial = new(shader) { name = name };
                EditorUtility.CopySerialized(cleanMaterial, material);
                UnityEngine.Object.DestroyImmediate(cleanMaterial);
            }

            material.shader = shader;
            material.name = name;
            material.enableInstancing = true;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Brightness", brightness);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject BuildCastPrefab(GougeVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("GougeCastVFX");
            MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
            poolable.ConfigureAuthoring(24);
            Transform motionRoot = CreateSection("GougeWeaponTrailVFX", root.transform);
            Transform anticipation = CreateSection("Instant Attack Anticipation", motionRoot);
            Transform trailSection = CreateSection("Gouging Weapon Motion", motionRoot);

            ParticleSystem mainTrail = CreateParticles("Main Weapon Trail", trailSection, materials["WeaponTrail"], false, false, 1, 1, 0.26f, 0f, 1f, 1, 10, 0f);
            ParticleSystem tearingTrail = CreateParticles("Secondary Tearing Trail", trailSection, materials["TearingTrail"], false, false, 1, 1, 0.2f, 0f, 1f, 1, 11, 0f);
            ParticleSystem[] glints =
            {
                CreateParticles("Weapon Glint A", anticipation, materials["Flash"], false, false, 2, 2, 0.12f, 0f, 0.24f, 1, 20, 0f),
                CreateParticles("Weapon Glint B", anticipation, materials["Flash"], false, false, 2, 2, 0.12f, 0f, 0.2f, 1, 20, 0f)
            };
            ParticleSystem fragments = CreateParticles("Motion Fragments", trailSection, materials["Debris"], false, true, 4, 2, 0.42f, 1.2f, 0.16f, 12, 14, 0.25f);
            ParticleSystem dust = CreateParticles("Arm Dust And Cloth", anticipation, materials["Mist"], false, true, 2, 2, 0.34f, 0.45f, 0.2f, 8, 8, 0.08f);
            ConfigureFadeAndScale(mainTrail, 0.3f, 1f, 0.25f, 0f, 0.48f);
            ConfigureFadeAndScale(tearingTrail, 0.25f, 1f, 0.18f, 0.08f, 0.42f);
            ConfigureFadeAndScale(glints[0], 0.25f, 1f, 0.1f, 0f, 0.3f);
            ConfigureFadeAndScale(glints[1], 0.2f, 1f, 0.08f, 0.05f, 0.34f);
            ConfigureFadeAndScale(fragments, 0.7f, 1f, 0.2f, 0f, 0.48f);
            ConfigureFadeAndScale(dust, 0.4f, 1f, 1.2f, 0f, 0.34f);

            GougeCastVFX cast = root.AddComponent<GougeCastVFX>();
            cast.ConfigureAuthoring(profile, motionRoot, mainTrail, tearingTrail, glints, fragments, dust);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CastPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject BuildHitPrefab(GougeVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("GougeVFX");
            MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
            poolable.ConfigureAuthoring(48);
            Transform attached = CreateSection("Target Attached Layers", root.transform);
            Transform impact = CreateSection("GougeImpactVFX", attached);
            Transform wound = CreateSection("GougeBleedVFX", attached);
            Transform tick = CreateSection("GougeBleedTickVFX", attached);
            Transform stack = CreateSection("GougeStackIncreaseVFX", attached);
            Transform critical = CreateSection("GougeCriticalResetVFX", attached);
            Transform expiration = CreateSection("GougeExpirationVFX", attached);
            Transform groundReaction = CreateSection("Ground Force Reaction", root.transform);

            ParticleSystem contactFlash = CreateParticles("Contact Flash", impact, materials["Flash"], false, false, 2, 2, 0.2f, 0f, 0.8f, 1, 21, 0f);
            ParticleSystem criticalFlash = CreateParticles("Critical Contact Flash", critical, materials["Flash"], false, true, 2, 2, 0.2f, 0f, 1f, 1, 25, 0f);
            ParticleSystem impactLine = CreateParticles("Physical Impact Lines", impact, materials["Flash"], false, true, 2, 2, 0.3f, 1.8f, 0.22f, 6, 22, 0.04f);

            ParticleSystem[] woundBases = new ParticleSystem[3];
            ParticleSystem[] woundInners = new ParticleSystem[3];
            ParticleSystem[] wetHighlights = new ParticleSystem[3];
            for (int i = 0; i < 3; i++)
            {
                Transform channel = CreateSection($"Wound State {i + 1}", wound);
                float fixedFrame = i == 0 ? 3f / 4f : i == 1 ? 1f / 4f : 2f / 4f;
                woundBases[i] = CreateParticles("Dark Wound Base", channel, materials["WoundBase"], false, false, 2, 2, 9.6f, 0f, 0.86f, 1, 12 + i, 0f, fixedFrame);
                woundInners[i] = CreateParticles("Bright Inner Cut", channel, materials["WoundInner"], false, false, 2, 2, 9.6f, 0f, 0.78f, 1, 15 + i, 0f, fixedFrame);
                wetHighlights[i] = CreateParticles("Wet Highlight", channel, materials["WoundInner"], false, false, 2, 2, 9.6f, 0f, 0.54f, 1, 18 + i, 0f, fixedFrame);
                ConfigurePersistentWoundCurve(woundBases[i], 1f);
                ConfigurePersistentWoundCurve(woundInners[i], 0.96f);
                ConfigurePersistentWoundCurve(wetHighlights[i], 0.9f);
                SetFireballViewAlignment(woundBases[i]);
                SetFireballViewAlignment(woundInners[i]);
                SetFireballViewAlignment(wetHighlights[i]);
            }

            ParticleSystem woundPulse = CreateParticles("Blood Pulse", tick, materials["BloodAdd"], false, false, 4, 2, 0.36f, 0f, 1.05f, 1, 21, 0f, 7f / 8f);
            ParticleSystem bodyAccent = CreateParticles("Target Body Accent", tick, materials["Blood"], false, false, 4, 2, 0.3f, 0f, 1.4f, 1, 10, 0f, 6f / 8f);
            ParticleSystem[] stackStreaks =
            {
                CreateParticles("Stack Tearing Streak A", stack, materials["TearingTrail"], false, true, 1, 1, 0.32f, 1.1f, 0.28f, 1, 23, 0f),
                CreateParticles("Stack Tearing Streak B", stack, materials["TearingTrail"], false, true, 1, 1, 0.32f, 1.1f, 0.28f, 1, 23, 0f),
                CreateParticles("Stack Tearing Streak C", stack, materials["TearingTrail"], false, true, 1, 1, 0.32f, 1.1f, 0.28f, 1, 23, 0f)
            };
            stackStreaks[1].transform.localRotation = Quaternion.Euler(0f, 0f, 48f);
            stackStreaks[2].transform.localRotation = Quaternion.Euler(0f, 0f, -42f);

            ParticleSystem resetRing = CreateParticles("Cooldown Reset Ring", critical, materials["Ring"], false, true, 1, 1, 0.22f, 0f, 0.78f, 1, 26, 0f);
            ParticleSystem[] criticalStreaks =
            {
                CreateParticles("Critical Double Gouge A", critical, materials["TearingTrail"], false, true, 1, 1, 0.26f, 1.25f, 0.24f, 1, 24, 0f),
                CreateParticles("Critical Double Gouge B", critical, materials["TearingTrail"], false, true, 1, 1, 0.26f, 1.25f, 0.24f, 1, 24, 0f)
            };
            criticalStreaks[1].transform.localRotation = Quaternion.Euler(0f, 0f, 27f);

            ParticleSystem expirationFragments = CreateParticles("Closing Wound Fragments", expiration, materials["Blood"], false, true, 4, 2, 0.48f, 0.3f, 0.16f, 6, 16, 0.04f);

            ParticleSystem directionalSpray = CreateParticles("Directional Blood Spray", impact, materials["Blood"], false, true, 4, 2, 0.48f, 1.35f, 0.24f, 28, 18, 0.12f);
            ConfigureStretchRenderer(directionalSpray, 2.4f, 0.18f);
            ParticleSystem closeBurst = CreateParticles("Close Blood Burst", impact, materials["Blood"], false, true, 4, 2, 0.42f, 0.8f, 0.2f, 24, 17, 0.22f);
            ParticleSystem fragments = CreateParticles("Torn Fragment Burst", impact, materials["Debris"], false, true, 4, 2, 0.52f, 1.2f, 0.16f, 20, 14, 0.2f);
            ParticleSystem groundBurst = CreateParticles("Charge Ground Burst", groundReaction, materials["ChargeGroundBurst"], false, true, 4, 1, 0.78f, 3.1f, 1.45f, 48, 8, 0.42f);
            groundBurst.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem.ShapeModule groundBurstShape = groundBurst.shape;
            groundBurstShape.shapeType = ParticleSystemShapeType.Cone;
            groundBurstShape.angle = 74f;
            groundBurstShape.radiusThickness = 0.78f;
            ParticleSystem heavyDust = CreateParticles("Environmental Heavy Dust", groundReaction, materials["ChargeHeavyDust"], false, true, 4, 2, 2.5f, 2.55f, 1.5f, 128, 6, 0.42f);
            ParticleSystem fineDust = CreateParticles("Environmental Fine Dust", groundReaction, materials["ChargeFineDust"], false, true, 4, 2, 3f, 1.4f, 0.92f, 128, 7, 0.52f);
            ParticleSystem impactDustRing = CreateParticles("Ground Compression Ring", groundReaction, materials["DustRing"], false, true, 1, 1, 0.42f, 0f, 1.65f, 1, 7, 0f);
            impactDustRing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            SetLocalSurfaceAlignment(impactDustRing);
            ConfigureFadeAndScale(impactDustRing, 0.2f, 1f, 1.12f, 0f, 0.22f);
            ParticleSystem groundDebris = CreateParticles("Ground Debris", groundReaction, materials["ChargeDirt"], false, true, 4, 1, 0.72f, 3.2f, 0.24f, 32, 9, 0.24f);
            groundDebris.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            ParticleSystem.MainModule debrisMain = groundDebris.main;
            debrisMain.gravityModifier = 1.35f;
            ParticleSystem seepage = CreateParticles("Animated Blood Seepage", wound, materials["Blood"], true, false, 4, 2, 0.75f, 0.08f, 0.09f, 12, 14, 0.04f);
            ParticleSystem drips = CreateParticles("Persistent Droplets", wound, materials["Blood"], false, true, 4, 2, 0.9f, 0.12f, 0.11f, 20, 14, 0.05f);
            ParticleSystem mist = CreateParticles("Wound Mist", wound, materials["Mist"], true, false, 2, 2, 0.9f, 0.04f, 0.28f, 8, 9, 0.18f);
            ParticleSystem tickSpray = CreateParticles("Fresh Tick Blood Spray", tick, materials["Blood"], false, true, 4, 2, 0.42f, 0.9f, 0.18f, 24, 19, 0.14f);
            ConfigureStretchRenderer(tickSpray, 2f, 0.16f);
            ParticleSystem tickDrips = CreateParticles("Heavy Tick Drips", tick, materials["Blood"], false, true, 4, 2, 0.72f, 0.45f, 0.13f, 16, 18, 0.05f);
            ParticleSystem criticalBlood = CreateParticles("Critical Blood Burst", critical, materials["Blood"], false, true, 4, 2, 0.5f, 1.45f, 0.25f, 36, 22, 0.12f);
            ConfigureStretchRenderer(criticalBlood, 2.25f, 0.18f);
            ParticleSystem criticalSparks = CreateParticles("Metallic Critical Sparks", critical, materials["Spark"], false, true, 4, 2, 0.32f, 2.2f, 0.11f, 24, 26, 0.08f);
            ParticleSystem finalDrops = CreateParticles("Final Droplets", expiration, materials["Blood"], false, true, 4, 2, 0.72f, 0.35f, 0.11f, 6, 14, 0.04f);

            foreach (ParticleSystem system in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (system != woundBases[0] && system != woundBases[1] && system != woundBases[2]
                    && system != woundInners[0] && system != woundInners[1] && system != woundInners[2]
                    && system != wetHighlights[0] && system != wetHighlights[1] && system != wetHighlights[2])
                {
                    ConfigureFadeAndScale(system, 0.35f, 1f, 0.2f, 0f, 0.55f);
                }
            }
            ConfigureChargeEnvironmentalDust(heavyDust, 0.42f, 0.3f, 0.34f, 0.62f);
            ConfigureChargeEnvironmentalDust(fineDust, 0.52f, 0.36f, 0.24f, 0.58f);

            GougeVFX effect = root.AddComponent<GougeVFX>();
            effect.ConfigureAuthoring(
                profile, attached, groundReaction,
                contactFlash, criticalFlash, impactLine, woundBases, woundInners, wetHighlights,
                woundPulse, bodyAccent, stackStreaks, resetRing, criticalStreaks, expirationFragments,
                directionalSpray, closeBurst, fragments, groundBurst, heavyDust, fineDust, impactDustRing, groundDebris, seepage, drips, mist, tickSpray,
                tickDrips, criticalBlood, criticalSparks, finalDrops);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HitPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static void AssignAbilityVfx(GameObject castPrefab, GameObject hitPrefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOAbilityVfxDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.Configure(
                null,
                castPrefab,
                hitPrefab,
                true,
                false,
                true,
                false,
                new Vector3(0f, 1.1f, 0.35f),
                Vector3.zero,
                new Vector3(0f, 1.1f, 0.35f),
                new Vector3(0f, 1.02f, 0f),
                0f,
                false);
            EditorUtility.SetDirty(definition);

            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null)
            {
                throw new MissingReferenceException($"Missing Gouge ability at {AbilityPath}.");
            }

            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(ability);
        }

        private static ParticleSystem CreateParticles(
            string name,
            Transform parent,
            Material material,
            bool loop,
            bool worldSpace,
            int tilesX,
            int tilesY,
            float lifetime,
            float speed,
            float size,
            int maxParticles,
            int sortingOrder,
            float radius,
            float fixedFrame = -1f)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem particles = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.loop = loop;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime * 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed * 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.65f, size * 1.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.startColor = Color.white;
            main.maxParticles = Mathf.Max(1, maxParticles);
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = loop;
            emission.rateOverTime = loop ? 1f : 0f;
            emission.SetBursts(Array.Empty<ParticleSystem.Burst>());

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = radius > 0f;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.radius = radius;
            shape.angle = 18f;

            if (name.Contains("Drip") || name.Contains("Seepage") || name.Contains("Droplet"))
            {
                main.gravityModifier = 0.55f;
                ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
                velocity.enabled = true;
                // Unity requires all three axes to use the same MinMaxCurve mode. Leaving X/Z
                // as constants while Y used TwoConstants made the module invalid at authoring time.
                velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                velocity.y = new ParticleSystem.MinMaxCurve(-0.28f, -0.65f);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.15f, 1f),
                new Keyframe(1f, loop ? 0.7f : 0.25f)));

            ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
            sheet.enabled = tilesX > 1 || tilesY > 1;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.numTilesX = tilesX;
            sheet.numTilesY = tilesY;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.startFrame = fixedFrame >= 0f
                ? new ParticleSystem.MinMaxCurve(Mathf.Clamp01(fixedFrame))
                : new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.cycleCount = 1;

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = sortingOrder;
            renderer.enableGPUInstancing = true;
            return particles;
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

        private static void ConfigurePersistentWoundCurve(ParticleSystem system, float scale)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.025f),
                    new GradientAlphaKey(1f, 0.9f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, scale * 0.82f),
                new Keyframe(0.04f, scale),
                new Keyframe(0.9f, scale),
                new Keyframe(1f, scale * 0.72f)));
        }

        private static void SetLocalSurfaceAlignment(ParticleSystem system)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.allowRoll = false;
        }

        private static void SetFireballViewAlignment(ParticleSystem system)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.allowRoll = false;
        }

        private static void ConfigureChargeEnvironmentalDust(
            ParticleSystem system,
            float radius,
            float verticalScale,
            float noiseStrength,
            float startScale)
        {
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = radius;
            shape.scale = new Vector3(1f, verticalScale, 1f);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = noiseStrength;
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.18f;
            ConfigureFadeAndScale(system, startScale, 1f, 1.2f, 0.012f, 0.62f);
        }

        private static void ConfigureStretchRenderer(ParticleSystem system, float lengthScale, float velocityScale)
        {
            ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = lengthScale;
            renderer.velocityScale = velocityScale;
            renderer.cameraVelocityScale = 0f;
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static Texture2D LoadTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                throw new MissingReferenceException($"Missing generated Gouge texture at {path}.");
            }

            return texture;
        }

        private static Material LoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                throw new MissingReferenceException($"Missing shared Charge material at {path}.");
            }

            return material;
        }

        private static void ImportTextures()
        {
            string[] texturePaths =
            {
                WoundTexturePath,
                WeaponTrailTexturePath,
                TearingTrailTexturePath,
                ContactFlashTexturePath,
                BloodTexturePath,
                DebrisSparkTexturePath,
                CriticalRingTexturePath,
                WoundMistTexturePath
            };

            foreach (string path in texturePaths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    throw new InvalidOperationException($"Could not configure texture importer for {path}.");
                }

                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static void ValidateTexture(string path, ICollection<string> failures)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (texture == null || importer == null)
            {
                failures.Add($"Missing texture: {path}");
                return;
            }

            if (importer.alphaSource != TextureImporterAlphaSource.FromInput || !importer.alphaIsTransparency)
            {
                failures.Add($"Texture does not preserve generated alpha: {path}");
            }
        }

        private static void ValidateMaterial(string path, ICollection<string> failures)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null || material.shader == null || !material.shader.isSupported || material.mainTexture == null)
            {
                failures.Add($"Missing or invalid shared Charge material: {path}");
            }
        }

        private static void ValidateRenderers(GameObject prefab, ICollection<string> failures)
        {
            if (prefab == null)
            {
                return;
            }

            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null || !material.shader.isSupported)
                {
                    failures.Add($"{prefab.name}/{renderer.name} has no supported material.");
                    continue;
                }

                if (material.mainTexture == null)
                {
                    failures.Add($"{prefab.name}/{renderer.name} has no visible texture assigned.");
                }
            }
        }

        private static void ValidateParticleVelocityModes(GameObject prefab, ICollection<string> failures)
        {
            if (prefab == null)
            {
                return;
            }

            foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
                if (!velocity.enabled)
                {
                    continue;
                }

                ParticleSystemCurveMode mode = velocity.x.mode;
                if (velocity.y.mode != mode || velocity.z.mode != mode)
                {
                    failures.Add($"{prefab.name}/{particles.name} has incompatible velocity curve modes.");
                }
            }
        }

        private static void EnsureFolders()
        {
            CreateFolder(Root);
            CreateFolder(TextureFolder);
            CreateFolder(MaterialFolder);
            CreateFolder(PrefabFolder);
            CreateFolder(ProfileFolder);
            CreateFolder(DocumentationFolder);
        }

        private static void CreateFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                CreateFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
