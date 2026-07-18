using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Healing;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class HealingBeamVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/HealingBeam";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/HealingBeamVFX_Default.asset";
        private const string PrefabPath = PrefabFolder + "/HealingBeamVFX.prefab";
        private const string CastingPrefabPath = PrefabFolder + "/HealingBeamChargeVFX.prefab";
        private const string SpellVfxDefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Healing_Beam_VFX.asset";
        private const string SpellAbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Healing_Beam.asset";

        [MenuItem("Tools/RPG Clone/VFX/Build Healing Beam VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();

            HealingBeamVFXProfile profile = LoadOrCreateProfile();
            UpgradeProfileDefaults(profile);
            EditorUtility.SetDirty(profile);
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject castingPrefab = CreateCastingPrefab(profile, materials);
            GameObject prefab = CreatePrefab(profile, materials);
            WireIntoHealingSpell(castingPrefab, prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log($"Built reusable healing beam VFX at '{PrefabPath}'.", prefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Healing Beam VFX")]
        public static void ValidateBuild()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                throw new MissingReferenceException($"Healing beam prefab is missing at {PrefabPath}.");
            }

            string[] sections = { "Beam Effect", "Caster Effect", "Target Effect", "Heal-Tick Burst Effect" };
            foreach (string section in sections)
            {
                if (prefab.transform.Find(section) == null)
                {
                    throw new MissingReferenceException($"Healing beam prefab is missing its '{section}' section.");
                }
            }

            HealingBeamVFX prefabController = prefab.GetComponent<HealingBeamVFX>();
            HealingBeamAbilityVfxAdapter prefabAdapter = prefab.GetComponent<HealingBeamAbilityVfxAdapter>();
            if (prefabController == null || prefabController.Profile == null || prefabAdapter == null)
            {
                throw new MissingReferenceException("Healing beam controller, spell adapter, or profile reference is missing.");
            }

            if (prefab.GetComponentsInChildren<LineRenderer>(true).Length != 3)
            {
                throw new UnityException("Healing beam prefab must contain exactly three beam layers.");
            }

            if (prefab.GetComponentsInChildren<ParticleSystem>(true).Length != 11)
            {
                throw new UnityException("Healing beam prefab must contain exactly eleven lightweight particle systems, including its one-shot target-impact echo.");
            }

            GameObject castingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastingPrefabPath);
            if (castingPrefab == null
                || castingPrefab.GetComponent<HealingBeamChargeVFX>() == null
                || castingPrefab.transform.Find("Caster Ground Buildup") == null
                || castingPrefab.GetComponentsInChildren<ParticleSystem>(true).Length != 5
                || castingPrefab.GetComponentsInChildren<LineRenderer>(true).Length != 0)
            {
                throw new MissingReferenceException("Healing Beam's charge prefab is missing its caster-only orb, nature rings, or ground-dust buildup.");
            }

            ValidateShader($"{ShaderFolder}/HealingBeamUnlit.shader");
            ValidateShader($"{ShaderFolder}/HealingSpriteUnlit.shader");
            ValidateSpellWiring(prefab);
            ValidateChargeLifecycle(castingPrefab);
            ValidateLifecycle(prefab);
            Debug.Log("HealingBeamVFX validation passed: nature charge, caster ground buildup, launch timing, target impact, spell wiring, assets, shaders, lifecycle hooks, and pooling reset are valid.", prefab);
        }

        private static void ValidateShader(string path)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || !shader.isSupported)
            {
                throw new MissingReferenceException($"Healing VFX shader is missing or unsupported: {path}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Shader error in {path}: {message.message}");
                }
            }
        }

        private static void ValidateSpellWiring(GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(SpellVfxDefinitionPath);
            GameObject castingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastingPrefabPath);
            if (definition == null
                || castingPrefab == null
                || definition.CastingPrefab != castingPrefab
                || definition.CastPrefab != prefab
                || definition.HitPrefab != null
                || definition.UseHandCastingAnchors
                || definition.CastPrefabControlsHitTiming)
            {
                throw new MissingReferenceException("Shaman Healing Beam must use its centered caster-only charge while casting and HealingBeamVFX only for its post-cast launch.");
            }
        }

        private static void ValidateChargeLifecycle(GameObject prefab)
        {
            GameObject instance = null;
            GameObject caster = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                caster = new GameObject("HealingBeamChargeVFX Validation Caster") { hideFlags = HideFlags.HideAndDontSave };
                instance.hideFlags = HideFlags.HideAndDontSave;
                caster.transform.position = new Vector3(2f, 0f, -1f);

                HealingBeamChargeVFX controller = instance.GetComponent<HealingBeamChargeVFX>();
                MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(SpellAbilityPath);
                MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(SpellVfxDefinitionPath);
                if (controller == null || ability == null || definition == null)
                {
                    throw new MissingReferenceException("Healing Beam charge lifecycle dependencies are missing.");
                }

                System.Reflection.MethodInfo awake = typeof(HealingBeamChargeVFX).GetMethod(
                    "Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(controller, null);
                MMOAbilityVfxContext context = new(
                    null,
                    ability,
                    definition,
                    caster.transform,
                    null,
                    caster.transform.position,
                    caster.transform.position,
                    false,
                    null);
                controller.Initialize(context);

                System.Reflection.BindingFlags privateInstance =
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(HealingBeamChargeVFX).GetField("chargeStartedAt", privateInstance)
                    ?.SetValue(controller, Time.time - ability.CastTimeSeconds);
                typeof(HealingBeamChargeVFX).GetField("transitionStartedAt", privateInstance)
                    ?.SetValue(controller, Time.time - 1f);
                typeof(HealingBeamChargeVFX).GetMethod("LateUpdate", privateInstance)
                    ?.Invoke(controller, null);

                Transform groundRoot = instance.transform.Find("Caster Ground Buildup");
                Renderer innerRing = groundRoot != null
                    ? groundRoot.Find("Inner Nature Ring")?.GetComponent<Renderer>()
                    : null;
                Renderer outerRing = groundRoot != null
                    ? groundRoot.Find("Outer Nature Ring")?.GetComponent<Renderer>()
                    : null;
                ParticleSystem dust = groundRoot != null
                    ? groundRoot.Find("Encircling Nature Dust")?.GetComponent<ParticleSystem>()
                    : null;
                MaterialPropertyBlock innerRingProperties = new();
                MaterialPropertyBlock outerRingProperties = new();
                innerRing?.GetPropertyBlock(innerRingProperties);
                outerRing?.GetPropertyBlock(outerRingProperties);
                float visibleRingOpacity = Mathf.Max(
                    innerRingProperties.GetFloat("_Opacity"),
                    outerRingProperties.GetFloat("_Opacity"));
                Vector3 expectedGroundPosition = caster.transform.position + (Vector3.up * controller.Profile.CasterGroundVerticalOffset);
                float ringVerticalSeparation = innerRing != null && outerRing != null
                    ? Mathf.Abs(innerRing.transform.localPosition.y - outerRing.transform.localPosition.y)
                    : 0f;
                ParticleSystem.ShapeModule dustShape = dust != null ? dust.shape : default;
                ParticleSystem.MainModule dustMain = dust != null ? dust.main : default;
                ParticleSystemRenderer dustRenderer = dust != null ? dust.GetComponent<ParticleSystemRenderer>() : null;
                Texture dustTexture = dustRenderer != null && dustRenderer.sharedMaterial != null
                    ? dustRenderer.sharedMaterial.GetTexture("_BaseMap")
                    : null;

                if (groundRoot == null
                    || !groundRoot.gameObject.activeSelf
                    || innerRing == null
                    || !innerRing.enabled
                    || outerRing == null
                    || !outerRing.enabled
                    || visibleRingOpacity < 0.45f
                    || ringVerticalSeparation < controller.Profile.CasterBuildupCylinderHeight * 0.4f
                    || Vector3.Distance(groundRoot.position, expectedGroundPosition) > 0.01f
                    || dust == null
                    || !dust.isPlaying
                    || dustMain.maxParticles != controller.Profile.CasterDustParticleCount
                    || dustShape.radiusThickness > 0.06f
                    || dustTexture == null
                    || dustTexture.name != "HealingDust")
                {
                    throw new UnityException("Healing Beam caster buildup did not form its visible rising nature-and-dust cylinder.");
                }
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }

                if (caster != null)
                {
                    Object.DestroyImmediate(caster);
                }
            }
        }

        private static void ValidateLifecycle(GameObject prefab)
        {
            GameObject instance = null;
            GameObject caster = null;
            GameObject target = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                caster = new GameObject("HealingBeamVFX Validation Caster") { hideFlags = HideFlags.HideAndDontSave };
                target = new GameObject("HealingBeamVFX Validation Target") { hideFlags = HideFlags.HideAndDontSave };
                instance.hideFlags = HideFlags.HideAndDontSave;
                caster.transform.position = new Vector3(-1f, 1.2f, 0f);
                target.transform.position = new Vector3(3f, 1f, 1f);

                HealingBeamVFX controller = instance.GetComponent<HealingBeamVFX>();
                System.Reflection.MethodInfo awake = typeof(HealingBeamVFX).GetMethod(
                    "Awake",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                awake?.Invoke(controller, null);
                controller.Play(caster.transform, target.transform);
                if (!controller.IsPlaying || controller.ReadyForPool)
                {
                    throw new UnityException("Healing beam did not enter its playing lifecycle state.");
                }

                System.Reflection.MethodInfo lateUpdate = typeof(HealingBeamVFX).GetMethod(
                    "LateUpdate",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.FieldInfo launchStartedAt = typeof(HealingBeamVFX).GetField(
                    "launchStartedAt",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.FieldInfo stateStartedAt = typeof(HealingBeamVFX).GetField(
                    "stateStartedAt",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                launchStartedAt?.SetValue(controller, Time.time - controller.Profile.BeamLaunchDuration - 0.01f);
                stateStartedAt?.SetValue(controller, Time.time - controller.Profile.FadeInDuration - 0.01f);
                lateUpdate?.Invoke(controller, null);
                LineRenderer[] lines = instance.GetComponentsInChildren<LineRenderer>(true);
                foreach (LineRenderer line in lines)
                {
                    if (line.positionCount < 2
                        || Vector3.Distance(line.GetPosition(0), caster.transform.position) > 0.001f
                        || Vector3.Distance(line.GetPosition(line.positionCount - 1), target.transform.position) > 0.001f)
                    {
                        throw new UnityException("A healing beam layer is not firmly attached to both endpoints.");
                    }
                }

                Vector3 firstTargetPosition = lines[0].GetPosition(lines[0].positionCount - 1);
                target.transform.position += new Vector3(1.25f, 0.4f, -0.35f);
                lateUpdate?.Invoke(controller, null);
                if (Vector3.Distance(firstTargetPosition, lines[0].GetPosition(lines[0].positionCount - 1)) < 0.5f)
                {
                    throw new UnityException("Healing beam endpoint did not follow its moving target attachment.");
                }

                controller.TriggerHealingTick();
                System.Reflection.FieldInfo pulseStartedAt = typeof(HealingBeamVFX).GetField(
                    "pulseStartedAt",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                pulseStartedAt?.SetValue(controller, Time.time - (1f / controller.Profile.PulseSpeed) - 0.01f);
                lateUpdate?.Invoke(controller, null);
                System.Reflection.FieldInfo impactStartedAt = typeof(HealingBeamVFX).GetField(
                    "impactStartedAt",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                impactStartedAt?.SetValue(controller, Time.time - (controller.Profile.TargetImpactEchoDuration * 0.35f));
                lateUpdate?.Invoke(controller, null);
                Renderer impactHalo = instance.transform.Find("Heal-Tick Burst Effect/Expanding Nature Halo")?.GetComponent<Renderer>();
                Transform targetImpactEcho = instance.transform.Find("Heal-Tick Burst Effect/Target Impact Echo");
                Renderer impactOrb = targetImpactEcho?.Find("Impact Orb Flash")?.GetComponent<Renderer>();
                Renderer impactInnerRing = targetImpactEcho?.Find("Impact Inner Nature Ring")?.GetComponent<Renderer>();
                Renderer impactOuterRing = targetImpactEcho?.Find("Impact Outer Nature Ring")?.GetComponent<Renderer>();
                ParticleSystem impactSparkles = targetImpactEcho?.Find("Impact Sparkle Flash")?.GetComponent<ParticleSystem>();
                ParticleSystem impactDust = targetImpactEcho?.Find("Impact Dust Ring")?.GetComponent<ParticleSystem>();
                MaterialPropertyBlock impactOrbProperties = new();
                impactOrb?.GetPropertyBlock(impactOrbProperties);
                if (impactHalo == null
                    || !impactHalo.enabled
                    || targetImpactEcho == null
                    || impactOrb == null
                    || !impactOrb.enabled
                    || impactOrbProperties.GetFloat("_Opacity") < 0.7f
                    || impactInnerRing == null
                    || !impactInnerRing.enabled
                    || impactOuterRing == null
                    || !impactOuterRing.enabled
                    || impactSparkles == null
                    || !impactSparkles.isPlaying
                    || impactDust == null
                    || !impactDust.isPlaying)
                {
                    throw new UnityException("Healing Beam's synchronized orb, rings, sparkles, and dust impact did not trigger when the traveling pulse reached the target.");
                }

                controller.Stop();
                if (controller.IsPlaying || controller.ReadyForPool)
                {
                    throw new UnityException("Healing beam graceful-stop lifecycle state is invalid.");
                }

                controller.StopImmediate();
                if (!controller.ReadyForPool)
                {
                    throw new UnityException("Healing beam did not become ready for pooling after an immediate stop.");
                }

                controller.ResetForPool();
            }
            finally
            {
                if (instance != null)
                {
                    Object.DestroyImmediate(instance);
                }
                if (caster != null)
                {
                    Object.DestroyImmediate(caster);
                }
                if (target != null)
                {
                    Object.DestroyImmediate(target);
                }
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX", "HealingBeam");
            EnsureFolder(RootFolder, "Textures");
            EnsureFolder(RootFolder, "Shaders");
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder, "Profiles");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(RootFolder, "Documentation");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static void ConfigureTextureImporters()
        {
            ConfigureTexture("HealingRibbon.png", TextureWrapMode.Repeat, 512, true, true);
            ConfigureTexture("HealingNoise.png", TextureWrapMode.Repeat, 256, false, false);
            ConfigureTexture("SoftGlow.png", TextureWrapMode.Clamp, 256, true, true);
            ConfigureTexture("HealingSpark.png", TextureWrapMode.Clamp, 256, true, true);
            ConfigureTexture("HealingOrb.png", TextureWrapMode.Clamp, 128, true, true);
            ConfigureTexture("HealingBurst.png", TextureWrapMode.Clamp, 256, true, true);
            ConfigureTexture("GroundRing.png", TextureWrapMode.Clamp, 256, true, true);
            ConfigureTexture("HealingLeaf.png", TextureWrapMode.Clamp, 256, true, true);
            ConfigureTexture("HealingDust.png", TextureWrapMode.Clamp, 256, true, true);
        }

        private static void ConfigureTexture(string fileName, TextureWrapMode wrapMode, int maxSize, bool hasAlpha, bool sRgb)
        {
            string path = $"{TextureFolder}/{fileName}";
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogError($"Required healing VFX texture is missing: {path}");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = wrapMode;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = hasAlpha;
            importer.sRGBTexture = sRgb;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static HealingBeamVFXProfile LoadOrCreateProfile()
        {
            HealingBeamVFXProfile profile = AssetDatabase.LoadAssetAtPath<HealingBeamVFXProfile>(ProfilePath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<HealingBeamVFXProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static void UpgradeProfileDefaults(HealingBeamVFXProfile profile)
        {
            SerializedObject serializedProfile = new(profile);
            UpgradeFloat(serializedProfile, "casterGroundRingSize", 3f, 3.6f);
            UpgradeFloat(serializedProfile, "casterGroundRingOpacity", 0.48f, 0.78f);
            UpgradeInt(serializedProfile, "casterDustParticleCount", 10, 16);
            UpgradeFloat(serializedProfile, "casterDustParticleSize", 0.38f, 0.52f);
            UpgradeFloat(serializedProfile, "casterDustRingRadius", 1.25f, 1.45f);
            UpgradeInt(serializedProfile, "casterDustParticleCount", 16, 40);
            UpgradeFloat(serializedProfile, "casterDustParticleSize", 0.52f, 0.18f);
            UpgradeFloat(serializedProfile, "casterDustRingRadius", 1.45f, 1.55f);
            UpgradeFloat(serializedProfile, "casterGroundRingOpacity", 0.78f, 0.92f);
            UpgradeFloat(serializedProfile, "casterRingRiseSpeed", 0.42f, 0.58f);
            UpgradeFloat(serializedProfile, "casterDustRiseSpeed", 0.84f, 1.1f);
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpgradeFloat(SerializedObject serializedObject, string propertyName, float oldValue, float newValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && Mathf.Approximately(property.floatValue, oldValue))
            {
                property.floatValue = newValue;
            }
        }

        private static void UpgradeInt(SerializedObject serializedObject, string propertyName, int oldValue, int newValue)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && property.intValue == oldValue)
            {
                property.intValue = newValue;
            }
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader beamShader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderFolder}/HealingBeamUnlit.shader");
            Shader spriteShader = AssetDatabase.LoadAssetAtPath<Shader>($"{ShaderFolder}/HealingSpriteUnlit.shader");
            if (beamShader == null || spriteShader == null)
            {
                throw new MissingReferenceException("Healing beam shaders must compile before building the prefab.");
            }

            Texture ribbon = LoadTexture("HealingRibbon.png");
            Texture noise = LoadTexture("HealingNoise.png");
            Texture glow = LoadTexture("SoftGlow.png");
            Texture spark = LoadTexture("HealingSpark.png");
            Texture orb = LoadTexture("HealingOrb.png");
            Texture burst = LoadTexture("HealingBurst.png");
            Texture groundRing = LoadTexture("GroundRing.png");
            Texture leaf = LoadTexture("HealingLeaf.png");
            Texture dust = LoadTexture("HealingDust.png");

            return new Dictionary<string, Material>
            {
                ["MainBeam"] = CreateBeamMaterial("HealingBeam_MainBeam", beamShader, ribbon, noise, new Color(1f, 0.82f, 0.34f, 0.82f)),
                ["BeamGlow"] = CreateBeamMaterial("HealingBeam_BeamGlow", beamShader, ribbon, noise, new Color(1f, 0.68f, 0.18f, 0.3f)),
                ["BeamCore"] = CreateBeamMaterial("HealingBeam_BeamCore", beamShader, ribbon, noise, new Color(1.3f, 1.15f, 0.8f, 0.92f)),
                ["CasterGlow"] = CreateSpriteMaterial("HealingBeam_CasterGlow", spriteShader, glow, new Color(1f, 0.78f, 0.25f, 0.5f), true),
                ["TargetGlow"] = CreateSpriteMaterial("HealingBeam_TargetGlow", spriteShader, glow, new Color(1f, 0.84f, 0.38f, 0.45f), true),
                ["GroundRing"] = CreateSpriteMaterial("HealingBeam_GroundRing", spriteShader, groundRing, new Color(1f, 0.72f, 0.2f, 0.38f), false),
                ["CasterNatureRing"] = CreateSpriteMaterial("HealingBeam_CasterNatureRing", spriteShader, groundRing, new Color(0.72f, 1f, 0.32f, 0.72f), true),
                ["Sparks"] = CreateSpriteMaterial("HealingBeam_Sparks", spriteShader, spark, new Color(1f, 0.88f, 0.48f, 0.9f), true),
                ["Orbs"] = CreateSpriteMaterial("HealingBeam_Orbs", spriteShader, orb, new Color(0.82f, 1f, 0.72f, 0.76f), true),
                ["HealBurst"] = CreateSpriteMaterial("HealingBeam_HealBurst", spriteShader, burst, new Color(1f, 0.88f, 0.45f, 0.8f), true),
                ["Leaves"] = CreateSpriteMaterial("HealingBeam_Leaves", spriteShader, leaf, new Color(0.76f, 1f, 0.52f, 0.92f), true),
                ["Dust"] = CreateSpriteMaterial("HealingBeam_Dust", spriteShader, dust, new Color(0.94f, 0.8f, 0.55f, 0.94f), false)
            };
        }

        private static Texture LoadTexture(string fileName)
        {
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>($"{TextureFolder}/{fileName}");
            if (texture == null)
            {
                throw new MissingReferenceException($"Missing healing VFX texture: {fileName}");
            }
            return texture;
        }

        private static Material CreateBeamMaterial(string name, Shader shader, Texture ribbon, Texture noise, Color tint)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = LoadOrCreateMaterial(path, shader);
            material.SetTexture("_BaseMap", ribbon);
            material.SetTexture("_NoiseMap", noise);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Opacity", 1f);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateSpriteMaterial(string name, Shader shader, Texture texture, Color tint, bool additive)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = LoadOrCreateMaterial(path, shader);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Opacity", 1f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreateMaterial(string path, Shader shader)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            return material;
        }

        private static GameObject CreateCastingPrefab(HealingBeamVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("HealingBeamChargeVFX");
            try
            {
                HealingBeamChargeVFX controller = root.AddComponent<HealingBeamChargeVFX>();
                CasterEffectReferences caster = CreateCasterEffect(root.transform, materials);
                CasterGroundEffectReferences ground = CreateCasterGroundEffect(root.transform, materials);
                controller.ConfigureAuthoring(
                    profile,
                    caster.Root,
                    caster.Glow,
                    caster.OriginFlash,
                    caster.OrbitingStars,
                    caster.InwardOrbs,
                    caster.GatheringLeaves,
                    ground.Root,
                    ground.InnerRing.transform,
                    ground.InnerRing,
                    ground.OuterRing.transform,
                    ground.OuterRing,
                    ground.Dust);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CastingPrefabPath);
                if (prefab == null)
                {
                    throw new UnityException($"Failed to save healing charge VFX prefab at {CastingPrefabPath}.");
                }

                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePrefab(HealingBeamVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("HealingBeamVFX");
            try
            {
                HealingBeamVFX controller = root.AddComponent<HealingBeamVFX>();
                HealingBeamAbilityVfxAdapter adapter = root.AddComponent<HealingBeamAbilityVfxAdapter>();

                Transform beamRoot = CreateSection("Beam Effect", root.transform);
                LineRenderer outerGlow = CreateLine("Outer Glow", beamRoot, materials["BeamGlow"], 0);
                LineRenderer ribbon = CreateLine("Flowing Energy Ribbon", beamRoot, materials["MainBeam"], 1);
                LineRenderer core = CreateLine("Bright Inner Core + Traveling Pulse", beamRoot, materials["BeamCore"], 2);
                MeshRenderer launchHead = CreateQuad(
                    "Mint Launch Head",
                    beamRoot,
                    materials["TargetGlow"],
                    Vector3.zero,
                    Vector3.one,
                    Quaternion.identity,
                    4);

                CasterEffectReferences caster = CreateCasterEffect(root.transform, materials);

                Transform targetRoot = CreateSection("Target Effect", root.transform);
                MeshRenderer targetGlow = CreateQuad("Torso Glow", targetRoot, materials["TargetGlow"], Vector3.zero, Vector3.one * 1.15f, Quaternion.identity, 0);
                MeshRenderer groundRing = CreateQuad("Soft Ground Ring", targetRoot, materials["GroundRing"], new Vector3(0f, -1.05f, 0f), Vector3.one * 2.2f, Quaternion.Euler(90f, 0f, 0f), -1);
                ParticleSystem risingOrbs = CreateRisingOrbs("Rising Orbs", targetRoot, materials["Orbs"]);
                ParticleSystem sparkles = CreateTargetSparkles("Target Sparkles", targetRoot, materials["Sparks"]);

                Transform tickRoot = CreateSection("Heal-Tick Burst Effect", root.transform);
                ParticleSystem burst = CreateHealBurst("Soft Petal Burst", tickRoot, materials["HealBurst"]);
                ParticleSystem tickSparks = CreateTickSparks("Tick Sparks", tickRoot, materials["Sparks"]);
                ParticleSystem impactLeaves = CreateImpactLeaves("Restorative Leaf Burst", tickRoot, materials["Leaves"]);
                MeshRenderer impactHalo = CreateQuad(
                    "Expanding Nature Halo",
                    tickRoot,
                    materials["HealBurst"],
                    Vector3.zero,
                    Vector3.one,
                    Quaternion.identity,
                    4);
                TargetImpactEchoReferences targetImpactEcho = CreateTargetImpactEcho(tickRoot, materials);

                controller.ConfigureAuthoring(
                    profile,
                    beamRoot,
                    outerGlow,
                    ribbon,
                    core,
                    launchHead,
                    caster.Root,
                    caster.Glow,
                    caster.OriginFlash,
                    caster.OrbitingStars,
                    caster.InwardOrbs,
                    caster.GatheringLeaves,
                    targetRoot,
                    targetGlow,
                    groundRing.transform,
                    groundRing,
                    risingOrbs,
                    sparkles,
                    tickRoot,
                    burst,
                    tickSparks,
                    impactLeaves,
                    impactHalo.transform,
                    impactHalo,
                    targetImpactEcho.Root,
                    targetImpactEcho.Orb,
                    targetImpactEcho.InnerRing.transform,
                    targetImpactEcho.InnerRing,
                    targetImpactEcho.OuterRing.transform,
                    targetImpactEcho.OuterRing,
                    targetImpactEcho.Sparkles,
                    targetImpactEcho.Dust);
                adapter.ConfigureAuthoring(controller);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    throw new UnityException($"Failed to save healing VFX prefab at {PrefabPath}.");
                }
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static CasterEffectReferences CreateCasterEffect(Transform parent, IReadOnlyDictionary<string, Material> materials)
        {
            Transform root = CreateSection("Caster Effect", parent);
            MeshRenderer glow = CreateQuad("Caster Glow", root, materials["CasterGlow"], Vector3.zero, Vector3.one * 0.72f, Quaternion.identity, 0);
            ParticleSystem originFlash = CreateFlash("Origin Flash", root, materials["CasterGlow"]);
            ParticleSystem orbitingStars = CreateOrbitStars("Orbiting Stars", root, materials["Sparks"]);
            ParticleSystem inwardOrbs = CreateInwardOrbs("Inward Spiral Orbs", root, materials["Orbs"]);
            ParticleSystem gatheringLeaves = CreateGatheringLeaves("Gathering Leaves", root, materials["Leaves"]);
            return new CasterEffectReferences(root, glow, originFlash, orbitingStars, inwardOrbs, gatheringLeaves);
        }

        private static CasterGroundEffectReferences CreateCasterGroundEffect(
            Transform parent,
            IReadOnlyDictionary<string, Material> materials)
        {
            Transform root = CreateSection("Caster Ground Buildup", parent);
            MeshRenderer innerRing = CreateQuad(
                "Inner Nature Ring",
                root,
                materials["CasterNatureRing"],
                new Vector3(0f, 0.012f, 0f),
                Vector3.one,
                Quaternion.Euler(90f, 0f, 0f),
                -2);
            MeshRenderer outerRing = CreateQuad(
                "Outer Nature Ring",
                root,
                materials["CasterNatureRing"],
                new Vector3(0f, 0.006f, 0f),
                Vector3.one,
                Quaternion.Euler(90f, 0f, 0f),
                -1);
            ParticleSystem dust = CreateGroundDust("Encircling Nature Dust", root, materials["Dust"]);
            return new CasterGroundEffectReferences(root, innerRing, outerRing, dust);
        }

        private static TargetImpactEchoReferences CreateTargetImpactEcho(
            Transform parent,
            IReadOnlyDictionary<string, Material> materials)
        {
            Transform root = CreateSection("Target Impact Echo", parent);
            MeshRenderer orb = CreateQuad(
                "Impact Orb Flash",
                root,
                materials["CasterGlow"],
                Vector3.zero,
                Vector3.one * 0.72f,
                Quaternion.identity,
                6);
            MeshRenderer innerRing = CreateQuad(
                "Impact Inner Nature Ring",
                root,
                materials["CasterNatureRing"],
                new Vector3(0f, -1.05f, 0f),
                Vector3.one,
                Quaternion.Euler(90f, 0f, 0f),
                4);
            MeshRenderer outerRing = CreateQuad(
                "Impact Outer Nature Ring",
                root,
                materials["CasterNatureRing"],
                new Vector3(0f, -1.05f, 0f),
                Vector3.one,
                Quaternion.Euler(90f, 0f, 0f),
                5);
            ParticleSystem sparkles = CreateTargetImpactSparkles("Impact Sparkle Flash", root, materials["Sparks"]);
            ParticleSystem dust = CreateTargetImpactDust("Impact Dust Ring", root, materials["Dust"]);
            dust.transform.localPosition = new Vector3(0f, -1.05f, 0f);
            return new TargetImpactEchoReferences(root, orb, innerRing, outerRing, sparkles, dust);
        }

        private static void WireIntoHealingSpell(GameObject castingPrefab, GameObject prefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(SpellVfxDefinitionPath);
            if (definition == null)
            {
                throw new MissingReferenceException($"Healing Beam VFX definition is missing: {SpellVfxDefinitionPath}");
            }

            if (castingPrefab == null)
            {
                throw new MissingReferenceException($"Healing Beam hand-charge prefab is missing: {CastingPrefabPath}");
            }

            definition.Configure(
                castingPrefab,
                prefab,
                null,
                true,
                false,
                true,
                false,
                new Vector3(0f, 1.15f, 0.42f),
                Vector3.zero,
                new Vector3(0f, 1.18f, 0.48f),
                new Vector3(0f, 0.85f, 0f),
                0f,
                false);
            EditorUtility.SetDirty(definition);
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static LineRenderer CreateLine(string name, Transform parent, Material material, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 12;
            // Keep UV.x normalized for endpoint fading and traveling pulses; the shader performs distance tiling.
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 2;
            line.numCapVertices = 3;
            line.generateLightingData = false;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.allowOcclusionWhenDynamic = false;
            line.sortingOrder = sortingOrder;
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.05f),
                new Keyframe(0.12f, 0.76f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.88f, 0.76f),
                new Keyframe(1f, 0.05f));
            return line;
        }

        private static MeshRenderer CreateQuad(string name, Transform parent, Material material, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            child.transform.localRotation = localRotation;
            child.transform.localScale = localScale;
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static ParticleSystem CreateFlash(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, false, 0.28f, 0.22f, 0f, 0.42f, new Color(1f, 0.9f, 0.52f, 0.9f), 2);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
            ConfigureFadeAndScale(particleSystem, 0.2f, 1f, 1.75f);
            return particleSystem;
        }

        private static ParticleSystem CreateOrbitStars(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, true, 2f, 1.35f, 0f, 0.14f, new Color(1f, 0.86f, 0.36f, 0.9f), 8);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 4f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.orbitalY = 1.2f;
            ConfigureFadeAndScale(particleSystem, 0.1f, 1f, 0.25f);
            return particleSystem;
        }

        private static ParticleSystem CreateInwardOrbs(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, true, 1.6f, 1.1f, 0f, 0.1f, new Color(0.72f, 1f, 0.64f, 0.72f), 8);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 4f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.38f;
            shape.radiusThickness = 0.05f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = -0.32f;
            velocity.orbitalY = 0.7f;
            ConfigureFadeAndScale(particleSystem, 0.25f, 1f, 0.15f);
            return particleSystem;
        }

        private static ParticleSystem CreateGatheringLeaves(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(
                name,
                parent,
                material,
                true,
                2f,
                1.4f,
                0f,
                0.22f,
                new Color(0.72f, 1f, 0.48f, 0.88f),
                8);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 2f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.42f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.radial = -0.18f;
            velocity.orbitalY = 0.85f;
            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = 1.2f;
            ConfigureFadeAndScale(particleSystem, 0.35f, 1f, 0.2f);
            return particleSystem;
        }

        private static ParticleSystem CreateGroundDust(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(
                name,
                parent,
                material,
                true,
                2.5f,
                2.25f,
                0f,
                0.18f,
                new Color(0.8f, 0.7f, 0.48f, 0.9f),
                40);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 24f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.55f;
            shape.radiusThickness = 0.05f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.69f, 0.99f);
            velocity.orbitalY = 0.12f;
            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.1f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.12f;
            ConfigureFadeAndScale(particleSystem, 0.24f, 1f, 0.38f);
            return particleSystem;
        }

        private static ParticleSystem CreateRisingOrbs(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, true, 2f, 1.65f, 0.12f, 0.12f, new Color(0.78f, 1f, 0.66f, 0.7f), 12);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 5f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.48f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
            velocity.orbitalY = 0.18f;
            ConfigureFadeAndScale(particleSystem, 0.15f, 1f, 0.35f);
            return particleSystem;
        }

        private static ParticleSystem CreateTargetSparkles(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, true, 2f, 1.25f, 0.08f, 0.13f, new Color(1f, 0.9f, 0.45f, 0.82f), 8);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 2.5f;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            ConfigureFadeAndScale(particleSystem, 0.05f, 1f, 0.05f);
            return particleSystem;
        }

        private static ParticleSystem CreateTargetImpactSparkles(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(
                name,
                parent,
                material,
                false,
                0.7f,
                0.58f,
                0.72f,
                0.28f,
                new Color(1f, 0.9f, 0.46f, 0.96f),
                24);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)12) });
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.52f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.72f);
            velocity.orbitalY = 0.35f;
            ConfigureFadeAndScale(particleSystem, 0.12f, 1f, 0.08f);
            return particleSystem;
        }

        private static ParticleSystem CreateTargetImpactDust(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(
                name,
                parent,
                material,
                false,
                0.7f,
                0.58f,
                0f,
                0.18f,
                new Color(0.82f, 0.71f, 0.48f, 0.9f),
                40);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)40) });
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.55f;
            shape.radiusThickness = 0.05f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(2.25f, 3.45f);
            velocity.orbitalY = 0.16f;
            ParticleSystem.NoiseModule noise = particleSystem.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.1f;
            noise.frequency = 0.35f;
            ConfigureFadeAndScale(particleSystem, 0.2f, 1f, 0.28f);
            return particleSystem;
        }

        private static ParticleSystem CreateHealBurst(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, false, 0.55f, 0.46f, 0f, 0.72f, new Color(1f, 0.86f, 0.38f, 0.78f), 2);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)1) });
            ConfigureFadeAndScale(particleSystem, 0.2f, 1.65f, 1.9f);
            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = 0.9f;
            return particleSystem;
        }

        private static ParticleSystem CreateTickSparks(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(name, parent, material, false, 0.9f, 0.75f, 0.62f, 0.15f, new Color(1f, 0.9f, 0.45f, 0.92f), 12);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)7) });
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.28f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            velocity.orbitalY = 0.22f;
            ConfigureFadeAndScale(particleSystem, 0.15f, 1f, 0.15f);
            return particleSystem;
        }

        private static ParticleSystem CreateImpactLeaves(string name, Transform parent, Material material)
        {
            ParticleSystem particleSystem = CreateParticleSystem(
                name,
                parent,
                material,
                false,
                0.9f,
                0.82f,
                0.52f,
                0.22f,
                new Color(0.74f, 1f, 0.48f, 0.92f),
                16);
            ParticleSystem.EmissionModule emission = particleSystem.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)7) });
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
            velocity.enabled = true;
            velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.72f);
            velocity.orbitalY = 0.3f;
            ParticleSystem.RotationOverLifetimeModule rotation = particleSystem.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = 1.6f;
            ConfigureFadeAndScale(particleSystem, 0.3f, 1f, 0.2f);
            return particleSystem;
        }

        private static ParticleSystem CreateParticleSystem(string name, Transform parent, Material material, bool loop, float duration, float lifetime, float speed, float size, Color color, int maxParticles)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem particleSystem = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particleSystem.main;
            main.duration = duration;
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.stopAction = ParticleSystemStopAction.None;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.enabled = false;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 3;
            return particleSystem;
        }

        private static void ConfigureFadeAndScale(ParticleSystem particleSystem, float startScale, float middleScale, float endScale)
        {
            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, startScale),
                new Keyframe(0.35f, middleScale),
                new Keyframe(1f, endScale)));
        }

        private readonly struct CasterEffectReferences
        {
            public CasterEffectReferences(
                Transform root,
                MeshRenderer glow,
                ParticleSystem originFlash,
                ParticleSystem orbitingStars,
                ParticleSystem inwardOrbs,
                ParticleSystem gatheringLeaves)
            {
                Root = root;
                Glow = glow;
                OriginFlash = originFlash;
                OrbitingStars = orbitingStars;
                InwardOrbs = inwardOrbs;
                GatheringLeaves = gatheringLeaves;
            }

            public Transform Root { get; }
            public MeshRenderer Glow { get; }
            public ParticleSystem OriginFlash { get; }
            public ParticleSystem OrbitingStars { get; }
            public ParticleSystem InwardOrbs { get; }
            public ParticleSystem GatheringLeaves { get; }
        }

        private readonly struct CasterGroundEffectReferences
        {
            public CasterGroundEffectReferences(
                Transform root,
                MeshRenderer innerRing,
                MeshRenderer outerRing,
                ParticleSystem dust)
            {
                Root = root;
                InnerRing = innerRing;
                OuterRing = outerRing;
                Dust = dust;
            }

            public Transform Root { get; }
            public MeshRenderer InnerRing { get; }
            public MeshRenderer OuterRing { get; }
            public ParticleSystem Dust { get; }
        }

        private readonly struct TargetImpactEchoReferences
        {
            public TargetImpactEchoReferences(
                Transform root,
                MeshRenderer orb,
                MeshRenderer innerRing,
                MeshRenderer outerRing,
                ParticleSystem sparkles,
                ParticleSystem dust)
            {
                Root = root;
                Orb = orb;
                InnerRing = innerRing;
                OuterRing = outerRing;
                Sparkles = sparkles;
                Dust = dust;
            }

            public Transform Root { get; }
            public MeshRenderer Orb { get; }
            public MeshRenderer InnerRing { get; }
            public MeshRenderer OuterRing { get; }
            public ParticleSystem Sparkles { get; }
            public ParticleSystem Dust { get; }
        }
    }
}
