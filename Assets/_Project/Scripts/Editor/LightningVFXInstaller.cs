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
    public static class LightningVFXInstaller
    {
        private const string Root = "Assets/_Project/VFX/Lightning";
        private const string TextureFolder = Root + "/Textures";
        private const string MaterialFolder = Root + "/Materials";
        private const string MeshFolder = Root + "/Meshes";
        private const string ProfileFolder = Root + "/Profiles";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string DocumentationFolder = Root + "/Documentation";
        private const string ProfilePath = ProfileFolder + "/LightningVFX_Default.asset";
        private const string CastPrefabPath = PrefabFolder + "/LightningCastVFX.prefab";
        private const string BeamPrefabPath = PrefabFolder + "/LightningBeamVFX.prefab";
        private const string ImpactPrefabPath = PrefabFolder + "/LightningImpactVFX.prefab";
        private const string AftermathPrefabPath = PrefabFolder + "/LightningAftermathVFX.prefab";
        private const string CompletePrefabPath = PrefabFolder + "/LightningVFX.prefab";
        private const string ChargeWindMeshPath = MeshFolder + "/Lightning_ChargeWindTorus.asset";
        private const string DefinitionPath = "Assets/_Project/VFX/Definitions/Shaman_Lightning_Bolt_VFX.asset";
        private const string ShamanAbilityPath = "Assets/_Project/Configs/Abilities/Shaman_Lightning_Bolt.asset";
        private const string TrogAbilityPath = "Assets/_Project/Configs/Abilities/Trog_Lightning_Bolt.asset";
        private const string ChargeHeavyDustPath = "Assets/_Project/VFX/Charge/Textures/Charge_HeavyDustAtlas.png";
        private const string ChargeFineDustPath = "Assets/_Project/VFX/Charge/Textures/Charge_FineDustAtlas.png";
        private const string ChargeDirtPath = "Assets/_Project/VFX/Charge/Textures/Charge_DirtChunksAtlas.png";
        private const string ChargeGroundBurstPath = "Assets/_Project/VFX/Charge/Textures/Charge_GroundBurstAtlas.png";
        private const string ChargeAirCompressionPath = "Assets/_Project/VFX/Charge/Textures/Charge_AirCompressionAtlas.png";
        private const string BashDustRingPath = "Assets/_Project/VFX/Bash/Textures/Bash_DustRing.png";

        [MenuItem("Tools/RPG Clone/VFX/Build Lightning VFX")]
        public static void Build()
        {
            EnsureFolders();
            ConfigureTextureImporters();
            LightningVFXProfile profile = CreateProfile();
            Dictionary<string, Material> materials = CreateMaterials();
            GameObject castPrefab = CreateCastPrefab(profile, materials);
            GameObject beamPrefab = CreateBeamPrefab(profile, materials);
            GameObject impactPrefab = CreateImpactPrefab(profile, materials);
            GameObject aftermathPrefab = CreateAftermathPrefab(profile, materials);
            CreateCompletePrefab(profile, castPrefab, beamPrefab, impactPrefab, aftermathPrefab);
            WireDefinition(castPrefab, beamPrefab, impactPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Validate();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(CompletePrefabPath);
            Debug.Log("Lightning VFX built: generated electrical art, Charge/Bash-derived world dust, layered beam, impact, aftermath, and shared Shaman/Trog wiring are ready.");
        }

        [MenuItem("Tools/RPG Clone/VFX/Validate Lightning VFX")]
        public static void Validate()
        {
            LightningVFXProfile profile = AssetDatabase.LoadAssetAtPath<LightningVFXProfile>(ProfilePath);
            GameObject cast = AssetDatabase.LoadAssetAtPath<GameObject>(CastPrefabPath);
            GameObject beam = AssetDatabase.LoadAssetAtPath<GameObject>(BeamPrefabPath);
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
            GameObject aftermath = AssetDatabase.LoadAssetAtPath<GameObject>(AftermathPrefabPath);
            GameObject complete = AssetDatabase.LoadAssetAtPath<GameObject>(CompletePrefabPath);
            if (profile == null || cast == null || beam == null || impact == null || aftermath == null || complete == null)
            {
                throw new MissingReferenceException("Lightning VFX profile or prefab deliverables are missing. Run Build Lightning VFX.");
            }

            if (cast.GetComponent<LightningCastVFX>() == null || beam.GetComponent<LightningBeamVFX>() == null
                || beam.GetComponent<LightningAftermathVFX>() == null || impact.GetComponent<LightningImpactVFX>() == null
                || aftermath.GetComponent<LightningAftermathVFX>() == null || complete.GetComponent<LightningVFX>() == null)
            {
                throw new MissingComponentException("Lightning VFX prefabs are missing one or more phase controllers.");
            }

            LightningChargeWindMeshVFX windMesh = cast.GetComponentInChildren<LightningChargeWindMeshVFX>(true);
            if (windMesh == null || windMesh.RingCount != 3)
            {
                throw new MissingComponentException("Lightning cast must use the three-layer seamless charge wind mesh.");
            }

            foreach (MeshFilter filter in windMesh.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null)
                {
                    throw new MissingReferenceException($"Lightning charge wind mesh is missing on '{filter.name}'.");
                }
            }

            foreach (ParticleSystem particle in cast.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (particle.name.Contains("Pressure And Air Distortion"))
                {
                    throw new UnityException("The legacy camera-facing pressure billboard must not be present in LightningCastVFX.");
                }

                if ((particle.name.Contains("Dust") || particle.name.Contains("Dirt") || particle.name.Contains("Ground"))
                    && particle.main.simulationSpace != ParticleSystemSimulationSpace.World)
                {
                    throw new UnityException($"Lightning environmental particle '{particle.name}' must simulate in world space.");
                }
            }

            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            MMOAbilityDefinition shaman = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(ShamanAbilityPath);
            MMOAbilityDefinition trog = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(TrogAbilityPath);
            if (definition == null || shaman == null || trog == null || definition.CastingPrefab != cast || definition.CastPrefab != beam
                || definition.HitPrefab != impact || !definition.CastPrefabControlsHitTiming || shaman.VisualEffects != definition || trog.VisualEffects != definition)
            {
                throw new MissingReferenceException("Shared Lightning VFX definition is not wired consistently to both Shaman and Trog Lightning Bolt.");
            }

            foreach (string texture in RuntimeTexturePaths())
            {
                Texture2D asset = AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
                if (asset == null)
                {
                    throw new MissingReferenceException($"Lightning texture is missing: {texture}");
                }
            }

            Debug.Log("Lightning VFX validation passed: seamless mesh charge wind, generated textures, reusable URP materials/profile, Charge/Bash dust reuse, phase lifecycles, and shared Shaman/Trog network presentation wiring are valid.", complete);
        }

        private static LightningVFXProfile CreateProfile()
        {
            LightningVFXProfile profile = AssetDatabase.LoadAssetAtPath<LightningVFXProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<LightningVFXProfile>();
                profile.ResetToProductionDefaults();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            Shader meshShader = Shader.Find("Universal Render Pipeline/Unlit") ?? particleShader;
            if (particleShader == null || meshShader == null)
            {
                throw new MissingReferenceException("A URP-compatible unlit particle shader could not be found.");
            }

            return new Dictionary<string, Material>
            {
                ["Core"] = CreateMaterial("Lightning_WhiteCore", particleShader, TextureFolder + "/Lightning_Core.png", new Color(1f, 0.98f, 0.84f, 1f), true),
                ["Main"] = CreateMaterial("Lightning_CyanBody", particleShader, TextureFolder + "/Lightning_Main.png", new Color(0.2f, 0.95f, 1f, 0.96f), true),
                ["Outer"] = CreateMaterial("Lightning_BlueOuter", particleShader, TextureFolder + "/Lightning_Outer.png", new Color(0.08f, 0.38f, 1f, 0.72f), true),
                ["Violet"] = CreateMaterial("Lightning_VioletGlow", particleShader, TextureFolder + "/Lightning_Outer.png", new Color(0.48f, 0.16f, 1f, 0.42f), true),
                ["Branch"] = CreateMaterial("Lightning_Branches", particleShader, TextureFolder + "/Lightning_Core.png", new Color(0.38f, 0.92f, 1f, 0.92f), true),
                ["Electrical"] = CreateMaterial("Lightning_ElectricalSprites", particleShader, TextureFolder + "/Lightning_ElectricalAtlas.png", Color.white, true),
                ["Sparks"] = CreateMaterial("Lightning_FastSparks", particleShader, TextureFolder + "/Lightning_BranchesAtlas.png", Color.white, true),
                ["Ground"] = CreateMaterial("Lightning_GroundCrawler", particleShader, TextureFolder + "/Lightning_Core.png", new Color(0.18f, 0.72f, 1f, 0.72f), true),
                ["Pressure"] = CreateMaterial("Lightning_PressureWave", particleShader, TextureFolder + "/Lightning_PressureRing.png", new Color(0.45f, 0.9f, 1f, 0.24f), false),
                ["WindMesh"] = CreateMaterial("Lightning_ChargeWindMesh", meshShader, TextureFolder + "/Lightning_ChargeWindRibbon.png", Color.white, true),
                ["HeavyDust"] = CreateMaterial("Lightning_HeavyDust", particleShader, ChargeHeavyDustPath, new Color(0.62f, 0.48f, 0.31f, 0.72f), false),
                ["FineDust"] = CreateMaterial("Lightning_FineDust", particleShader, ChargeFineDustPath, new Color(0.82f, 0.7f, 0.5f, 0.48f), false),
                ["Dirt"] = CreateMaterial("Lightning_DirtDebris", particleShader, ChargeDirtPath, new Color(0.48f, 0.38f, 0.26f, 0.86f), false),
                ["GroundBurst"] = CreateMaterial("Lightning_GroundBurst", particleShader, ChargeGroundBurstPath, new Color(0.72f, 0.6f, 0.43f, 0.68f), false),
                ["DustRing"] = CreateMaterial("Lightning_DustRing", particleShader, BashDustRingPath, new Color(0.76f, 0.68f, 0.54f, 0.58f), false),
                ["Smoke"] = CreateMaterial("Lightning_ImpactSmoke", particleShader, TextureFolder + "/Lightning_SmokeAtlas.png", new Color(0.24f, 0.31f, 0.46f, 0.64f), false)
            };
        }

        private static GameObject CreateCastPrefab(LightningVFXProfile profile, Dictionary<string, Material> materials)
        {
            GameObject root = new("LightningCastVFX");
            try
            {
                Transform handRoot = CreateSection("Hand Electricity", root.transform);
                LineRenderer[] handArcs = new LineRenderer[5];
                for (int i = 0; i < handArcs.Length; i++)
                {
                    handArcs[i] = CreateLine($"Hand Arc {i + 1:00}", handRoot, i == 0 ? materials["Core"] : i % 2 == 0 ? materials["Main"] : materials["Branch"], 20 + i);
                }

                Transform groundElectricity = CreateSection("Ground Crawlers", root.transform);
                LineRenderer[] crawlers = new LineRenderer[6];
                for (int i = 0; i < crawlers.Length; i++)
                {
                    crawlers[i] = CreateLine($"Ground Crawler {i + 1:00}", groundElectricity, materials["Ground"], 5 + i);
                }

                Transform attached = CreateSection("Attached Charge", root.transform);
                ParticleSystem core = CreateParticle("Electrical Charge Core", attached, materials["Electrical"], false, true, 2, 2, 0.25f, 64, 18);
                ParticleSystem handSparks = CreateParticle("Hand Sparks", attached, materials["Sparks"], true, true, 4, 4, -1f, 128, 22);
                ParticleSystem wristSparks = CreateParticle("Wrist And Forearm Sparks", attached, materials["Sparks"], true, true, 4, 4, -1f, 128, 19);

                Transform windRoot = CreateSection("Seamless Charge Wind Mesh", attached);
                Mesh windMesh = CreateOrUpdateChargeWindMesh();
                MeshRenderer[] windRings = new MeshRenderer[3];
                for (int i = 0; i < windRings.Length; i++)
                {
                    windRings[i] = CreateWindRing($"Contracting Wind Ring {i + 1:00}", windRoot, windMesh, materials["WindMesh"], 2 + i);
                }
                LightningChargeWindMeshVFX chargeWind = windRoot.gameObject.AddComponent<LightningChargeWindMeshVFX>();
                chargeWind.ConfigureAuthoring(profile, windRings);

                Transform environmental = CreateSection("World Space Environment", root.transform);
                ParticleSystem ring = CreateParticle("Circular Dust Ring", environmental, materials["DustRing"], true, false, 1, 1, -1f, 4, 1);
                ParticleSystem heavy = CreateEnvironmentalDust("Inward Heavy Dust", environmental, materials["HeavyDust"], 4, 2, 256, 2);
                ParticleSystem fine = CreateEnvironmentalDust("Inward Fine Dust", environmental, materials["FineDust"], 4, 2, 384, 3);
                ParticleSystem dirt = CreateDebris("Inward Dirt Fragments", environmental, materials["Dirt"], 4, 2, 96, 4);
                ParticleSystem release = CreateEnvironmentalDust("Release Ground Burst", environmental, materials["GroundBurst"], 4, 1, 192, 6);

                LightningCastVFX controller = root.AddComponent<LightningCastVFX>();
                controller.ConfigureAuthoring(profile, handArcs, crawlers, chargeWind, new[] { core, handSparks, wristSparks }, new[] { ring, heavy, fine, dirt, release });
                return SavePrefab(root, CastPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateBeamPrefab(LightningVFXProfile profile, Dictionary<string, Material> materials)
        {
            GameObject root = new("LightningBeamVFX");
            try
            {
                Transform mainRoot = CreateSection("Thick Multilayer Beam", root.transform);
                LineRenderer[] main =
                {
                    CreateLine("White Hot Core", mainRoot, materials["Core"], 40),
                    CreateLine("Cyan Main Body", mainRoot, materials["Main"], 36),
                    CreateLine("Blue Outer Body", mainRoot, materials["Outer"], 32),
                    CreateLine("Violet Outer Glow", mainRoot, materials["Violet"], 28)
                };

                Transform secondaryRoot = CreateSection("Secondary Offset Bolts", root.transform);
                LineRenderer[] secondary = new LineRenderer[4];
                for (int i = 0; i < secondary.Length; i++) secondary[i] = CreateLine($"Secondary Bolt {i + 1:00}", secondaryRoot, materials["Branch"], 38 + i);

                Transform branchRoot = CreateSection("Branching Arcs", root.transform);
                LineRenderer[] branches = new LineRenderer[8];
                for (int i = 0; i < branches.Length; i++) branches[i] = CreateLine($"Branch {i + 1:00}", branchRoot, materials["Branch"], 34 + i);

                Transform particles = CreateSection("Beam Particles", root.transform);
                ParticleSystem flashes = CreateParticle("Beam Flashes", particles, materials["Electrical"], true, false, 2, 2, 0f, 64, 48);
                ParticleSystem sheath = CreateParticle("Electrical Particle Sheath", particles, materials["Sparks"], true, false, 4, 4, -1f, 256, 44, ParticleSystemRenderMode.Stretch);
                LineRenderer residual = CreateLine("Residual Aftermath Arc", root.transform, materials["Branch"], 26);

                LightningBeamVFX beam = root.AddComponent<LightningBeamVFX>();
                beam.ConfigureAuthoring(profile, main, secondary, branches, flashes, sheath);
                LightningAftermathVFX aftermath = root.AddComponent<LightningAftermathVFX>();
                aftermath.ConfigureAuthoring(profile, residual);
                return SavePrefab(root, BeamPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateImpactPrefab(LightningVFXProfile profile, Dictionary<string, Material> materials)
        {
            GameObject root = new("LightningImpactVFX");
            try
            {
                Transform targetArcs = CreateSection("Target Body Arcs", root.transform);
                LineRenderer[] body = new LineRenderer[6];
                for (int i = 0; i < body.Length; i++) body[i] = CreateLine($"Body Arc {i + 1:00}", targetArcs, i % 3 == 0 ? materials["Violet"] : materials["Branch"], 40 + i);

                Transform groundRoot = CreateSection("Ground Strike Arcs", root.transform);
                LineRenderer[] ground = new LineRenderer[5];
                for (int i = 0; i < ground.Length; i++) ground[i] = CreateLine($"Ground Strike {i + 1:00}", groundRoot, materials["Ground"], 32 + i);

                Transform impact = CreateSection("Layered Impact", root.transform);
                ParticleSystem flash = CreateParticle("Contact Flash", impact, materials["Electrical"], true, false, 2, 2, 0f, 16, 50);
                ParticleSystem burst = CreateParticle("Electrical Burst", impact, materials["Electrical"], true, false, 2, 2, 0.75f, 64, 46);
                ParticleSystem ring = CreateParticle("Impact Shock Ring", impact, materials["Electrical"], true, false, 2, 2, 0.5f, 8, 38);
                ParticleSystem sparks = CreateParticle("Impact Sparks", impact, materials["Sparks"], true, false, 4, 4, -1f, 256, 44, ParticleSystemRenderMode.Stretch);
                ParticleSystem heavy = CreateEnvironmentalDust("Impact Heavy Dust", impact, materials["HeavyDust"], 4, 2, 128, 6);
                ParticleSystem fine = CreateEnvironmentalDust("Impact Fine Dust", impact, materials["FineDust"], 4, 2, 192, 7);
                ParticleSystem dirt = CreateDebris("Impact Dirt Debris", impact, materials["Dirt"], 4, 2, 96, 8);
                ParticleSystem smoke = CreateEnvironmentalDust("Impact Smoke", impact, materials["Smoke"], 2, 2, 48, 4);

                LightningImpactVFX controller = root.AddComponent<LightningImpactVFX>();
                controller.ConfigureAuthoring(profile, body, ground, new[] { flash, burst, ring, sparks, heavy, fine, dirt, smoke });
                return SavePrefab(root, ImpactPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateAftermathPrefab(LightningVFXProfile profile, Dictionary<string, Material> materials)
        {
            GameObject root = new("LightningAftermathVFX");
            try
            {
                LineRenderer residual = CreateLine("Residual Arc", root.transform, materials["Branch"], 24);
                root.AddComponent<LightningAftermathVFX>().ConfigureAuthoring(profile, residual);
                return SavePrefab(root, AftermathPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateCompletePrefab(LightningVFXProfile profile, GameObject cast, GameObject beam, GameObject impact, GameObject aftermath)
        {
            GameObject root = new("LightningVFX");
            try
            {
                root.AddComponent<LightningVFX>().Configure(profile, cast, beam, impact, aftermath);
                SavePrefab(root, CompletePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WireDefinition(GameObject cast, GameObject beam, GameObject impact)
        {
            MMOAbilityVfxDefinition definition = AssetDatabase.LoadAssetAtPath<MMOAbilityVfxDefinition>(DefinitionPath);
            if (definition == null)
            {
                throw new MissingReferenceException($"Lightning ability VFX definition is missing: {DefinitionPath}");
            }

            definition.Configure(cast, beam, impact, true, false, true, false, Vector3.zero, Vector3.zero,
                new Vector3(0f, 1.18f, 0.48f), new Vector3(0f, 0.85f, 0f), 0f, true);
            EditorUtility.SetDirty(definition);

            foreach (string path in new[] { ShamanAbilityPath, TrogAbilityPath })
            {
                MMOAbilityDefinition ability = AssetDatabase.LoadAssetAtPath<MMOAbilityDefinition>(path);
                if (ability == null)
                {
                    throw new MissingReferenceException($"Lightning ability asset is missing: {path}");
                }

                ability.SetVisualEffects(definition);
                EditorUtility.SetDirty(ability);
            }
        }

        private static LineRenderer CreateLine(string name, Transform parent, Material material, int order)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * 0.1f);
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = order;
            line.enabled = false;
            return line;
        }

        private static MeshRenderer CreateWindRing(string name, Transform parent, Mesh mesh, Material material, int order)
        {
            GameObject child = new(name);
            child.transform.SetParent(parent, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.sortingOrder = order;
            renderer.enabled = false;
            return renderer;
        }

        private static Mesh CreateOrUpdateChargeWindMesh()
        {
            const int majorSegments = 48;
            const int minorSegments = 8;
            // A broader tube exposes more of the animated lightning ribbon while remaining
            // closed geometry from every camera angle.
            const float tubeRadius = 0.14f;

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(ChargeWindMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "Lightning_ChargeWindTorus" };
                AssetDatabase.CreateAsset(mesh, ChargeWindMeshPath);
            }

            int stride = minorSegments + 1;
            Vector3[] vertices = new Vector3[(majorSegments + 1) * stride];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = new int[majorSegments * minorSegments * 6];

            for (int major = 0; major <= majorSegments; major++)
            {
                float u = major / (float)majorSegments;
                float angle = u * Mathf.PI * 2f;
                Vector3 radial = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 center = radial + Vector3.up * Mathf.Sin(angle * 3f) * 0.035f;
                for (int minor = 0; minor <= minorSegments; minor++)
                {
                    float v = minor / (float)minorSegments;
                    float tubeAngle = v * Mathf.PI * 2f;
                    Vector3 normal = radial * Mathf.Cos(tubeAngle) + Vector3.up * Mathf.Sin(tubeAngle);
                    int index = major * stride + minor;
                    vertices[index] = center + normal * tubeRadius;
                    normals[index] = normal.normalized;
                    uvs[index] = new Vector2(u, v);
                }
            }

            int triangle = 0;
            for (int major = 0; major < majorSegments; major++)
            {
                for (int minor = 0; minor < minorSegments; minor++)
                {
                    int current = major * stride + minor;
                    int next = current + stride;
                    triangles[triangle++] = current;
                    triangles[triangle++] = next;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = current + 1;
                    triangles[triangle++] = next;
                    triangles[triangle++] = next + 1;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static ParticleSystem CreateParticle(
            string name,
            Transform parent,
            Material material,
            bool worldSpace,
            bool continuous,
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
            main.loop = continuous;
            main.duration = 1f;
            main.playOnAwake = false;
            main.simulationSpace = worldSpace ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 0.25f;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
            main.maxParticles = maxParticles;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = continuous;
            emission.rateOverTime = continuous ? (name.Contains("Core") ? 1f : name.Contains("Pressure") ? 2.5f : 24f) : 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = continuous && !name.Contains("Core") && !name.Contains("Pressure");
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = name.Contains("Wrist") ? 0.38f : 0.18f;

            ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = renderMode;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.lengthScale = renderMode == ParticleSystemRenderMode.Stretch ? 2.6f : 1f;
            renderer.velocityScale = renderMode == ParticleSystemRenderMode.Stretch ? 0.22f : 0f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = order;
            renderer.enableGPUInstancing = true;
            ConfigureAtlas(system, columns, rows, fixedFrame);
            ConfigureFadeAndScale(system, 0.42f, 1f, 0.2f, 0.02f, 0.68f);
            return system;
        }

        private static ParticleSystem CreateEnvironmentalDust(string name, Transform parent, Material material, int columns, int rows, int maxParticles, int order)
        {
            ParticleSystem system = CreateParticle(name, parent, material, true, false, columns, rows, -1f, maxParticles, order);
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            noise.strength = 0.28f;
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.18f;
            ParticleSystem.LimitVelocityOverLifetimeModule drag = system.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.dampen = 0.18f;
            ConfigureFadeAndScale(system, 0.52f, 1f, 1.18f, 0.012f, 0.62f);
            return system;
        }

        private static ParticleSystem CreateDebris(string name, Transform parent, Material material, int columns, int rows, int maxParticles, int order)
        {
            ParticleSystem system = CreateParticle(name, parent, material, true, false, columns, rows, -1f, maxParticles, order);
            ParticleSystem.MainModule main = system.main;
            main.gravityModifier = 1.35f;
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            ConfigureFadeAndScale(system, 0.82f, 1f, 0.28f, 0.015f, 0.76f);
            return system;
        }

        private static void ConfigureAtlas(ParticleSystem system, int columns, int rows, float fixedFrame)
        {
            if (columns <= 1 && rows <= 1) return;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Grid;
            sheet.animation = ParticleSystemAnimationType.WholeSheet;
            sheet.numTilesX = columns;
            sheet.numTilesY = rows;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            sheet.startFrame = fixedFrame >= 0f ? new ParticleSystem.MinMaxCurve(fixedFrame) : new ParticleSystem.MinMaxCurve(0f, 0.999f);
            sheet.cycleCount = 1;
        }

        private static void ConfigureFadeAndScale(ParticleSystem system, float startScale, float middleScale, float endScale, float fadeInEnd, float fadeOutStart)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(fadeInEnd > 0.02f ? 0f : 1f, 0f), new GradientAlphaKey(1f, Mathf.Max(0.02f, fadeInEnd)), new GradientAlphaKey(1f, fadeOutStart), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, startScale), new Keyframe(0.42f, middleScale), new Keyframe(1f, endScale)));
        }

        private static Material CreateMaterial(string name, Shader shader, string texturePath, Color color, bool additive)
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

            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
            if (texture == null) throw new MissingReferenceException($"Lightning material source texture is missing: {texturePath}");
            SetTextureIfPresent(material, "_BaseMap", texture);
            SetTextureIfPresent(material, "_MainTex", texture);
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
            SetFloatIfPresent(material, "_Surface", 1f);
            SetFloatIfPresent(material, "_Blend", additive ? 1f : 0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTextureImporters()
        {
            foreach (string path in GeneratedTexturePaths())
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Default;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = path.EndsWith("Lightning_Core.png", StringComparison.Ordinal)
                    || path.EndsWith("Lightning_Main.png", StringComparison.Ordinal)
                    || path.EndsWith("Lightning_Outer.png", StringComparison.Ordinal)
                    || path.EndsWith("Lightning_ChargeWindRibbon.png", StringComparison.Ordinal)
                    ? TextureWrapMode.Repeat
                    : TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static IEnumerable<string> RuntimeTexturePaths()
        {
            foreach (string path in GeneratedTexturePaths()) yield return path;
            yield return ChargeHeavyDustPath;
            yield return ChargeFineDustPath;
            yield return ChargeDirtPath;
            yield return ChargeGroundBurstPath;
            yield return ChargeAirCompressionPath;
            yield return BashDustRingPath;
        }

        private static IEnumerable<string> GeneratedTexturePaths()
        {
            yield return TextureFolder + "/Lightning_Core.png";
            yield return TextureFolder + "/Lightning_Main.png";
            yield return TextureFolder + "/Lightning_Outer.png";
            yield return TextureFolder + "/Lightning_BranchesAtlas.png";
            yield return TextureFolder + "/Lightning_ElectricalAtlas.png";
            yield return TextureFolder + "/Lightning_DebrisAtlas.png";
            yield return TextureFolder + "/Lightning_SmokeAtlas.png";
            yield return TextureFolder + "/Lightning_PressureRing.png";
            yield return TextureFolder + "/Lightning_ChargeWindRibbon.png";
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }

        private static Transform CreateSection(string name, Transform parent)
        {
            GameObject section = new(name);
            section.transform.SetParent(parent, false);
            return section.transform;
        }

        private static void EnsureFolders()
        {
            foreach (string path in new[] { Root, TextureFolder, MaterialFolder, MeshFolder, ProfileFolder, PrefabFolder, DocumentationFolder })
            {
                CreateFolderIfMissing(path);
            }
        }

        private static void CreateFolderIfMissing(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            if (!string.IsNullOrWhiteSpace(parent)) CreateFolderIfMissing(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property)) material.SetTexture(property, texture);
        }

        private static void SetColorIfPresent(Material material, string property, Color color)
        {
            if (material.HasProperty(property)) material.SetColor(property, color);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }
    }
}
