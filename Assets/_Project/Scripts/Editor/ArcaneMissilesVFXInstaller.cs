#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.ArcaneMissiles;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class ArcaneMissilesVFXInstaller
    {
        private const string RootFolder = "Assets/_Project/VFX/ArcaneMissiles";
        private const string TextureFolder = RootFolder + "/Textures";
        private const string SourceFolder = TextureFolder + "/Sources";
        private const string ShaderFolder = RootFolder + "/Shaders";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string ProfileFolder = RootFolder + "/Profiles";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string SourceAtlasPath = SourceFolder + "/ArcaneMissiles_SourceAtlas.png";
        private const string ShaderPath = ShaderFolder + "/ArcaneMissilesLayeredUnlit.shader";
        private const string ProfilePath = ProfileFolder + "/ArcaneMissilesVFX_Default.asset";
        private const string CastingPrefabPath = PrefabFolder + "/ArcaneMissilesVFX.prefab";
        private const string ProjectilePrefabPath = PrefabFolder + "/ArcaneMissileProjectileVFX.prefab";
        private const string ImpactPrefabPath = PrefabFolder + "/ArcaneMissilesImpactVFX.prefab";
        private const string InterruptPrefabPath = PrefabFolder + "/ArcaneMissilesInterruptVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Mage_Arcane_Missile_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Mage_Arcane_Missile.asset";

        private static readonly string[] TextureNames =
        {
            "ArcaneMissiles_OrbBody.png",
            "ArcaneMissiles_WhiteCore.png",
            "ArcaneMissiles_RuneAtlas.png",
            "ArcaneMissiles_BrokenRunicRing.png",
            "ArcaneMissiles_Fragments.png",
            "ArcaneMissiles_ProjectileBody.png",
            "ArcaneMissiles_PurpleFlares.png",
            "ArcaneMissiles_Shell.png",
            "ArcaneMissiles_WhiteCoreTrail.png",
            "ArcaneMissiles_BlueTrailRibbon.png",
            "ArcaneMissiles_PurpleTrailRibbon.png",
            "ArcaneMissiles_Vapor.png",
            "ArcaneMissiles_ImpactExplosion.png",
            "ArcaneMissiles_ShockRing.png",
            "ArcaneMissiles_EnergySpikes.png",
            "ArcaneMissiles_SparksMotes.png",
            "ArcaneMissiles_Distortion.png",
            "ArcaneMissiles_HandGlow.png",
            "ArcaneMissiles_EnergyConnection.png",
            "ArcaneMissiles_ChannelCircle.png"
        };

        [MenuItem("Tools/RPG Clone/VFX/Install Arcane Missiles VFX")]
        public static void Install()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            GenerateRuntimeTextures();
            ArcaneMissilesVFXProfile profile = LoadOrCreateProfile();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject projectilePrefab = CreateProjectilePrefab(profile, materials);
            GameObject impactPrefab = CreateImpactPrefab(profile, materials);
            GameObject interruptPrefab = CreateInterruptPrefab(profile, materials);
            GameObject castingPrefab = CreateCastingPrefab(profile, materials, projectilePrefab, impactPrefab, interruptPrefab);
            WireAbility(castingPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = castingPrefab;
            EditorGUIUtility.PingObject(castingPrefab);
            Debug.Log($"Installed Arcane Missiles VFX package and wired replicated presentation at '{CastingPrefabPath}'.", castingPrefab);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Arcane Missiles VFX")]
        public static void ValidatePackage()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null || !shader.isSupported)
            {
                throw new MissingReferenceException($"Arcane Missiles shader is missing or unsupported: {ShaderPath}");
            }

            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == ShaderCompilerMessageSeverity.Error)
                {
                    throw new UnityException($"Arcane Missiles shader error: {message.message}");
                }
            }

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(SourceAtlasPath) == null)
            {
                throw new MissingReferenceException("Arcane Missiles generated source atlas is missing.");
            }

            foreach (string textureName in TextureNames)
            {
                if (AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{textureName}") == null)
                {
                    throw new MissingReferenceException($"Arcane Missiles runtime texture is missing: {textureName}");
                }
            }

            ArcaneMissilesVFXProfile profile = AssetDatabase.LoadAssetAtPath<ArcaneMissilesVFXProfile>(ProfilePath);
            GameObject castingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CastingPrefabPath);
            GameObject projectilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectilePrefabPath);
            GameObject impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
            GameObject interruptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InterruptPrefabPath);
            if (profile == null || castingPrefab == null || projectilePrefab == null || impactPrefab == null || interruptPrefab == null)
            {
                throw new MissingReferenceException("Arcane Missiles profile or one of its four reusable prefabs is missing.");
            }

            ArcaneMissilesVFX controller = castingPrefab.GetComponent<ArcaneMissilesVFX>();
            if (controller == null || controller.Profile != profile
                || castingPrefab.GetComponentsInChildren<ArcaneMissilesFabricatorVFX>(true).Length != 3
                || castingPrefab.GetComponentsInChildren<LineRenderer>(true).Length != 4)
            {
                throw new UnityException("Arcane Missiles casting prefab must contain its profile, three fabricators, and four energy ribbons.");
            }

            if (projectilePrefab.GetComponent<ArcaneMissileProjectileVFX>() == null
                || projectilePrefab.GetComponentsInChildren<TrailRenderer>(true).Length != 4
                || impactPrefab.GetComponent<ArcaneMissilesImpactVFX>() == null
                || interruptPrefab.GetComponent<ArcaneMissilesInterruptVFX>() == null)
            {
                throw new UnityException("Arcane Missiles projectile, trail, impact, or reusable interruption prefab is invalid.");
            }

            foreach (ParticleSystem particles in castingPrefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particles.main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    throw new UnityException($"Detached Arcane Missiles particles must simulate in world space: {particles.name}");
                }
            }

            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (ability == null || definition == null || ability.VisualEffects != definition
                || definition.CastingPrefab != castingPrefab
                || definition.CastPrefab != null
                || definition.HitPrefab != null
                || definition.UseHandCastingAnchors
                || !ability.IsChanneled
                || !ability.InterruptOnMovement
                || Mathf.Abs(ability.CastTimeSeconds - 5f) > 0.001f)
            {
                throw new UnityException("Arcane Missile is not wired as a five-second replicated channel through the dedicated casting package.");
            }

            Debug.Log("Arcane Missiles VFX validation passed: generated textures, layered materials, pooled prefabs, world-space particles, interruption package, and ability wiring are valid.", castingPrefab);
        }

        private static void GenerateRuntimeTextures()
        {
            TextureImporter sourceImporter = AssetImporter.GetAtPath(SourceAtlasPath) as TextureImporter;
            if (sourceImporter == null)
            {
                throw new FileNotFoundException($"Generated Arcane Missiles source atlas is missing: {SourceAtlasPath}");
            }

            sourceImporter.textureType = TextureImporterType.Default;
            sourceImporter.isReadable = true;
            sourceImporter.sRGBTexture = true;
            sourceImporter.mipmapEnabled = false;
            sourceImporter.textureCompression = TextureImporterCompression.Uncompressed;
            sourceImporter.maxTextureSize = 2048;
            sourceImporter.SaveAndReimport();
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceAtlasPath);
            if (source == null) throw new FileNotFoundException(SourceAtlasPath);

            int cellHeight = source.height / 4;
            int outputSize = Mathf.Min(512, cellHeight);
            for (int index = 0; index < TextureNames.Length; index++)
            {
                int column = index % 5;
                int rowFromTop = index / 5;
                int x0 = Mathf.FloorToInt(column * source.width / 5f);
                int x1 = Mathf.FloorToInt((column + 1) * source.width / 5f);
                int y0 = Mathf.FloorToInt((3 - rowFromTop) * source.height / 4f);
                int sourceWidth = x1 - x0;
                int cropSize = Mathf.Min(sourceWidth, cellHeight);
                int cropX = x0 + (sourceWidth - cropSize) / 2;
                Color[] pixels = source.GetPixels(cropX, y0, cropSize, cropSize);
                Texture2D output = new(outputSize, outputSize, TextureFormat.RGBA32, false);
                Color[] resized = ResampleAndExtractAlpha(pixels, cropSize, outputSize);
                output.SetPixels(resized);
                output.Apply(false, false);
                File.WriteAllBytes($"{TextureFolder}/{TextureNames[index]}", output.EncodeToPNG());
                Object.DestroyImmediate(output);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int index = 0; index < TextureNames.Length; index++)
            {
                string path = $"{TextureFolder}/{TextureNames[index]}";
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                bool repeat = index is 8 or 9 or 10 or 16 or 18;
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = index != 16;
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }
        }

        private static Color[] ResampleAndExtractAlpha(Color[] source, int sourceSize, int outputSize)
        {
            Color[] output = new Color[outputSize * outputSize];
            for (int y = 0; y < outputSize; y++)
            {
                int sy = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) * sourceSize / outputSize), 0, sourceSize - 1);
                for (int x = 0; x < outputSize; x++)
                {
                    int sx = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) * sourceSize / outputSize), 0, sourceSize - 1);
                    Color color = source[sy * sourceSize + sx];
                    float luminance = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
                    color.a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.012f, 0.18f, luminance));
                    output[y * outputSize + x] = color;
                }
            }

            return output;
        }

        private static ArcaneMissilesVFXProfile LoadOrCreateProfile()
        {
            ArcaneMissilesVFXProfile profile = AssetDatabase.LoadAssetAtPath<ArcaneMissilesVFXProfile>(ProfilePath);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<ArcaneMissilesVFXProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (shader == null) throw new MissingReferenceException(ShaderPath);
            Texture distortion = LoadTexture("ArcaneMissiles_Distortion.png");
            return new Dictionary<string, Material>
            {
                ["WhiteCore"] = CreateMaterial("ArcaneMissiles_WhiteCore", shader, "ArcaneMissiles_WhiteCore.png", distortion, Color.white, true, 5f, Vector2.zero, 0.012f, 0.05f),
                ["HandGlow"] = CreateMaterial("ArcaneMissiles_HandGlow", shader, "ArcaneMissiles_HandGlow.png", distortion, new Color(0.45f, 0.72f, 1.2f, 1f), true, 3.2f, new Vector2(0.02f, 0.04f), 0.022f, 0.04f),
                ["BlueOrb"] = CreateMaterial("ArcaneMissiles_BlueOrbEnergy", shader, "ArcaneMissiles_OrbBody.png", distortion, new Color(0.12f, 0.72f, 1.25f, 1f), true, 1.7f, new Vector2(0.03f, 0.05f), 0.035f, 0.12f),
                ["PurpleOrb"] = CreateMaterial("ArcaneMissiles_PurpleOrbEnergy", shader, "ArcaneMissiles_OrbBody.png", distortion, new Color(0.72f, 0.16f, 1.2f, 0.78f), true, 1.35f, new Vector2(-0.05f, 0.025f), 0.05f, 0.1f),
                ["Shell"] = CreateMaterial("ArcaneMissiles_ArcaneShell", shader, "ArcaneMissiles_Shell.png", distortion, new Color(0.62f, 0.86f, 1.2f, 0.72f), false, 1.1f, new Vector2(0.01f, -0.018f), 0.022f, 0.16f),
                ["Rune"] = CreateMaterial("ArcaneMissiles_InternalRunes", shader, "ArcaneMissiles_RuneAtlas.png", distortion, Color.white, true, 2.3f, Vector2.zero, 0.006f, 0.03f),
                ["Ring"] = CreateMaterial("ArcaneMissiles_RunicRings", shader, "ArcaneMissiles_BrokenRunicRing.png", distortion, new Color(0.26f, 0.7f, 1.3f, 0.9f), true, 1.7f, Vector2.zero, 0.012f, 0.04f),
                ["Connection"] = CreateMaterial("ArcaneMissiles_EnergyConnections", shader, "ArcaneMissiles_EnergyConnection.png", distortion, new Color(0.2f, 0.55f, 1.2f, 0.85f), true, 1.7f, new Vector2(0.9f, 0f), 0.025f, 0.02f),
                ["ProjectileCore"] = CreateMaterial("ArcaneMissiles_ProjectileCore", shader, "ArcaneMissiles_WhiteCore.png", distortion, Color.white, true, 5.5f, Vector2.zero, 0.012f, 0.03f),
                ["ProjectileBody"] = CreateMaterial("ArcaneMissiles_ProjectileBody", shader, "ArcaneMissiles_ProjectileBody.png", distortion, new Color(0.1f, 0.64f, 1.25f, 1f), true, 1.8f, new Vector2(-0.18f, 0f), 0.028f, 0.04f),
                ["ProjectileFlares"] = CreateMaterial("ArcaneMissiles_ProjectileFlares", shader, "ArcaneMissiles_PurpleFlares.png", distortion, new Color(0.7f, 0.14f, 1.2f, 0.85f), true, 1.45f, new Vector2(-0.28f, 0f), 0.045f, 0.03f),
                ["TrailCore"] = CreateMaterial("ArcaneMissiles_WhiteCoreTrail", shader, "ArcaneMissiles_WhiteCoreTrail.png", distortion, Color.white, true, 4.2f, new Vector2(-1.8f, 0f), 0.01f, 0f),
                ["TrailBlue"] = CreateMaterial("ArcaneMissiles_BlueTrailRibbon", shader, "ArcaneMissiles_BlueTrailRibbon.png", distortion, new Color(0.08f, 0.64f, 1.2f, 0.86f), true, 1.7f, new Vector2(-1.1f, 0f), 0.025f, 0f),
                ["TrailPurple"] = CreateMaterial("ArcaneMissiles_PurpleTrailRibbon", shader, "ArcaneMissiles_PurpleTrailRibbon.png", distortion, new Color(0.68f, 0.13f, 1.18f, 0.72f), true, 1.4f, new Vector2(-0.8f, 0f), 0.04f, 0f),
                ["Fragments"] = CreateMaterial("ArcaneMissiles_RuneFragments", shader, "ArcaneMissiles_Fragments.png", distortion, new Color(0.55f, 0.3f, 1.2f, 0.9f), true, 1.6f, Vector2.zero, 0.015f, 0.08f),
                ["Vapor"] = CreateMaterial("ArcaneMissiles_ArcaneVapor", shader, "ArcaneMissiles_Vapor.png", distortion, new Color(0.24f, 0.2f, 0.72f, 0.42f), false, 1.05f, new Vector2(0.04f, 0.025f), 0.055f, 0.28f),
                ["ImpactFlash"] = CreateMaterial("ArcaneMissiles_ImpactFlash", shader, "ArcaneMissiles_WhiteCore.png", distortion, Color.white, true, 6f, Vector2.zero, 0.018f, 0.03f),
                ["Explosion"] = CreateMaterial("ArcaneMissiles_Explosion", shader, "ArcaneMissiles_ImpactExplosion.png", distortion, new Color(0.23f, 0.55f, 1.2f, 0.95f), true, 2f, new Vector2(0.02f, -0.03f), 0.05f, 0.07f),
                ["ShockRing"] = CreateMaterial("ArcaneMissiles_ShockRings", shader, "ArcaneMissiles_ShockRing.png", distortion, new Color(0.38f, 0.62f, 1.25f, 0.86f), true, 1.8f, Vector2.zero, 0.02f, 0.04f),
                ["Spikes"] = CreateMaterial("ArcaneMissiles_EnergySpikes", shader, "ArcaneMissiles_EnergySpikes.png", distortion, new Color(0.24f, 0.52f, 1.2f, 0.9f), true, 1.8f, Vector2.zero, 0.022f, 0.06f),
                ["Sparks"] = CreateMaterial("ArcaneMissiles_SparksMotes", shader, "ArcaneMissiles_SparksMotes.png", distortion, Color.white, true, 2.2f, Vector2.zero, 0.01f, 0.04f),
                ["ChannelCircle"] = CreateMaterial("ArcaneMissiles_ChannelCircle", shader, "ArcaneMissiles_ChannelCircle.png", distortion, new Color(0.48f, 0.18f, 1.1f, 0.74f), true, 1.25f, Vector2.zero, 0.018f, 0.12f),
                ["Distortion"] = CreateMaterial("ArcaneMissiles_Distortion", shader, "ArcaneMissiles_Distortion.png", distortion, new Color(0.18f, 0.32f, 0.7f, 0.22f), false, 0.75f, new Vector2(0.08f, 0.1f), 0.12f, 0.22f)
            };
        }

        private static Material CreateMaterial(string name, Shader shader, string textureName, Texture noise, Color tint, bool additive, float brightness, Vector2 scroll, float distortion, float depthFade)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;

            material.SetTexture("_BaseMap", LoadTexture(textureName));
            material.SetTexture("_NoiseMap", noise);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Opacity", 1f);
            material.SetFloat("_Brightness", brightness);
            material.SetVector("_ScrollSpeed", new Vector4(scroll.x, scroll.y, 0f, 0f));
            material.SetFloat("_DistortionStrength", distortion);
            material.SetFloat("_Dissolve", 0f);
            material.SetFloat("_FlickerSpeed", 8f);
            material.SetFloat("_FlickerAmount", 0.06f);
            material.SetFloat("_PulseSpeed", 3f);
            material.SetFloat("_PulseAmount", 0.08f);
            material.SetFloat("_DepthFadeDistance", depthFade);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateProjectilePrefab(ArcaneMissilesVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ArcaneMissileProjectileVFX");
            try
            {
                MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
                poolable.ConfigureAuthoring(12);
                ArcaneMissileProjectileVFX controller = root.AddComponent<ArcaneMissileProjectileVFX>();
                Transform visual = CreateSection("Layered Projectile", root.transform);
                MeshRenderer core = CreateSphere("White-hot Leading Core", visual, materials["ProjectileCore"], Vector3.zero, new Vector3(0.7f, 0.7f, 1f), 20);
                MeshRenderer body = CreateSphere("Thick Blue Energy Body", visual, materials["ProjectileBody"], Vector3.zero, new Vector3(0.95f, 0.95f, 1.3f), 19);
                MeshRenderer flares = CreateQuad("Purple Outer Flares", visual, materials["ProjectileFlares"], Vector3.zero, new Vector3(1.45f, 1.45f, 1f), Quaternion.identity, 18);
                MeshRenderer shell = CreateSphere("Broken Pale-blue Shell", visual, materials["Shell"], Vector3.zero, new Vector3(1.15f, 1.15f, 1.45f), 17);
                Transform runeRoot = CreateSection("Rotating Internal Rune", visual);
                MeshRenderer rune = CreateQuad("Visible Rune", runeRoot, materials["Rune"], new Vector3(0f, 0f, -0.03f), Vector3.one * 0.82f, Quaternion.identity, 22);
                TrailRenderer coreTrail = CreateTrail("White Core Trail", root.transform, materials["TrailCore"], 24);
                TrailRenderer blueTrail = CreateTrail("Blue Energy Ribbon", root.transform, materials["TrailBlue"], 23);
                TrailRenderer purpleA = CreateTrail("Purple Spiral Ribbon A", root.transform, materials["TrailPurple"], 22);
                TrailRenderer purpleB = CreateTrail("Purple Spiral Ribbon B", root.transform, materials["TrailPurple"], 21);
                ParticleSystem fragments = CreateParticles("World Rune Fragments", root.transform, materials["Fragments"], true, false, 64, 20);
                ParticleSystem vapor = CreateParticles("World Arcane Vapor", root.transform, materials["Vapor"], true, false, 48, 18);
                ParticleSystem motes = CreateParticles("World Arcane Motes", root.transform, materials["Sparks"], true, false, 64, 21);
                controller.ConfigureAuthoring(visual, core, body, flares, shell, runeRoot, rune, coreTrail, blueTrail, purpleA, purpleB, fragments, vapor, motes);
                return SavePrefab(root, ProjectilePrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreateImpactPrefab(ArcaneMissilesVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ArcaneMissilesImpactVFX");
            try
            {
                MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
                poolable.ConfigureAuthoring(14);
                ArcaneMissilesImpactVFX controller = root.AddComponent<ArcaneMissilesImpactVFX>();
                Transform visual = CreateSection("Layered Target Explosion", root.transform);
                MeshRenderer flash = CreateQuad("White-blue Contact Flash", visual, materials["ImpactFlash"], Vector3.zero, Vector3.one, Quaternion.identity, 32);
                MeshRenderer explosion = CreateSphere("Rounded Blue-purple Explosion", visual, materials["Explosion"], Vector3.zero, Vector3.one * 0.8f, 29);
                Transform ringA = CreateSection("Broken Runic Shock Ring", visual);
                MeshRenderer ringRendererA = CreateQuad("Shock Ring A", ringA, materials["ShockRing"], Vector3.zero, Vector3.one, Quaternion.identity, 30);
                Transform ringB = CreateSection("Final Secondary Shock Ring", visual);
                MeshRenderer ringRendererB = CreateQuad("Shock Ring B", ringB, materials["Ring"], Vector3.zero, Vector3.one, Quaternion.identity, 28);
                Transform wrap = CreateSection("Target Arcane Wrap", visual);
                MeshRenderer wrapRenderer = CreateSphere("Short Energy Wrap", wrap, materials["Distortion"], Vector3.zero, Vector3.one, 24);
                ParticleSystem spikes = CreateParticles("Broad Energy Spikes", root.transform, materials["Spikes"], true, true, 32, 31);
                ParticleSystem fragments = CreateParticles("Shattered Internal Runes", root.transform, materials["Fragments"], true, true, 64, 27);
                ParticleSystem sparks = CreateParticles("Impact Sparks", root.transform, materials["Sparks"], true, true, 96, 33);
                controller.ConfigureAuthoring(visual, flash, explosion, ringA, ringRendererA, ringB, ringRendererB, wrap, wrapRenderer, spikes, fragments, sparks);
                return SavePrefab(root, ImpactPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreateInterruptPrefab(ArcaneMissilesVFXProfile profile, IReadOnlyDictionary<string, Material> materials)
        {
            GameObject root = new("ArcaneMissilesInterruptVFX");
            try
            {
                MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
                poolable.ConfigureAuthoring(6);
                ArcaneMissilesInterruptVFX controller = root.AddComponent<ArcaneMissilesInterruptVFX>();
                Transform core = CreateSection("Collapsing Hand Core", root.transform);
                MeshRenderer coreRenderer = CreateSphere("Imploding White-blue Core", core, materials["WhiteCore"], Vector3.zero, Vector3.one, 34);
                Transform ring = CreateSection("Connection Snap Ring", root.transform);
                MeshRenderer ringRenderer = CreateQuad("Broken Recoil Ring", ring, materials["ShockRing"], Vector3.zero, Vector3.one, Quaternion.identity, 32);
                ParticleSystem fragments = CreateParticles("Dim Rune Fragments", root.transform, materials["Fragments"], true, true, 96, 30);
                ParticleSystem sparks = CreateParticles("Blue-purple Collapse Sparks", root.transform, materials["Sparks"], true, true, 96, 31);
                ParticleSystem snaps = CreateParticles("Snapped Energy Connections", root.transform, materials["Connection"], true, true, 48, 33);
                controller.ConfigureAuthoring(core, coreRenderer, ring, ringRenderer, fragments, sparks, snaps);
                return SavePrefab(root, InterruptPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject CreateCastingPrefab(ArcaneMissilesVFXProfile profile, IReadOnlyDictionary<string, Material> materials, GameObject projectilePrefab, GameObject impactPrefab, GameObject interruptPrefab)
        {
            GameObject root = new("ArcaneMissilesVFX");
            try
            {
                MMOAbilityVfxPoolable poolable = root.AddComponent<MMOAbilityVfxPoolable>();
                poolable.ConfigureAuthoring(8);
                ArcaneMissilesVFX controller = root.AddComponent<ArcaneMissilesVFX>();
                Transform caster = CreateSection("Caster Channel Effect", root.transform);
                MeshRenderer leftHand = CreateQuad("Left Hand White-blue Glow", caster, materials["HandGlow"], Vector3.zero, Vector3.one * 0.36f, Quaternion.identity, 20);
                MeshRenderer rightHand = CreateQuad("Right Hand White-blue Glow", caster, materials["HandGlow"], Vector3.zero, Vector3.one * 0.36f, Quaternion.identity, 20);
                MeshRenderer core = CreateSphere("Central Arcane Core", caster, materials["WhiteCore"], Vector3.zero, Vector3.one * 0.28f, 22);
                MeshRenderer circle = CreateQuad("Restrained Broken Runic Circle", caster, materials["ChannelCircle"], Vector3.zero, Vector3.one, Quaternion.Euler(90f, 0f, 0f), 8);
                ParticleSystem sparks = CreateParticles("World Purple Sparks", caster, materials["Sparks"], true, true, 96, 21);
                ParticleSystem runeFragments = CreateParticles("World Rune Fragments", caster, materials["Fragments"], true, true, 72, 19);
                LineRenderer handRibbon = CreateLine("Hand Energy Ribbon", caster, materials["Connection"], 18);

                Transform formation = CreateSection("Three Arcane Fabricators", root.transform);
                ArcaneMissilesFabricatorVFX[] fabricators = new ArcaneMissilesFabricatorVFX[3];
                LineRenderer[] connections = new LineRenderer[3];
                string[] names = { "Upper Left Fabricator", "Upper Right Fabricator", "Higher Rear Fabricator" };
                for (int i = 0; i < 3; i++)
                {
                    GameObject orbObject = new(names[i]);
                    orbObject.transform.SetParent(formation, false);
                    ArcaneMissilesFabricatorVFX fabricator = orbObject.AddComponent<ArcaneMissilesFabricatorVFX>();
                    Transform visual = CreateSection("Layered Fabrication Orb", orbObject.transform);
                    MeshRenderer orbCore = CreateSphere("White Internal Core", visual, materials["WhiteCore"], Vector3.zero, Vector3.one * 0.62f, 20);
                    MeshRenderer blue = CreateSphere("Blue Main Energy Body", visual, materials["BlueOrb"], Vector3.zero, Vector3.one, 18);
                    MeshRenderer purple = CreateSphere("Purple Secondary Energy", visual, materials["PurpleOrb"], Vector3.zero, Vector3.one * 1.08f, 17);
                    MeshRenderer shell = CreateSphere("Pale Broken Shell", visual, materials["Shell"], Vector3.zero, Vector3.one * 1.22f, 16);
                    Transform runeRoot = CreateSection("Visible Rotating Rune", visual);
                    MeshRenderer rune = CreateQuad("Original Internal Rune", runeRoot, materials["Rune"], new Vector3(0f, 0f, -0.04f), Vector3.one * profile.RuneScale, Quaternion.identity, 24);
                    Transform firstRing = CreateSection("Broken Runic Ring A", visual);
                    MeshRenderer firstRingRenderer = CreateQuad("Runic Ring A", firstRing, materials["Ring"], Vector3.zero, Vector3.one * 1.4f, Quaternion.Euler(12f, 0f, 0f), 21);
                    Transform secondRing = CreateSection("Broken Runic Ring B", visual);
                    MeshRenderer secondRingRenderer = CreateQuad("Runic Ring B", secondRing, materials["Ring"], Vector3.zero, Vector3.one * 1.66f, Quaternion.Euler(-15f, 8f, 0f), 19);
                    ParticleSystem fragments = CreateParticles("Converging Geometric Fragments", orbObject.transform, materials["Fragments"], true, true, 64, 23);
                    ParticleSystem orbSparks = CreateParticles("Fabrication Sparks", orbObject.transform, materials["Sparks"], true, true, 64, 25);
                    ParticleSystem recoil = CreateParticles("Launch Recoil Ring", orbObject.transform, materials["ShockRing"], true, true, 32, 26);
                    fabricator.ConfigureAuthoring(visual, orbCore, blue, purple, shell, runeRoot, rune, firstRing, firstRingRenderer, secondRing, secondRingRenderer, fragments, orbSparks, recoil);
                    fabricators[i] = fabricator;
                    connections[i] = CreateLine($"Core-to-{names[i]} Energy Stream", root.transform, materials["Connection"], 15 + i);
                }

                controller.ConfigureAuthoring(profile, projectilePrefab, impactPrefab, interruptPrefab, leftHand.transform, leftHand, rightHand.transform, rightHand, core.transform, core, circle.transform, circle, sparks, runeFragments, handRibbon, connections, fabricators);
                return SavePrefab(root, CastingPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void WireAbility(GameObject castingPrefab)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<MMOAbilityVfxDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.Configure(
                castingPrefab,
                null,
                null,
                true,
                false,
                true,
                false,
                Vector3.zero,
                Vector3.zero,
                new Vector3(0f, 1.25f, 0.25f),
                new Vector3(0f, 1.05f, 0f),
                0f,
                false);
            EditorUtility.SetDirty(definition);
            MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (ability == null) throw new MissingReferenceException(AbilityPath);
            ability.SetVisualEffects(definition);
            EditorUtility.SetDirty(ability);
        }

        private static MeshRenderer CreateSphere(string name, Transform parent, Material material, Vector3 position, Vector3 scale, int sortingOrder)
        {
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = scale;
            Object.DestroyImmediate(child.GetComponent<Collider>());
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static MeshRenderer CreateQuad(string name, Transform parent, Material material, Vector3 position, Vector3 scale, Quaternion rotation, int sortingOrder)
        {
            GameObject child = GameObject.CreatePrimitive(PrimitiveType.Quad);
            child.name = name;
            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localRotation = rotation;
            child.transform.localScale = scale;
            Object.DestroyImmediate(child.GetComponent<Collider>());
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static TrailRenderer CreateTrail(string name, Transform parent, Material material, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            TrailRenderer trail = child.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.time = 0.45f;
            trail.minVertexDistance = 0.025f;
            trail.textureMode = LineTextureMode.Tile;
            trail.alignment = LineAlignment.View;
            trail.numCapVertices = 3;
            trail.numCornerVertices = 3;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.sortingOrder = sortingOrder;
            trail.emitting = false;
            return trail;
        }

        private static LineRenderer CreateLine(string name, Transform parent, Material material, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = sortingOrder;
            line.enabled = false;
            return line;
        }

        private static ParticleSystem CreateParticles(string name, Transform parent, Material material, bool worldSpace, bool burst, int maxParticles, int sortingOrder)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            ParticleSystem particles = child.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = !burst;
            main.duration = burst ? 0.55f : 1.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.72f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.1f);
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.maxParticles = maxParticles;
            main.stopAction = ParticleSystemStopAction.None;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = burst ? 0f : 12f;
            if (burst) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;
            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) },
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }
            });
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            return particles;
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null) throw new UnityException($"Failed to save Arcane Missiles prefab: {path}");
            return saved;
        }

        private static Texture2D LoadTexture(string name)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{name}");
            if (texture == null) throw new FileNotFoundException(name);
            return texture;
        }

        private static void EnsureFolders()
        {
            foreach (string path in new[] { RootFolder, TextureFolder, SourceFolder, ShaderFolder, MaterialFolder, ProfileFolder, PrefabFolder, RootFolder + "/Documentation" })
            {
                EnsureFolder(path);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string child = Path.GetFileName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
#endif
