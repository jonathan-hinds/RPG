#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Warrior;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class BerzerkitisVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/Berzerkitis";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string DefinitionFolder = "Assets/_Project/VFX/Definitions";
        private const string AbilityFolder = "Assets/_Project/Configs/Abilities";
        private const string ProfilePath = ProfileFolder + "/BerzerkitisVFX_Default.asset";
        private const string DefinitionPath = DefinitionFolder + "/Warrior_Berzerkitis_VFX.asset";
        private const string AbilityPath = AbilityFolder + "/Warrior_Berzerkitis.asset";
        private const string LeftHandPrefabPath = PrefabFolder + "/BerzerkitisHandBuffVFX_Left.prefab";
        private const string RightHandPrefabPath = PrefabFolder + "/BerzerkitisHandBuffVFX_Right.prefab";
        private const string ActivationPrefabPath = PrefabFolder + "/BerzerkitisActivationVFX.prefab";
        private const string CombinedPrefabPath = PrefabFolder + "/BerzerkitisVFX.prefab";
        private const string HandCoreMaterialPath = MaterialFolder + "/Berzerkitis_HandCoreSphere.mat";

        [InitializeOnLoadMethod]
        private static void QueueInitialInstall()
        {
            if (Application.isBatchMode
                || (AssetDatabase.LoadAssetAtPath<GameObject>(CombinedPrefabPath) != null
                    && AssetDatabase.LoadAssetAtPath<GameObject>(ActivationPrefabPath) != null
                    && AssetDatabase.LoadAssetAtPath<Material>(HandCoreMaterialPath) != null))
            {
                return;
            }

            EditorApplication.delayCall += TryRunInitialInstall;
        }

        private static void TryRunInitialInstall()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += TryRunInitialInstall;
                return;
            }

            try
            {
                Install();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/RPG Clone/VFX/Install Berzerkitis VFX")]
        public static void Install()
        {
            EnsureFolders();
            ConfigureSourceTextures();
            CreateEmblemDerivedTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureRuntimeTextures();

            BerzerkitisVFXProfile profile = GetOrCreateProfile();
            Dictionary<string, Material> materials = CreateMaterials(profile);
            GameObject leftHand = CreateHandPrefab(profile, materials, BerzerkitisHandSide.Left, LeftHandPrefabPath);
            GameObject rightHand = CreateHandPrefab(profile, materials, BerzerkitisHandSide.Right, RightHandPrefabPath);
            GameObject combined = CreateCombinedPrefab(profile, materials, leftHand, rightHand);
            CreateActivationPrefab();
            MMOAbilityVfxDefinition definition = ConfigureDefinition(combined);
            ConfigureAbility(definition);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Berzerkitis VFX installed: dedicated activation, emblem, world burst, hand buff prefabs, materials, profile, and ability binding.");
        }

        public static void InstallFromCommandLine()
        {
            Install();
        }

        private static BerzerkitisVFXProfile GetOrCreateProfile()
        {
            BerzerkitisVFXProfile profile = AssetDatabase.LoadAssetAtPath<BerzerkitisVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<BerzerkitisVFXProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            SerializedObject serializedProfile = new(profile);
            serializedProfile.FindProperty("handFlameScale").floatValue = 0.21f;
            serializedProfile.FindProperty("flameHeight").floatValue = 0.36f;
            serializedProfile.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials(BerzerkitisVFXProfile profile)
        {
            Texture2D flames = LoadTexture("Berzerkitis_RageFlameSheet.png");
            Texture2D bodyMasks = LoadTexture("Berzerkitis_BodyMasksAtlas.png");
            Texture2D emblem = LoadTexture("Berzerkitis_Emblem_FullColor.png");
            Texture2D emblemGlow = LoadTexture("Berzerkitis_Emblem_OuterGlow.png");
            Texture2D emberAtlas = LoadTexture("Berzerkitis_EmberTrailRibbonAtlas.png");
            Texture2D shockwave = LoadTexture("Berzerkitis_ShockwaveRing.png");
            Texture2D dustSmoke = LoadTexture("Berzerkitis_DustSmokeAtlas.png");
            Shader spriteShader = Shader.Find("RPG Clone/VFX/Berzerkitis Sprite Unlit");
            Shader heatShader = Shader.Find("RPG Clone/VFX/Berzerkitis Heat Distortion");
            if (spriteShader == null || heatShader == null)
            {
                throw new System.InvalidOperationException("Berzerkitis shaders did not import. Resolve shader compilation errors before installing the package.");
            }

            Material handCoreSphere = CreateMaterial(
                "Berzerkitis_HandCoreSphere",
                spriteShader,
                Texture2D.whiteTexture,
                bodyMasks,
                profile.Colors.WhiteHot,
                true,
                1.65f);
            SetFloat(handCoreSphere, "_PulseFrequency", 5.5f);
            SetFloat(handCoreSphere, "_PulseAmount", 0.08f);
            SetFloat(handCoreSphere, "_DistortionStrength", 0.035f);

            return new Dictionary<string, Material>
            {
                ["HandCoreSphere"] = handCoreSphere,
                ["BrightFlameCore"] = CreateMaterial("Berzerkitis_BrightFlameCore", spriteShader, flames, bodyMasks, profile.Colors.WhiteHot, true, 2.2f),
                ["MainFlames"] = CreateMaterial("Berzerkitis_MainFlames", spriteShader, flames, bodyMasks, Color.white, true, 1.45f),
                ["DarkRageFlames"] = CreateMaterial("Berzerkitis_DarkRageFlames", spriteShader, flames, bodyMasks, profile.Colors.BloodRed, false, 1.15f),
                ["BodyEnvelope"] = CreateMaterial("Berzerkitis_BodyRageEnvelope", spriteShader, bodyMasks, bodyMasks, profile.Colors.DeepOrange, true, 1.1f),
                ["RageSilhouette"] = CreateMaterial("Berzerkitis_RageSilhouette", spriteShader, bodyMasks, bodyMasks, profile.Colors.BloodRed, false, 1f),
                ["BuffEmblem"] = CreateMaterial("Berzerkitis_BuffEmblem", spriteShader, emblem, bodyMasks, Color.white, true, 1.25f),
                ["EmblemGlow"] = CreateMaterial("Berzerkitis_EmblemGlow", spriteShader, emblemGlow, bodyMasks, profile.Colors.DeepOrange, true, 1.6f),
                ["EnergyRibbons"] = CreateMaterial("Berzerkitis_EnergyRibbons", spriteShader, emberAtlas, bodyMasks, Color.white, true, 1.15f),
                ["Embers"] = CreateMaterial("Berzerkitis_Embers", spriteShader, emberAtlas, bodyMasks, Color.white, true, 1.35f),
                ["Sparks"] = CreateMaterial("Berzerkitis_Sparks", spriteShader, emberAtlas, bodyMasks, Color.white, true, 1.65f),
                ["FlameTrails"] = CreateMaterial("Berzerkitis_FlameTrails", spriteShader, emberAtlas, bodyMasks, Color.white, true, 1.3f),
                ["GroundShockwave"] = CreateMaterial("Berzerkitis_GroundShockwave", spriteShader, shockwave, bodyMasks, Color.white, true, 1.1f),
                ["Dust"] = CreateMaterial("Berzerkitis_Dust", spriteShader, dustSmoke, bodyMasks, profile.Colors.Dust, false, 0.82f),
                ["Smoke"] = CreateMaterial("Berzerkitis_Smoke", spriteShader, dustSmoke, bodyMasks, profile.Colors.Charcoal, false, 0.7f),
                ["HeatDistortion"] = CreateMaterial("Berzerkitis_HeatDistortion", heatShader, bodyMasks, bodyMasks, Color.white, false, 1f)
            };
        }

        private static Material CreateMaterial(string name, Shader shader, Texture texture, Texture noise, Color tint, bool additive, float brightness)
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
                material.shader = shader;
            }

            SetTexture(material, "_BaseMap", texture);
            SetTexture(material, "_NoiseMap", noise != null ? noise : texture);
            SetColor(material, "_Tint", tint);
            SetFloat(material, "_Opacity", 1f);
            SetFloat(material, "_Brightness", brightness);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_Dissolve", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateHandPrefab(
            BerzerkitisVFXProfile profile,
            IReadOnlyDictionary<string, Material> materials,
            BerzerkitisHandSide side,
            string path)
        {
            string name = side == BerzerkitisHandSide.Left ? "BerzerkitisHandBuffVFX_Left" : "BerzerkitisHandBuffVFX_Right";
            GameObject root = new(name);
            BerzerkitisHandVFX controller = root.AddComponent<BerzerkitisHandVFX>();

            Renderer core = CreateSphere("Hand Fire Core Sphere", root.transform, materials["HandCoreSphere"], new Vector3(0f, 0.01f, 0f), Vector3.one * 0.21f);
            ParticleSystem main = CreateParticleSystem("Main Hand Flames", root.transform, materials["MainFlames"], true, false, 0.46f, 18f, 0.08f, 0.275f, 0.075f, ParticleSystemShapeType.Sphere, 4, 2);
            ParticleSystem outer = CreateParticleSystem("Outer Rage Flames", root.transform, materials["DarkRageFlames"], true, false, 0.58f, 11f, 0.11f, 0.34f, 0.1f, ParticleSystemShapeType.Sphere, 4, 2);
            LineRenderer ribbon = CreateRing("Hand Energy Wrap", root.transform, materials["EnergyRibbons"], 0.095f, 0.0175f, 36, 0.01f);
            ribbon.transform.localPosition = new Vector3(0f, -0.045f, 0f);
            ParticleSystem embers = CreateParticleSystem("Hand Embers", root.transform, materials["Embers"], true, true, 0.48f, 16f, 0.03f, 0.065f, 0.06f, ParticleSystemShapeType.Sphere, 4, 2);
            ParticleSystem sparks = CreateParticleSystem("Attack Sparks", root.transform, materials["Sparks"], false, true, 0.22f, 0f, 0.02f, 0.065f, 0.04f, ParticleSystemShapeType.Sphere, 4, 2);
            TrailRenderer trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = materials["FlameTrails"];
            trail.time = profile.MotionTrailLifetime;
            trail.widthMultiplier = profile.MotionTrailWidth;
            trail.minVertexDistance = 0.025f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.emitting = false;
            trail.colorGradient = CreateFadeGradient(Color.white, profile.Colors.DeepOrange);

            controller.ConfigureAuthoring(profile, side, core, main, outer, ribbon, embers, sparks, trail);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateCombinedPrefab(
            BerzerkitisVFXProfile profile,
            IReadOnlyDictionary<string, Material> materials,
            GameObject leftHand,
            GameObject rightHand)
        {
            GameObject root = new("BerzerkitisVFX");
            BerzerkitisVFX controller = root.AddComponent<BerzerkitisVFX>();

            Transform attachedRoot = CreateChild("Character Attached Activation", root.transform);
            Renderer chest = CreateQuad("Chest Ignition", attachedRoot, materials["BrightFlameCore"], new Vector3(0f, 1.25f, 0.32f), Quaternion.identity, Vector3.one);
            Renderer envelope = CreateQuad("Full Body Rage Envelope", attachedRoot, materials["BodyEnvelope"], new Vector3(0f, 1.25f, 0.16f), Quaternion.identity, Vector3.one);
            Renderer silhouette = CreateQuad("Rage Silhouette Flash", attachedRoot, materials["RageSilhouette"], new Vector3(0f, 1.38f, 0.12f), Quaternion.identity, Vector3.one);
            Renderer heat = CreateQuad("Heat Distortion", attachedRoot, materials["HeatDistortion"], new Vector3(0f, 1.28f, 0.08f), Quaternion.identity, Vector3.one);

            ParticleSystem columns = CreateParticleSystem("Rising Flame Columns", attachedRoot, materials["MainFlames"], false, false, 0.85f, 0f, 0.5f, 1.25f, 0.75f, ParticleSystemShapeType.Circle, 4, 2);
            ConfigureColumnMotion(columns);
            ParticleSystem activationEmbers = CreateParticleSystem("Activation Embers", attachedRoot, materials["Embers"], false, true, 1.15f, 0f, 0.5f, 0.17f, 0.55f, ParticleSystemShapeType.Sphere, 4, 2);
            ParticleSystem sparks = CreateParticleSystem("Hot Sparks", attachedRoot, materials["Sparks"], false, true, 0.32f, 0f, 0.32f, 0.14f, 0.42f, ParticleSystemShapeType.Sphere, 4, 2);
            ParticleSystem attachedSmoke = CreateParticleSystem("Attached Smoke Accents", attachedRoot, materials["Smoke"], false, false, 0.72f, 0f, 0.65f, 0.52f, 0.48f, ParticleSystemShapeType.Sphere, 4, 2);

            LineRenderer waistBand = CreateRing("Waist Rage Band", attachedRoot, materials["EnergyRibbons"], 0.62f, 0.065f, 56, 0.75f);
            waistBand.transform.localPosition = new Vector3(0f, 0.93f, 0f);
            LineRenderer shoulderBand = CreateRing("Shoulder Rage Band", attachedRoot, materials["EnergyRibbons"], 0.78f, 0.055f, 56, 0.75f);
            shoulderBand.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            LineRenderer leftTransfer = CreateTransferLine("Energy Transfer Left", attachedRoot, materials["EnergyRibbons"]);
            LineRenderer rightTransfer = CreateTransferLine("Energy Transfer Right", attachedRoot, materials["EnergyRibbons"]);

            Transform emblemRoot = CreateChild("Emerging Buff Emblem", attachedRoot);
            Renderer emblemGlow = CreateQuad("Emblem Backing Glow", emblemRoot, materials["EmblemGlow"], new Vector3(0f, 0f, 0.025f), Quaternion.identity, Vector3.one * 1.28f);
            Renderer emblem = CreateQuad("Berzerkitis Emblem", emblemRoot, materials["BuffEmblem"], Vector3.zero, Quaternion.identity, Vector3.one);
            ParticleSystem emblemEmbers = CreateParticleSystem("Emblem Edge Embers", emblemRoot, materials["Embers"], false, true, 0.72f, 0f, 0.35f, 0.11f, 0.45f, ParticleSystemShapeType.Circle, 4, 2);

            Transform worldRoot = CreateChild("World Space Activation", root.transform);
            Renderer shockwave = CreateQuad("Ground Rage Shockwave", worldRoot, materials["GroundShockwave"], new Vector3(0f, 0.045f, 0f), Quaternion.Euler(90f, 0f, 0f), Vector3.one * 0.1f);
            ParticleSystem dust = CreateParticleSystem("Ground Dust Burst", worldRoot, materials["Dust"], false, true, 1.45f, 0f, 0.25f, 0.82f, 0.15f, ParticleSystemShapeType.Circle, 4, 2);
            ConfigureGroundBurst(dust);
            ParticleSystem worldEmbers = CreateParticleSystem("World Embers", worldRoot, materials["Embers"], false, true, 1.35f, 0f, 0.4f, 0.14f, 0.35f, ParticleSystemShapeType.Circle, 4, 2);
            ParticleSystem worldSmoke = CreateParticleSystem("World Smoke", worldRoot, materials["Smoke"], false, true, 1.65f, 0f, 0.35f, 0.65f, 0.2f, ParticleSystemShapeType.Circle, 4, 2);

            controller.ConfigureAuthoring(
                profile,
                leftHand,
                rightHand,
                true,
                attachedRoot,
                chest,
                envelope,
                silhouette,
                heat,
                columns,
                activationEmbers,
                sparks,
                attachedSmoke,
                waistBand,
                shoulderBand,
                leftTransfer,
                rightTransfer,
                emblemRoot,
                emblem,
                emblemGlow,
                emblemEmbers,
                worldRoot,
                shockwave,
                dust,
                worldEmbers,
                worldSmoke);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CombinedPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
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
                null,
                combinedPrefab,
                true,
                false,
                true,
                false,
                Vector3.zero,
                Vector3.zero,
                new Vector3(0f, 1.2f, 0f),
                Vector3.zero,
                0.02f,
                false);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void CreateActivationPrefab()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(CombinedPrefabPath);
            try
            {
                contents.name = "BerzerkitisActivationVFX";
                BerzerkitisVFX controller = contents.GetComponent<BerzerkitisVFX>();
                controller.ConfigureActivationOnly(true);
                PrefabUtility.SaveAsPrefabAsset(contents, ActivationPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureAbility(MMOAbilityVfxDefinition definition)
        {
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null)
            {
                Debug.LogWarning($"Berzerkitis ability was not found at {AbilityPath}; the VFX definition is ready for manual assignment.");
                return;
            }

            ability.SetVisualEffects(definition);
            Sprite generatedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(TextureFolder + "/Berzerkitis_Emblem_FullColor.png");
            SerializedObject serializedAbility = new(ability);
            SerializedProperty iconProperty = serializedAbility.FindProperty("icon");
            if (iconProperty != null && iconProperty.objectReferenceValue == generatedIcon)
            {
                iconProperty.objectReferenceValue = null;
                serializedAbility.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(ability);
        }

        private static Renderer CreateQuad(string name, Transform parent, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = localPosition;
            quad.transform.localRotation = localRotation;
            quad.transform.localScale = localScale;
            Collider collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        private static Renderer CreateSphere(string name, Transform parent, Material material, Vector3 localPosition, Vector3 localScale)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = localPosition;
            sphere.transform.localRotation = Quaternion.identity;
            sphere.transform.localScale = localScale;
            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            MeshRenderer renderer = sphere.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        private static LineRenderer CreateRing(string name, Transform parent, Material material, float radius, float width, int points, float ellipseY)
        {
            GameObject ringObject = new(name);
            ringObject.transform.SetParent(parent, false);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.sharedMaterial = material;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = points;
            ring.widthMultiplier = width;
            ring.textureMode = LineTextureMode.Tile;
            ring.numCornerVertices = 1;
            ring.numCapVertices = 1;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            for (int i = 0; i < points; i++)
            {
                float angle = i / (float)points * Mathf.PI * 2f;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * ellipseY, Mathf.Sin(angle) * radius));
            }

            return ring;
        }

        private static LineRenderer CreateTransferLine(string name, Transform parent, Material material)
        {
            GameObject lineObject = new(name);
            lineObject.transform.SetParent(parent, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 9;
            line.widthMultiplier = 0.055f;
            line.textureMode = LineTextureMode.Tile;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        private static ParticleSystem CreateParticleSystem(
            string name,
            Transform parent,
            Material material,
            bool loop,
            bool worldSpace,
            float lifetime,
            float rate,
            float radius,
            float size,
            float speed,
            ParticleSystemShapeType shapeType,
            int tilesX,
            int tilesY)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem particles = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = loop;
            main.duration = Mathf.Max(0.1f, lifetime);
            main.playOnAwake = false;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.62f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.6f, speed * 1.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.62f, size * 1.18f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.maxParticles = Mathf.Max(64, Mathf.CeilToInt(rate * lifetime * 2f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = loop ? rate : 0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = shapeType;
            shape.radius = radius;
            shape.radiusThickness = 0.35f;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = CreateFadeGradient(Color.white, new Color(0.65f, 0.04f, 0.01f, 0f));

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve scale = new(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.18f, 1f),
                new Keyframe(0.72f, 0.82f),
                new Keyframe(1f, 0.08f));
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, scale);

            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-1.4f, 1.4f);

            if (tilesX > 1 || tilesY > 1)
            {
                ParticleSystem.TextureSheetAnimationModule sheet = particles.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Grid;
                sheet.numTilesX = tilesX;
                sheet.numTilesY = tilesY;
                sheet.animation = ParticleSystemAnimationType.WholeSheet;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 1f);
                sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f);
                sheet.cycleCount = 1;
            }

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.2f;
            return particles;
        }

        private static void ConfigureColumnMotion(ParticleSystem particles)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.78f;
            shape.radiusThickness = 1f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = new ParticleSystem.MinMaxCurve(2.5f, 4.2f);
            velocity.x = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        }

        private static void ConfigureGroundBurst(ParticleSystem particles)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.28f;
            shape.radiusThickness = 1f;
            ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-2.4f, 2.4f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.08f, 0.42f);
            velocity.z = new ParticleSystem.MinMaxCurve(-2.4f, 2.4f);
        }

        private static Gradient CreateFadeGradient(Color start, Color end)
        {
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(start, 0f),
                    new GradientColorKey(start, 0.32f),
                    new GradientColorKey(end, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(start.a, 0.1f),
                    new GradientAlphaKey(end.a, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void ConfigureSourceTextures()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string[] names =
            {
                "Berzerkitis_RageFlameSheet.png",
                "Berzerkitis_BodyMasksAtlas.png",
                "Berzerkitis_Emblem_FullColor.png",
                "Berzerkitis_EmberTrailRibbonAtlas.png",
                "Berzerkitis_ShockwaveRing.png",
                "Berzerkitis_DustSmokeAtlas.png"
            };

            foreach (string name in names)
            {
                ConfigureTextureImporter(TextureFolder + "/" + name, name.Contains("Emblem_FullColor"), true);
            }
        }

        private static void ConfigureRuntimeTextures()
        {
            string[] derived =
            {
                "Berzerkitis_Emblem_GrayscaleMask.png",
                "Berzerkitis_Emblem_EmissiveMask.png",
                "Berzerkitis_Emblem_OuterGlow.png"
            };

            foreach (string name in derived)
            {
                ConfigureTextureImporter(TextureFolder + "/" + name, false, false);
            }
        }

        private static void ConfigureTextureImporter(string path, bool sprite, bool readable)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new FileNotFoundException($"Required Berzerkitis texture is missing: {path}");
            }

            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            importer.spriteImportMode = sprite ? SpriteImportMode.Single : SpriteImportMode.None;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.isReadable = readable;
            importer.mipmapEnabled = true;
            importer.wrapMode = path.Contains("Ribbon") || path.Contains("BodyMasks") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void CreateEmblemDerivedTextures()
        {
            string sourcePath = TextureFolder + "/Berzerkitis_Emblem_FullColor.png";
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            if (source == null || !source.isReadable)
            {
                throw new System.InvalidOperationException("Berzerkitis emblem source must be readable before masks can be generated.");
            }

            int width = source.width;
            int height = source.height;
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] grayscale = new Color32[sourcePixels.Length];
            Color32[] emissive = new Color32[sourcePixels.Length];
            Color32[] glow = new Color32[sourcePixels.Length];

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color32 pixel = sourcePixels[i];
                byte luminance = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f), 0, 255);
                byte alpha = pixel.a;
                grayscale[i] = new Color32(luminance, luminance, luminance, alpha);
                byte emission = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Pow(luminance / 255f, 1.45f) * 255f), 0, 255);
                emissive[i] = new Color32(emission, emission, emission, alpha);
            }

            int[] offsets = { -18, -10, -5, 0, 5, 10, 18 };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    byte maximum = 0;
                    foreach (int offsetY in offsets)
                    {
                        int sampleY = Mathf.Clamp(y + offsetY, 0, height - 1);
                        foreach (int offsetX in offsets)
                        {
                            int sampleX = Mathf.Clamp(x + offsetX, 0, width - 1);
                            maximum = System.Math.Max(maximum, sourcePixels[sampleY * width + sampleX].a);
                        }
                    }

                    byte sourceAlpha = sourcePixels[y * width + x].a;
                    byte halo = (byte)Mathf.Clamp(maximum - sourceAlpha * 0.42f, 0f, 255f);
                    glow[y * width + x] = new Color32(255, 255, 255, halo);
                }
            }

            WriteTexture("Berzerkitis_Emblem_GrayscaleMask.png", width, height, grayscale);
            WriteTexture("Berzerkitis_Emblem_EmissiveMask.png", width, height, emissive);
            WriteTexture("Berzerkitis_Emblem_OuterGlow.png", width, height, glow);
        }

        private static void WriteTexture(string fileName, int width, int height, Color32[] pixels)
        {
            Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(TextureFolder + "/" + fileName, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static Texture2D LoadTexture(string fileName)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + fileName);
            if (texture == null)
            {
                throw new FileNotFoundException($"Required Berzerkitis texture is missing: {fileName}");
            }

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

        private static void EnsureFolders()
        {
            EnsureFolder(RootFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(ProfileFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
