using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Vfx;
using RPGClone.Vfx.Fire;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTools
{
    public static class FlamestrikeVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/Flamestrike";
        private const string TextureFolder = Root + "/Textures";
        private const string ShaderFolder = Root + "/Shaders";
        private const string MaterialFolder = Root + "/Materials";
        private const string MeshFolder = Root + "/Meshes";
        private const string ProfileFolder = Root + "/Profiles";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string ProfilePath = ProfileFolder + "/FlamestrikeVFX_Default.asset";
        private const string TargetingPath = PrefabFolder + "/FlamestrikeTargetingVFX.prefab";
        private const string CastingPath = PrefabFolder + "/FlamestrikeCastVFX.prefab";
        private const string HitPath = PrefabFolder + "/FlamestrikeVFX.prefab";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Mage_Flamestrike_VFX.asset";
        private const string AbilityPath = "Assets/_Project/Configs/Abilities/Mage_Flamestrike.asset";
        private const string AtlasPath = TextureFolder + "/Flamestrike_FlameAtlas_Polished.png";
        private const string GroundPath = TextureFolder + "/Flamestrike_GroundScorch_Polished.png";
        private const string UtilityAtlasPath = TextureFolder + "/Flamestrike_UtilityAtlas.png";
        private const string TubeFlowMaskPath = TextureFolder + "/Flamestrike_TubeFlowMask.png";
        private const string NoisePath = "Assets/_Project/VFX/Fireball/Textures/Fireball_Noise.png";
        private const string HeavyDustPath = "Assets/_Project/VFX/Charge/Materials/Charge_HeavyDust.mat";
        private const string FineDustPath = "Assets/_Project/VFX/Charge/Materials/Charge_FineDust.mat";

        [MenuItem("Tools/RPG Clone/VFX/Build Flamestrike VFX")]
        public static void Build()
        {
            EnsureFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(AtlasPath, TextureWrapMode.Clamp, 2048, true);
            ConfigureTexture(GroundPath, TextureWrapMode.Clamp, 2048, true);
            ConfigureTexture(UtilityAtlasPath, TextureWrapMode.Clamp, 2048, true);
            ConfigureTexture(TubeFlowMaskPath, TextureWrapMode.Mirror, 2048, false);
            Dictionary<string, Mesh> meshes = CreateMeshes();
            FlamestrikeVFXProfile profile = LoadOrCreateProfile();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject targeting = CreateTargetingPrefab(profile, materials, meshes);
            GameObject casting = CreateCastingPrefab(profile, materials, meshes);
            GameObject hit = CreateHitPrefab(profile, materials, meshes);
            WireAbility(targeting, casting, hit);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = hit;
            Debug.Log($"Built and wired the polished mesh-based Flamestrike VFX package at '{Root}'.", hit);
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Flamestrike VFX")]
        public static void ValidateBuild()
        {
            GameObject targeting = AssetDatabase.LoadAssetAtPath<GameObject>(TargetingPath);
            GameObject casting = AssetDatabase.LoadAssetAtPath<GameObject>(CastingPath);
            GameObject hit = AssetDatabase.LoadAssetAtPath<GameObject>(HitPath);
            if (targeting == null || targeting.GetComponent<FlamestrikeTargetingVFX>() == null) throw new MissingReferenceException("Flamestrike targeting prefab is invalid.");
            if (casting == null || casting.GetComponent<FlamestrikeCastVFX>() == null) throw new MissingReferenceException("Flamestrike cast prefab is invalid.");
            if (hit == null || hit.GetComponent<FlamestrikeVFX>() == null) throw new MissingReferenceException("Flamestrike hit prefab is invalid.");
            foreach (string phase in new[] { "Initial Impact", "Persistent Burning Ground", "Damage Pulse", "Target Reactions" })
                if (hit.transform.Find(phase) == null) throw new MissingReferenceException($"Flamestrike is missing phase '{phase}'.");
            if (hit.GetComponentsInChildren<Light>(true).Length != 0 || hit.GetComponentsInChildren<Animator>(true).Length != 0)
                throw new UnityException("Flamestrike must remain procedural and light-free.");
            if (hit.GetComponentsInChildren<ParticleSystem>(true).Length > 32)
                throw new UnityException("Flamestrike exceeds its authored particle-system budget.");
            if (AssetDatabase.FindAssets("t:Material", new[] { MaterialFolder }).Length != 17)
                throw new UnityException("Flamestrike must provide exactly seventeen shared materials.");
            foreach (string meshName in new[] { "Flamestrike_Disc", "Flamestrike_Torus", "Flamestrike_ConeShell", "Flamestrike_TubeShell" })
                if (AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/{meshName}.asset") == null) throw new MissingReferenceException($"Missing procedural mesh: {meshName}.");
            if (targeting.GetComponentsInChildren<MeshFilter>(true).Length < 3 || Array.Exists(targeting.GetComponentsInChildren<MeshFilter>(true), f => f.sharedMesh != null && f.sharedMesh.name == "Quad"))
                throw new UnityException("The targeting preview must use circular meshes, not square cards.");
            Transform mainColumn = hit.transform.Find("Initial Impact/Main Vertical Fire Column");
            if (mainColumn == null || mainColumn.GetComponentsInChildren<FlamestrikeTubeShellVFX>(true).Length != 4)
                throw new UnityException("The dominant fire pillar must use exactly four large centered tube shells.");
            Transform lingeringVortex = hit.transform.Find("Persistent Burning Ground/Lingering Fire Vortex");
            if (lingeringVortex == null || lingeringVortex.GetComponentsInChildren<FlamestrikeTubeShellVFX>(true).Length != 4)
                throw new UnityException("The lingering vortex must use exactly four large centered tube shells.");
            if (hit.GetComponentsInChildren<FlamestrikeExpandingRingVFX>(true).Length != 3)
                throw new UnityException("Flamestrike must provide three rotating, expanding, evaporating vortex rings.");
            foreach (FlamestrikeExpandingRingVFX ring in hit.GetComponentsInChildren<FlamestrikeExpandingRingVFX>(true))
                if (Mathf.Abs(ring.EndDiameter - 10f) > 0.01f || ring.HeightAtPerimeter >= 0.15f)
                    throw new UnityException("Every vortex ring must reach the ground perimeter while flattening vertically.");
            if (Array.Exists(hit.GetComponentsInChildren<MeshFilter>(true), f => f.sharedMesh != null && f.sharedMesh.name == "Quad"))
                throw new UnityException("The Flamestrike hit prefab must not contain atlas cards.");
            ValidateChargeDust(hit, "Initial Impact/Smoke Crown", "Charge_HeavyDust");
            ParticleSystem smokeCrown = hit.transform.Find("Initial Impact/Smoke Crown").GetComponent<ParticleSystem>();
            if (!smokeCrown.main.loop || smokeCrown.emission.rateOverTime.constant <= 0f)
                throw new UnityException("The smoke crown must emit continuously for the burning-field lifetime.");
            ValidateChargeDust(hit, "Persistent Burning Ground/Rising Smoke", "Charge_HeavyDust");
            ValidateChargeDust(hit, "Persistent Burning Ground/Ash Haze", "Charge_FineDust");
            ValidateShader(ShaderFolder + "/FlamestrikeLayeredUnlit.shader");
            ValidateShader(ShaderFolder + "/FlamestrikeTargetingRadial.shader");
            ValidateShader(ShaderFolder + "/FlamestrikeProceduralTube.shader");
            ValidateWiring(targeting, casting, hit);
            Debug.Log("Flamestrike VFX validation passed: polished textures, circular/volumetric meshes, Charge dust reuse, lifecycle, and network-facing wiring are valid.", hit);
        }

        private static void ValidateChargeDust(GameObject root, string path, string materialName)
        {
            Transform child = root.transform.Find(path);
            ParticleSystemRenderer renderer = child != null ? child.GetComponent<ParticleSystemRenderer>() : null;
            if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.name != materialName)
                throw new MissingReferenceException($"'{path}' must reuse {materialName}.");
        }

        private static void ValidateShader(string path)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null || !shader.isSupported) throw new MissingReferenceException($"Flamestrike shader is missing or unsupported: {path}");
            foreach (ShaderMessage message in ShaderUtil.GetShaderMessages(shader))
                if (message.severity == ShaderCompilerMessageSeverity.Error) throw new UnityException(message.message);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project/VFX", "Flamestrike");
            EnsureFolder(Root, "Textures"); EnsureFolder(Root, "Shaders"); EnsureFolder(Root, "Materials");
            EnsureFolder(Root, "Meshes"); EnsureFolder(Root, "Profiles"); EnsureFolder(Root, "Prefabs");
            EnsureFolder(TextureFolder, "Sources");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void ConfigureTexture(string path, TextureWrapMode wrap, int maxSize, bool srgb)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new MissingReferenceException($"Required Flamestrike texture is missing: {path}");
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = false;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static FlamestrikeVFXProfile LoadOrCreateProfile()
        {
            FlamestrikeVFXProfile profile = AssetDatabase.LoadAssetAtPath<FlamestrikeVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FlamestrikeVFXProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            SerializedObject serialized = new(profile);
            serialized.FindProperty("groundTintOpacity").floatValue = 0.34f;
            serialized.FindProperty("targetingEmberRate").intValue = 0;
            serialized.FindProperty("validTargetColor").colorValue = new Color(0.08f, 0.46f, 1.35f, 1f);
            serialized.FindProperty("invalidTargetColor").colorValue = new Color(1.15f, 0.035f, 0.02f, 1f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader fireShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/FlamestrikeLayeredUnlit.shader");
            Shader targetingShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/FlamestrikeTargetingRadial.shader");
            Shader tubeShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "/FlamestrikeProceduralTube.shader");
            if (fireShader == null || targetingShader == null || tubeShader == null) throw new MissingReferenceException("Flamestrike shaders could not be loaded.");
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            Texture2D ground = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundPath);
            Texture2D utility = AssetDatabase.LoadAssetAtPath<Texture2D>(UtilityAtlasPath);
            Texture2D noise = AssetDatabase.LoadAssetAtPath<Texture2D>(NoisePath);
            Texture2D tubeFlowMask = AssetDatabase.LoadAssetAtPath<Texture2D>(TubeFlowMaskPath);
            Dictionary<string, Material> result = new();
            AddTube("WhiteHotCore", new Color(1.4f, 0.48f, 0.045f, 0.95f), new Color(2.8f, 2.15f, 0.92f, 1f), 2.8f, 1.7f, 0.34f, 0.66f, true, 0.35f);
            AddTube("MainGoldenFlame", new Color(1.35f, 0.16f, 0.006f, 0.9f), new Color(2.55f, 1.28f, 0.2f, 1f), 2.1f, 1.45f, 0.4f, 0.58f, true, 0.72f);
            AddTube("DarkOuterFlame", new Color(0.72f, 0.018f, 0.002f, 0.58f), new Color(1.65f, 0.2f, 0.012f, 0.9f), 1.35f, 1.1f, 0.48f, 0.46f, false, 1.2f);
            AddTube("InitialFireColumn", new Color(1.55f, 0.12f, 0.002f, 0.88f), new Color(2.8f, 1.5f, 0.28f, 1f), 2.35f, 1.55f, 0.37f, 0.62f, true, 0.88f);
            AddTube("GroundFlamePatches", new Color(1.2f, 0.08f, 0.002f, 0.82f), new Color(2.25f, 0.86f, 0.1f, 1f), 1.75f, 1.25f, 0.44f, 0.5f, true, 0.82f);
            AddFire("FireShockwave", atlas, Cell2(1, 1), new Color(1.4f, 0.16f, 0.004f, 0.78f), new Color(2.25f, 1.02f, 0.2f, 1f), true, 0.55f);
            AddTube("RadialFlameBlades", new Color(1.05f, 0.05f, 0.002f, 0.72f), new Color(2.1f, 0.7f, 0.07f, 1f), 1.9f, 1.4f, 0.46f, 0.5f, true, 0.9f);
            AddFire("GlowingGroundCracks", ground, new Vector4(1f, 1f, 0f, 0f), new Color(0.8f, 0.025f, 0.001f, 0.82f), new Color(2.4f, 1.08f, 0.16f, 1f), true, 0f);
            AddFire("ScorchedTerrain", ground, new Vector4(1f, 1f, 0f, 0f), new Color(0.34f, 0.045f, 0.012f, 0.7f), new Color(0.95f, 0.12f, 0.015f, 1f), false, 0f);
            AddFire("Embers", utility, Cell4(0, 2), new Color(1.2f, 0.2f, 0.008f, 0.95f), new Color(2f, 1.2f, 0.32f, 1f), true, 0f);
            AddFire("Sparks", utility, Cell4(1, 2), new Color(1.4f, 0.42f, 0.015f, 1f), new Color(2.2f, 1.65f, 0.62f, 1f), true, 0f);
            AddFire("CharredDebris", utility, Cell4(2, 2), new Color(0.24f, 0.025f, 0.008f, 0.88f), new Color(1.05f, 0.15f, 0.02f, 1f), false, 0f);
            AddFire("Smoke", utility, Cell4(3, 2), new Color(0.12f, 0.04f, 0.025f, 0.52f), new Color(0.42f, 0.09f, 0.025f, 0.65f), false, 0f);
            AddFire("Ash", utility, Cell4(1, 3), new Color(0.18f, 0.11f, 0.09f, 0.3f), new Color(0.48f, 0.16f, 0.06f, 0.42f), false, 0f);
            AddFire("MagicalFireEnergy", atlas, Cell2(1, 1), new Color(1.05f, 0.045f, 0.004f, 0.68f), new Color(2.1f, 0.74f, 0.1f, 1f), true, 0.5f);
            AddFire("HeatDistortion", utility, Cell4(3, 3), new Color(0.18f, 0.04f, 0.01f, 0.1f), new Color(0.5f, 0.12f, 0.02f, 0.16f), false, 0.9f);

            string targetingPath = $"{MaterialFolder}/Flamestrike_TargetingIndicator.mat";
            Material targeting = AssetDatabase.LoadAssetAtPath<Material>(targetingPath);
            if (targeting == null) { targeting = new Material(targetingShader) { name = "Flamestrike_TargetingIndicator" }; AssetDatabase.CreateAsset(targeting, targetingPath); }
            targeting.shader = targetingShader;
            targeting.SetColor("_Tint", new Color(0.08f, 0.46f, 1.35f, 1f));
            targeting.SetFloat("_FillAlpha", 0.34f); targeting.SetFloat("_EdgeAlpha", 1f); targeting.SetFloat("_EdgeWidth", 0.075f);
            targeting.renderQueue = (int)RenderQueue.Transparent; EditorUtility.SetDirty(targeting); result["TargetingIndicator"] = targeting;
            return result;

            void AddTube(string name, Color tint, Color hot, float speed, float scale, float cutoff, float topFade, bool additive, float fresnel)
            {
                string path = $"{MaterialFolder}/Flamestrike_{name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(tubeShader) { name = "Flamestrike_" + name }; AssetDatabase.CreateAsset(material, path); }
                material.shader = tubeShader; material.SetTexture("_NoiseMap", tubeFlowMask); material.SetColor("_Tint", tint); material.SetColor("_HotTint", hot);
                material.SetFloat("_FlowSpeed", speed); material.SetFloat("_FlowScale", scale); material.SetFloat("_Cutoff", cutoff); material.SetFloat("_EdgeSoftness", 0.13f);
                material.SetFloat("_TopFadeStart", topFade); material.SetFloat("_BaseFadeEnd", 0.055f); material.SetFloat("_FresnelStrength", fresnel);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                material.renderQueue = (int)RenderQueue.Transparent; EditorUtility.SetDirty(material); result[name] = material;
            }

            void AddFire(string name, Texture texture, Vector4 rect, Color tint, Color hot, bool additive, float fresnel)
            {
                string path = $"{MaterialFolder}/Flamestrike_{name}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(fireShader) { name = "Flamestrike_" + name }; AssetDatabase.CreateAsset(material, path); }
                material.shader = fireShader; material.SetTexture("_BaseMap", texture); material.SetTexture("_NoiseMap", noise);
                material.SetVector("_AtlasRect", rect); material.SetColor("_Tint", tint); material.SetColor("_HotTint", hot);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_DistortionStrength", name == "HeatDistortion" ? 0.08f : 0.018f); material.SetFloat("_FresnelStrength", fresnel);
                material.renderQueue = (int)RenderQueue.Transparent; EditorUtility.SetDirty(material); result[name] = material;
            }
        }

        private static Vector4 Cell2(int x, int visualRow) => new(0.5f, 0.5f, x * 0.5f, (1 - visualRow) * 0.5f);
        private static Vector4 Cell4(int x, int visualRow) => new(0.25f, 0.25f, x * 0.25f, (3 - visualRow) * 0.25f);

        private static Dictionary<string, Mesh> CreateMeshes()
        {
            return new Dictionary<string, Mesh>
            {
                ["Disc"] = CreateOrUpdateMesh("Flamestrike_Disc", CreateDiscMesh),
                ["Torus"] = CreateOrUpdateMesh("Flamestrike_Torus", CreateTorusMesh),
                ["Cone"] = CreateOrUpdateMesh("Flamestrike_ConeShell", CreateConeMesh),
                ["Tube"] = CreateOrUpdateMesh("Flamestrike_TubeShell", CreateTubeMesh)
            };
        }

        private static Mesh CreateOrUpdateMesh(string name, Func<Mesh> factory)
        {
            string path = $"{MeshFolder}/{name}.asset";
            Mesh generated = factory(); generated.name = name;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
            EditorUtility.CopySerialized(generated, existing); existing.name = name; EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(generated); return existing;
        }

        private static Mesh CreateDiscMesh()
        {
            const int segments = 64; Vector3[] vertices = new Vector3[segments + 2]; Vector2[] uv = new Vector2[vertices.Length]; Vector3[] normals = new Vector3[vertices.Length]; int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero; uv[0] = Vector2.one * 0.5f; normals[0] = Vector3.up;
            for (int i = 0; i <= segments; i++) { float a = i * Mathf.PI * 2f / segments; Vector3 p = new(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f); vertices[i + 1] = p; uv[i + 1] = new Vector2(p.x + 0.5f, p.z + 0.5f); normals[i + 1] = Vector3.up; }
            for (int i = 0; i < segments; i++) { int t = i * 3; triangles[t] = 0; triangles[t + 1] = i + 1; triangles[t + 2] = i + 2; }
            Mesh mesh = new(); mesh.vertices = vertices; mesh.uv = uv; mesh.normals = normals; mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh CreateTorusMesh()
        {
            const int radialSegments = 64, tubeSegments = 10; const float major = 0.455f, minor = 0.045f;
            int row = tubeSegments + 1; Vector3[] vertices = new Vector3[(radialSegments + 1) * row]; Vector3[] normals = new Vector3[vertices.Length]; Vector2[] uv = new Vector2[vertices.Length]; int[] triangles = new int[radialSegments * tubeSegments * 6];
            for (int r = 0; r <= radialSegments; r++) for (int t = 0; t <= tubeSegments; t++) { float u = r / (float)radialSegments; float v = t / (float)tubeSegments; float a = u * Mathf.PI * 2f; float b = v * Mathf.PI * 2f; Vector3 n = new(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b)); int i = r * row + t; vertices[i] = new Vector3(Mathf.Cos(a) * major, 0f, Mathf.Sin(a) * major) + n * minor; normals[i] = n; uv[i] = new Vector2(u, v); }
            int index = 0; for (int r = 0; r < radialSegments; r++) for (int t = 0; t < tubeSegments; t++) { int a = r * row + t, b = (r + 1) * row + t; triangles[index++] = a; triangles[index++] = b; triangles[index++] = a + 1; triangles[index++] = a + 1; triangles[index++] = b; triangles[index++] = b + 1; }
            Mesh mesh = new(); mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh CreateConeMesh()
        {
            const int radialSegments = 40, heightSegments = 6; int row = radialSegments + 1;
            Vector3[] vertices = new Vector3[(heightSegments + 1) * row]; Vector3[] normals = new Vector3[vertices.Length]; Vector2[] uv = new Vector2[vertices.Length]; int[] triangles = new int[heightSegments * radialSegments * 6];
            for (int y = 0; y <= heightSegments; y++) for (int r = 0; r <= radialSegments; r++) { float v = y / (float)heightSegments; float u = r / (float)radialSegments; float a = u * Mathf.PI * 2f; float radius = Mathf.Lerp(0.5f, 0.055f, Mathf.Pow(v, 0.82f)); int i = y * row + r; vertices[i] = new Vector3(Mathf.Cos(a) * radius, v, Mathf.Sin(a) * radius); normals[i] = new Vector3(Mathf.Cos(a), 0.445f, Mathf.Sin(a)).normalized; uv[i] = new Vector2(u, v); }
            int index = 0; for (int y = 0; y < heightSegments; y++) for (int r = 0; r < radialSegments; r++) { int a = y * row + r, b = (y + 1) * row + r; triangles[index++] = a; triangles[index++] = b; triangles[index++] = a + 1; triangles[index++] = a + 1; triangles[index++] = b; triangles[index++] = b + 1; }
            Mesh mesh = new(); mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
        }

        private static Mesh CreateTubeMesh()
        {
            const int radialSegments = 48, heightSegments = 12; int row = radialSegments + 1;
            Vector3[] vertices = new Vector3[(heightSegments + 1) * row]; Vector3[] normals = new Vector3[vertices.Length]; Vector2[] uv = new Vector2[vertices.Length]; int[] triangles = new int[heightSegments * radialSegments * 6];
            for (int y = 0; y <= heightSegments; y++) for (int r = 0; r <= radialSegments; r++)
            {
                float v = y / (float)heightSegments, u = r / (float)radialSegments, angle = u * Mathf.PI * 2f;
                float radius = Mathf.Lerp(0.4f, 0.52f, v) + Mathf.Sin(v * Mathf.PI) * 0.035f;
                int index = y * row + r; vertices[index] = new Vector3(Mathf.Cos(angle) * radius, v, Mathf.Sin(angle) * radius);
                normals[index] = new Vector3(Mathf.Cos(angle), -0.12f, Mathf.Sin(angle)).normalized; uv[index] = new Vector2(u, v);
            }
            int triangle = 0; for (int y = 0; y < heightSegments; y++) for (int r = 0; r < radialSegments; r++)
            {
                int a = y * row + r, b = (y + 1) * row + r;
                triangles[triangle++] = a; triangles[triangle++] = b; triangles[triangle++] = a + 1;
                triangles[triangle++] = a + 1; triangles[triangle++] = b; triangles[triangle++] = b + 1;
            }
            Mesh mesh = new(); mesh.vertices = vertices; mesh.normals = normals; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds(); return mesh;
        }

        private static GameObject CreateTargetingPrefab(FlamestrikeVFXProfile profile, IReadOnlyDictionary<string, Material> m, IReadOnlyDictionary<string, Mesh> meshes)
        {
            GameObject root = new("FlamestrikeTargetingVFX");
            MeshRenderer tint = CreateMeshRenderer("Blue Radial Fill", root.transform, meshes["Disc"], m["TargetingIndicator"], new Vector3(0, 0.005f, 0), new Vector3(10, 1, 10), Quaternion.identity, 1);
            MeshRenderer boundary = CreateMeshRenderer("Blue Circular Boundary", root.transform, meshes["Torus"], m["TargetingIndicator"], new Vector3(0, 0.018f, 0), new Vector3(10, 1, 10), Quaternion.identity, 2);
            Transform marker = CreateMeshRenderer("Center Ring", root.transform, meshes["Torus"], m["TargetingIndicator"], new Vector3(0, 0.025f, 0), Vector3.one, Quaternion.identity, 3).transform;
            FlamestrikeTargetingVFX controller = root.AddComponent<FlamestrikeTargetingVFX>();
            controller.ConfigureAuthoring(profile, tint, boundary, null, Array.Empty<Renderer>(), marker, null);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TargetingPath); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        private static GameObject CreateCastingPrefab(FlamestrikeVFXProfile profile, IReadOnlyDictionary<string, Material> m, IReadOnlyDictionary<string, Mesh> meshes)
        {
            GameObject root = new("FlamestrikeCastVFX"); Transform hands = CreateSection("Hand Ignition", root.transform); List<Renderer> flames = new();
            Transform left = CreateStaticTubeCluster("Left Hand Flame", hands, meshes["Tube"], m["MainGoldenFlame"], m["WhiteHotCore"], Vector3.zero, 12, flames);
            Transform right = CreateStaticTubeCluster("Right Hand Flame", hands, meshes["Tube"], m["MainGoldenFlame"], m["WhiteHotCore"], Vector3.zero, 12, flames);
            Transform core = CreateSection("Compressed Fire Core", root.transform);
            flames.Add(CreateMeshRenderer("Outer Core", core, Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), m["DarkOuterFlame"], Vector3.zero, Vector3.one * 1.25f, Quaternion.identity, 10));
            flames.Add(CreateMeshRenderer("Golden Core", core, Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), m["MainGoldenFlame"], Vector3.zero, Vector3.one, Quaternion.identity, 11));
            flames.Add(CreateMeshRenderer("White Hot Center", core, Resources.GetBuiltinResource<Mesh>("Sphere.fbx"), m["WhiteHotCore"], Vector3.zero, Vector3.one * 0.62f, Quaternion.identity, 12));
            Transform conduction = CreateSection("Fire Conduction", root.transform); LineRenderer[] conductionLines = new LineRenderer[3]; for (int i = 0; i < conductionLines.Length; i++) conductionLines[i] = CreateLine($"Hand Arc {i + 1}", conduction, m["MainGoldenFlame"], 12 + i);
            Transform ribbons = CreateSection("Caster Flame Ribbons", root.transform); LineRenderer[] orbitLines = new LineRenderer[3]; for (int i = 0; i < orbitLines.Length; i++) orbitLines[i] = CreateLine($"Orbit Ribbon {i + 1}", ribbons, m[i == 2 ? "MagicalFireEnergy" : "MainGoldenFlame"], 8 + i);
            Transform buildup = CreateSection("Target Area Buildup", root.transform); List<Renderer> buildupRenderers = new();
            buildupRenderers.Add(CreateMeshRenderer("Compressed Ground Glow", buildup, meshes["Disc"], m["GlowingGroundCracks"], Vector3.zero, Vector3.one, Quaternion.identity, 2));
            buildupRenderers.Add(CreateMeshRenderer("Growing Fire Ring", buildup, meshes["Torus"], m["FireShockwave"], new Vector3(0, 0.018f, 0), Vector3.one, Quaternion.identity, 3));
            ParticleSystem embers = CreateParticles("Cast Embers", root.transform, m["Embers"], true, 18, 0, 0.9f, 0.42f, 0.1f, 0.7f, false, 14);
            FlamestrikeCastVFX controller = root.AddComponent<FlamestrikeCastVFX>(); controller.ConfigureAuthoring(profile, left, right, core, flames.ToArray(), conductionLines, orbitLines, buildup, buildupRenderers.ToArray(), embers);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CastingPath); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        private static GameObject CreateHitPrefab(FlamestrikeVFXProfile profile, IReadOnlyDictionary<string, Material> m, IReadOnlyDictionary<string, Mesh> meshes)
        {
            Material heavyDust = LoadMaterial(HeavyDustPath); Material fineDust = LoadMaterial(FineDustPath); Mesh sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            GameObject root = new("FlamestrikeVFX"); Transform impact = CreateSection("Initial Impact", root.transform); List<Renderer> impactRenderers = new(); List<FlamestrikeTubeShellVFX> impactTubeShells = new(); List<Transform> pillars = new();
            MeshRenderer flash = CreateMeshRenderer("Central Impact Flash", impact, sphere, m["WhiteHotCore"], new Vector3(0, 0.45f, 0), Vector3.one, Quaternion.identity, 20);
            Transform mainColumn = CreateSection("Main Vertical Fire Column", impact);
            FlamestrikeTubeShellVFX outerImpactTube = AddTubeShell("Outer Red Expansion", mainColumn, meshes["Tube"], m["DarkOuterFlame"], new Vector3(1.42f, 1.08f, 1.42f), 16, 0.08f, 0.72f, 0.42f, 0.14f, 0.12f, 0.66f, false, new Color(1f, 0.42f, 0.34f, 0.78f)); outerImpactTube.SetAuthoringOffset(new Vector3(-0.08f, 0f, 0.05f)); impactTubeShells.Add(outerImpactTube);
            FlamestrikeTubeShellVFX amberImpactTube = AddTubeShell("Amber Expansion", mainColumn, meshes["Tube"], m["InitialFireColumn"], new Vector3(1.12f, 1.02f, 1.12f), 17, 0.31f, 0.82f, 0.34f, 0.11f, 0.1f, 0.82f, false, new Color(1f, 0.68f, 0.34f, 0.9f)); amberImpactTube.SetAuthoringOffset(new Vector3(0.07f, 0f, -0.04f)); impactTubeShells.Add(amberImpactTube);
            FlamestrikeTubeShellVFX goldenImpactTube = AddTubeShell("Golden Roaring Tube", mainColumn, meshes["Tube"], m["MainGoldenFlame"], new Vector3(0.76f, 0.96f, 0.76f), 18, 0.57f, 0.92f, 0.26f, 0.08f, 0.07f, 0.94f, false, new Color(1f, 0.9f, 0.55f, 1f)); goldenImpactTube.SetAuthoringOffset(new Vector3(-0.035f, 0f, -0.065f)); impactTubeShells.Add(goldenImpactTube);
            FlamestrikeTubeShellVFX coreImpactTube = AddTubeShell("White Core Shaft", mainColumn, meshes["Tube"], m["WhiteHotCore"], new Vector3(0.38f, 0.9f, 0.38f), 19, 0.79f, 1f, 0.16f, 0.05f, 0.04f, 1f, false, Color.white); coreImpactTube.SetAuthoringOffset(new Vector3(0.025f, 0f, 0.035f)); impactTubeShells.Add(coreImpactTube);
            Transform shockwave = CreateMeshRenderer("Circular Fire Shockwave", impact, meshes["Torus"], m["FireShockwave"], new Vector3(0, 0.08f, 0), Vector3.one, Quaternion.identity, 15).transform; impactRenderers.Add(shockwave.GetComponent<Renderer>());
            ParticleSystem impactEmbers = CreateParticles("Impact Embers", impact, m["Embers"], false, 0, 72, 1.8f, 7f, 0.14f, 3.2f, true, 24);
            ParticleSystem debris = CreateParticles("Charred Debris", impact, m["CharredDebris"], false, 0, 24, 1.4f, 5.3f, 0.18f, 2.6f, true, 21);
            ParticleSystem crown = CreateChargeDust("Smoke Crown", impact, heavyDust, true, 7, 18, 2.4f, 1.15f, 1.35f, 2.2f, 13);

            Transform ground = CreateSection("Persistent Burning Ground", root.transform);
            MeshRenderer scorch = CreateMeshRenderer("Scorched Ground", ground, meshes["Disc"], m["ScorchedTerrain"], Vector3.zero, new Vector3(10, 1, 10), Quaternion.identity, 1);
            List<Renderer> cracks = new(); cracks.Add(CreateMeshRenderer("Glowing Crack Layer 1", ground, meshes["Disc"], m["GlowingGroundCracks"], new Vector3(0, 0.018f, 0), new Vector3(9.2f, 1, 9.2f), Quaternion.identity, 2)); cracks.Add(CreateMeshRenderer("Glowing Crack Layer 2", ground, meshes["Disc"], m["GlowingGroundCracks"], new Vector3(0, 0.025f, 0), new Vector3(7.8f, 1, 7.8f), Quaternion.Euler(0, 137f, 0), 3));
            List<Transform> flamePatches = new(); List<Renderer> flameRenderers = new(); List<Renderer> perimeter = new(); List<FlamestrikeTubeShellVFX> persistentTubeShells = new();
            Transform lingeringVortex = CreateSection("Lingering Fire Vortex", ground);
            FlamestrikeTubeShellVFX lingeringOuter = AddTubeShell("Large Outer Ember Tube", lingeringVortex, meshes["Tube"], m["DarkOuterFlame"], new Vector3(5.2f, 6.2f, 5.2f), 8, 0.12f, 0.48f, 0.32f, 0.08f, 0.28f, 0.5f, false, new Color(0.95f, 0.35f, 0.24f, 0.64f)); lingeringOuter.SetAuthoringOffset(new Vector3(-0.32f, 0f, 0.16f)); persistentTubeShells.Add(lingeringOuter);
            FlamestrikeTubeShellVFX lingeringAmber = AddTubeShell("Large Amber Tube", lingeringVortex, meshes["Tube"], m["GroundFlamePatches"], new Vector3(4.25f, 5.7f, 4.25f), 9, 0.37f, 0.56f, 0.26f, 0.07f, 0.22f, 0.66f, false, new Color(1f, 0.58f, 0.3f, 0.8f)); lingeringAmber.SetAuthoringOffset(new Vector3(0.24f, 0f, -0.2f)); persistentTubeShells.Add(lingeringAmber);
            FlamestrikeTubeShellVFX lingeringGold = AddTubeShell("Large Golden Tube", lingeringVortex, meshes["Tube"], m["MainGoldenFlame"], new Vector3(3.25f, 5.1f, 3.25f), 10, 0.63f, 0.64f, 0.2f, 0.06f, 0.16f, 0.82f, false, new Color(1f, 0.84f, 0.48f, 0.94f)); lingeringGold.SetAuthoringOffset(new Vector3(-0.14f, 0f, -0.28f)); persistentTubeShells.Add(lingeringGold);
            FlamestrikeTubeShellVFX lingeringCore = AddTubeShell("Large Hot Core Tube", lingeringVortex, meshes["Tube"], m["WhiteHotCore"], new Vector3(1.8f, 4.6f, 1.8f), 11, 0.86f, 0.72f, 0.12f, 0.04f, 0.1f, 0.94f, false, new Color(1f, 1f, 0.74f, 1f)); lingeringCore.SetAuthoringOffset(new Vector3(0.16f, 0f, 0.22f)); persistentTubeShells.Add(lingeringCore);
            List<Renderer> magic = new(); List<FlamestrikeExpandingRingVFX> expandingRings = new();
            expandingRings.Add(CreateExpandingRing("Ember Vortex Shell", ground, meshes["Tube"], m["MagicalFireEnergy"], new Vector3(0, 0.045f, 0), new Vector3(2.2f, 5.6f, 2.2f), 7, 0f, 4.545455f, 0f, 16f, 0.82f));
            expandingRings.Add(CreateExpandingRing("Smoke Vortex Shell", ground, meshes["Tube"], m["Smoke"], new Vector3(0, 0.052f, 0), new Vector3(2.8f, 4.9f, 2.8f), 6, 0.12f, 3.571429f, 0f, -11f, 0.58f));
            expandingRings.Add(CreateExpandingRing("Debris Vortex Shell", ground, meshes["Tube"], m["CharredDebris"], new Vector3(0, 0.058f, 0), new Vector3(3.4f, 4.2f, 3.4f), 5, 0.24f, 2.941176f, 0f, 8f, 0.52f));
            MeshRenderer heat = CreateMeshRenderer("Volumetric Heat Shell", ground, meshes["Cone"], m["HeatDistortion"], new Vector3(0, 0.05f, 0), new Vector3(9.4f, 1.5f, 9.4f), Quaternion.identity, 7);
            ParticleSystem persistentEmbers = CreateParticles("Persistent Embers", ground, m["Embers"], true, 24, 0, 1.5f, 1.3f, 0.1f, 4.5f, true, 15);
            ParticleSystem persistentSmoke = CreateChargeDust("Rising Smoke", ground, heavyDust, true, 13, 0, 3.4f, 0.72f, 1.15f, 4.2f, 5);
            ParticleSystem ash = CreateChargeDust("Ash Haze", ground, fineDust, true, 10, 0, 2.8f, 0.32f, 0.42f, 4.7f, 4);

            Transform pulse = CreateSection("Damage Pulse", root.transform); Transform pulseRing = CreateMeshRenderer("Circular Heat Pulse", pulse, meshes["Torus"], m["FireShockwave"], new Vector3(0, 0.07f, 0), Vector3.one, Quaternion.identity, 17).transform;
            ParticleSystem pulseEmbers = CreateParticles("Pulse Ember Burst", pulse, m["Sparks"], false, 0, 32, 0.9f, 4.8f, 0.1f, 4f, true, 22);
            ParticleSystem eruptions = CreateParticles("Local Heat Sparks", pulse, m["Sparks"], false, 0, 12, 0.62f, 4.2f, 0.14f, 4.3f, true, 19);
            ParticleSystem finalSmoke = CreateChargeDust("Expiration Smoke", pulse, heavyDust, false, 0, 22, 2.6f, 1.1f, 1.3f, 3.8f, 6);
            Transform reactionsRoot = CreateSection("Target Reactions", root.transform); FlamestrikeTargetReactionVFX[] reactions = new FlamestrikeTargetReactionVFX[8]; for (int i = 0; i < reactions.Length; i++) reactions[i] = CreateReaction($"Reaction {i + 1:00}", reactionsRoot, m, sphere, fineDust);
            FlamestrikeVFX controller = root.AddComponent<FlamestrikeVFX>(); controller.ConfigureAuthoring(profile, impact, ground, mainColumn, pillars.ToArray(), shockwave, pulseRing, flash, impactRenderers.ToArray(), impactTubeShells.ToArray(), scorch, cracks.ToArray(), flamePatches.ToArray(), flameRenderers.ToArray(), perimeter.ToArray(), persistentTubeShells.ToArray(), magic.ToArray(), expandingRings.ToArray(), heat, impactEmbers, debris, crown, persistentEmbers, persistentSmoke, ash, pulseEmbers, eruptions, finalSmoke, reactions);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, HitPath); UnityEngine.Object.DestroyImmediate(root); return prefab;
        }

        private static FlamestrikeTargetReactionVFX CreateReaction(string name, Transform parent, IReadOnlyDictionary<string, Material> m, Mesh sphere, Material fineDust)
        {
            Transform root = CreateSection(name, parent); MeshRenderer flash = CreateMeshRenderer("Body Fire Flash", root, sphere, m["WhiteHotCore"], Vector3.zero, Vector3.one, Quaternion.identity, 30);
            ParticleSystem sparks = CreateParticles("Reaction Sparks", root, m["Sparks"], false, 0, 6, 0.45f, 2.7f, 0.09f, 0.25f, true, 31);
            ParticleSystem smoke = CreateChargeDust("Reaction Smoke", root, fineDust, false, 0, 2, 0.7f, 0.65f, 0.35f, 0.3f, 29);
            FlamestrikeTargetReactionVFX reaction = root.gameObject.AddComponent<FlamestrikeTargetReactionVFX>(); reaction.ConfigureAuthoring(flash, sparks, smoke); return reaction;
        }

        private static void WireAbility(GameObject targeting, GameObject casting, GameObject hit)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath); MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null) throw new MissingReferenceException("Mage Flamestrike ability or VFX definition is missing.");
            definition.ConfigureTargetingPrefab(targeting); definition.Configure(casting, null, hit, false, false, false, false, new Vector3(0, 1.1f, 0), Vector3.zero, new Vector3(0, 1.2f, 0.4f), Vector3.zero, 0f, false);
            ability.SetVisualEffects(definition); EditorUtility.SetDirty(definition); EditorUtility.SetDirty(ability);
        }

        private static void ValidateWiring(GameObject targeting, GameObject casting, GameObject hit)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath); MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(AbilityPath);
            if (definition == null || ability == null || ability.VisualEffects != definition || definition.TargetingPrefab != targeting || definition.CastingPrefab != casting || definition.HitPrefab != hit || definition.CastPrefab != null || definition.AttachHitToTarget || definition.UseHandCastingAnchors)
                throw new MissingReferenceException("Mage Flamestrike is not wired to the complete world-space VFX package.");
        }

        private static Material LoadMaterial(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path); if (material == null) throw new MissingReferenceException($"Required shared VFX material is missing: {path}"); return material;
        }

        private static Transform CreateSection(string name, Transform parent) { GameObject child = new(name); child.transform.SetParent(parent, false); return child.transform; }

        private static MeshRenderer CreateMeshRenderer(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation, int sortingOrder)
        {
            GameObject child = new(name); child.transform.SetParent(parent, false); child.transform.localPosition = position; child.transform.localRotation = rotation; child.transform.localScale = scale;
            MeshFilter filter = child.AddComponent<MeshFilter>(); filter.sharedMesh = mesh; MeshRenderer renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off; renderer.allowOcclusionWhenDynamic = false; renderer.sortingOrder = sortingOrder; return renderer;
        }

        private static Transform CreateStaticTubeCluster(string name, Transform parent, Mesh tube, Material outerMaterial, Material innerMaterial, Vector3 position, int sortingOrder, ICollection<Renderer> renderers)
        {
            Transform root = CreateSection(name, parent); root.localPosition = position;
            renderers.Add(CreateMeshRenderer("Outer Flow Tube", root, tube, outerMaterial, Vector3.zero, Vector3.one, Quaternion.identity, sortingOrder));
            renderers.Add(CreateMeshRenderer("White Hot Tube", root, tube, innerMaterial, Vector3.zero, new Vector3(0.48f, 0.86f, 0.48f), Quaternion.Euler(0, 47f, 0), sortingOrder + 1));
            return root;
        }

        private static FlamestrikeTubeShellVFX AddTubeShell(
            string name, Transform parent, Mesh tube, Material material, Vector3 scale, int sortingOrder,
            float phase, float speed, float radialExpansion, float verticalExpansion, float lift,
            float opacity, bool loop, Color tintMultiplier)
        {
            MeshRenderer renderer = CreateMeshRenderer(name, parent, tube, material, Vector3.zero, scale, Quaternion.Euler(0, phase * 271f, 0), sortingOrder);
            FlamestrikeTubeShellVFX shell = renderer.gameObject.AddComponent<FlamestrikeTubeShellVFX>();
            shell.ConfigureAuthoring(renderer, scale, phase, speed, radialExpansion, verticalExpansion, lift, opacity, loop, tintMultiplier);
            return shell;
        }

        private static FlamestrikeExpandingRingVFX CreateExpandingRing(
            string name, Transform parent, Mesh shellMesh, Material material, Vector3 position, Vector3 scale,
            int sortingOrder, float delay, float expansion, float finalHeight, float rotationSpeed, float opacity)
        {
            MeshRenderer renderer = CreateMeshRenderer(name, parent, shellMesh, material, position, scale, Quaternion.identity, sortingOrder);
            FlamestrikeExpandingRingVFX ring = renderer.gameObject.AddComponent<FlamestrikeExpandingRingVFX>();
            ring.ConfigureAuthoring(renderer, scale, delay, expansion, finalHeight, rotationSpeed, opacity);
            return ring;
        }

        private static LineRenderer CreateLine(string name, Transform parent, Material material, int sortingOrder)
        {
            GameObject child = new(name); child.transform.SetParent(parent, false); LineRenderer line = child.AddComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true;
            line.textureMode = LineTextureMode.Stretch; line.numCornerVertices = 2; line.numCapVertices = 2; line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false; line.sortingOrder = sortingOrder; line.positionCount = 0; return line;
        }

        private static ParticleSystem CreateParticles(string name, Transform parent, Material material, bool loop, int rate, int burst, float lifetime, float speed, float size, float radius, bool worldSpace, int sortingOrder)
        {
            GameObject child = new(name); child.transform.SetParent(parent, false); ParticleSystem system = child.AddComponent<ParticleSystem>(); ParticleSystem.MainModule main = system.main;
            main.loop = loop; main.playOnAwake = false; main.startLifetime = lifetime; main.startSpeed = speed; main.startSize = size; main.maxParticles = Mathf.Max(16, rate * Mathf.CeilToInt(lifetime) + burst * 2); main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            ParticleSystem.EmissionModule emission = system.emission; emission.rateOverTime = loop ? rate : 0; if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            ParticleSystem.ShapeModule shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = radius;
            ParticleSystem.VelocityOverLifetimeModule velocity = system.velocityOverLifetime; velocity.enabled = true; velocity.y = new ParticleSystem.MinMaxCurve(speed * 0.25f, speed * 0.75f); velocity.x = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f); velocity.z = new ParticleSystem.MinMaxCurve(-0.45f, 0.45f);
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime; color.enabled = true; Gradient gradient = new(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1, 0.18f, 0.02f), 0.72f) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.12f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.alignment = ParticleSystemRenderSpace.View; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.sortingOrder = sortingOrder;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return system;
        }

        private static ParticleSystem CreateChargeDust(string name, Transform parent, Material material, bool loop, int rate, int burst, float lifetime, float speed, float size, float radius, int sortingOrder)
        {
            GameObject child = new(name); child.transform.SetParent(parent, false); ParticleSystem system = child.AddComponent<ParticleSystem>(); ParticleSystem.MainModule main = system.main;
            main.loop = loop; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.78f, lifetime * 1.2f); main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed); main.startSize = new ParticleSystem.MinMaxCurve(size * 0.72f, size * 1.25f); main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI); main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.38f, 0.28f, 0.23f, 0.72f), new Color(0.16f, 0.12f, 0.11f, 0.58f)); main.maxParticles = Mathf.Max(32, rate * Mathf.CeilToInt(lifetime) + burst * 2);
            ParticleSystem.EmissionModule emission = system.emission; emission.rateOverTime = loop ? rate : 0; if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            ParticleSystem.ShapeModule shape = system.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = radius; shape.scale = new Vector3(1f, 0.25f, 1f);
            ParticleSystem.NoiseModule noise = system.noise; noise.enabled = true; noise.strength = 0.28f; noise.frequency = 0.42f; noise.scrollSpeed = 0.18f;
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime; color.enabled = true; Gradient gradient = new(); gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(0.72f, 0.55f, 0.46f), 0.72f) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.82f, 0.08f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            ParticleSystem.SizeOverLifetimeModule sizeOverLife = system.sizeOverLifetime; sizeOverLife.enabled = true; AnimationCurve scaleCurve = new(new Keyframe(0, 0.62f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.2f)); sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, scaleCurve);
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation; sheet.enabled = true; sheet.mode = ParticleSystemAnimationMode.Grid; sheet.animation = ParticleSystemAnimationType.WholeSheet; sheet.numTilesX = 4; sheet.numTilesY = 2; sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f); sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, 0.999f); sheet.cycleCount = 1;
            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard; renderer.alignment = ParticleSystemRenderSpace.View; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.sortingOrder = sortingOrder;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); return system;
        }
    }
}
