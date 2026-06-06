using System;
using System.IO;
using RPGClone.Player;
using RPGClone.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools
{
    public static class StarterWorldBuilder
    {
        private const string RootFolder = "Assets/_Project";
        private const string GeneratedFolder = RootFolder + "/Generated";
        private const string MaterialFolder = GeneratedFolder + "/Materials";
        private const string TextureFolder = GeneratedFolder + "/Textures";
        private const string TerrainFolder = GeneratedFolder + "/Terrain";
        private const string ConfigFolder = RootFolder + "/Configs";
        private const string PrefabFolder = RootFolder + "/Prefabs/Player";
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = SceneFolder + "/OrcishStarterValley.unity";
        private const float TerrainSize = 520f;
        private const float TerrainHeight = 70f;

        [MenuItem("Tools/RPG Clone/Build Starter World")]
        public static void BuildStarterWorld()
        {
            EnsureFolders();

            var palette = CreatePalette();
            MMOPlayerMovementConfig movementConfig = CreateMovementConfig();
            MMOThirdPersonCameraConfig cameraConfig = CreateCameraConfig();
            Terrain terrain = CreateTerrain(palette);
            MMOClassicGrassFoliageBuilder.ApplyToTerrain(terrain);

            GameObject worldRoot = new("Starter World");
            GameObject markerRoot = new("World Markers");
            markerRoot.transform.SetParent(worldRoot.transform);

            BuildLighting();
            BuildWorldDressing(terrain, palette, worldRoot.transform, markerRoot.transform);
            GameObject player = InstantiatePlayer(terrain, palette, movementConfig);
            Camera camera = CreateGameplayCamera(player, cameraConfig);
            WirePlayerCamera(player, camera);
            MMOHudSceneInstaller.InstallIntoOpenScene(false);
            CreateNavigationSurface(worldRoot.transform);

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = player;
            Debug.Log($"Starter world built at {ScenePath}. Selected {player.name}.");
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(GeneratedFolder);
            CreateFolderIfMissing(MaterialFolder);
            CreateFolderIfMissing(TextureFolder);
            CreateFolderIfMissing(TerrainFolder);
            CreateFolderIfMissing(ConfigFolder);
            CreateFolderIfMissing(RootFolder + "/Prefabs");
            CreateFolderIfMissing(PrefabFolder);
            CreateFolderIfMissing(SceneFolder);
        }

        private static void CreateFolderIfMissing(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(assetPath);
            if (!string.IsNullOrEmpty(parent))
            {
                CreateFolderIfMissing(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }

        private static WorldPalette CreatePalette()
        {
            return new WorldPalette
            {
                Grass = CreateMaterial("Ash Grass", new Color(0.27f, 0.39f, 0.24f)),
                Clay = CreateMaterial("Dry Red Clay", new Color(0.53f, 0.27f, 0.16f)),
                Dirt = CreateMaterial("Packed Trail Dirt", new Color(0.39f, 0.30f, 0.21f)),
                Rock = CreateMaterial("Dark Basalt Rock", new Color(0.19f, 0.18f, 0.17f)),
                Wood = CreateMaterial("Weathered Palisade Wood", new Color(0.34f, 0.22f, 0.13f)),
                Hide = CreateMaterial("Canvas Hide", new Color(0.48f, 0.39f, 0.29f)),
                Player = CreateMaterial("Player Capsule Blue", new Color(0.18f, 0.38f, 0.75f)),
                Friendly = CreateMaterial("Friendly Capsule Amber", new Color(0.95f, 0.66f, 0.22f)),
                Hostile = CreateMaterial("Hostile Capsule Red", new Color(0.72f, 0.18f, 0.16f)),
                Marker = CreateMaterial("Marker White", new Color(0.88f, 0.82f, 0.68f))
            };
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{Sanitize(name)}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static MMOPlayerMovementConfig CreateMovementConfig()
        {
            string path = $"{ConfigFolder}/DefaultPlayerMovement.asset";
            MMOPlayerMovementConfig config = AssetDatabase.LoadAssetAtPath<MMOPlayerMovementConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MMOPlayerMovementConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            config.forwardSpeed = 7.25f;
            config.backwardSpeed = 4.1f;
            config.strafeSpeed = 5.4f;
            config.acceleration = 34f;
            config.deceleration = 42f;
            config.keyboardTurnDegreesPerSecond = 150f;
            config.mouseFacingSharpness = 24f;
            config.jumpHeight = 1.35f;
            config.gravity = 28f;
            config.groundedStickVelocity = -2f;
            EditorUtility.SetDirty(config);
            return config;
        }

        private static MMOThirdPersonCameraConfig CreateCameraConfig()
        {
            string path = $"{ConfigFolder}/DefaultThirdPersonCamera.asset";
            MMOThirdPersonCameraConfig config = AssetDatabase.LoadAssetAtPath<MMOThirdPersonCameraConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MMOThirdPersonCameraConfig>();
                AssetDatabase.CreateAsset(config, path);
            }

            config.defaultDistance = 8.5f;
            config.minDistance = 2.2f;
            config.maxDistance = 16f;
            config.targetHeight = 1.45f;
            config.defaultPitch = 18f;
            config.minPitch = -18f;
            config.maxPitch = 66f;
            config.mouseYawSensitivity = 0.12f;
            config.mousePitchSensitivity = 0.1f;
            config.zoomUnitsPerScrollUnit = 0.02f;
            config.idleYawFollowSharpness = 8f;
            config.positionSharpness = 18f;
            config.collisionRadius = 0.28f;
            config.collisionPadding = 0.18f;
            config.collisionMask = ~0;
            EditorUtility.SetDirty(config);
            return config;
        }

        private static Terrain CreateTerrain(WorldPalette palette)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            TerrainLayer[] terrainLayers =
            {
                CreateTerrainLayer("AshGrass", palette.Grass, new Color(0.27f, 0.39f, 0.24f), 11f),
                CreateTerrainLayer("DryClay", palette.Clay, new Color(0.53f, 0.27f, 0.16f), 16f),
                CreateTerrainLayer("DarkRock", palette.Rock, new Color(0.19f, 0.18f, 0.17f), 18f),
                CreateTerrainLayer("TrailDirt", palette.Dirt, new Color(0.39f, 0.30f, 0.21f), 9f)
            };

            string dataPath = $"{TerrainFolder}/OrcishStarterValleyTerrain.asset";
            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (terrainData == null)
            {
                terrainData = new TerrainData();
                AssetDatabase.CreateAsset(terrainData, dataPath);
            }

            terrainData.heightmapResolution = 257;
            terrainData.alphamapResolution = 256;
            terrainData.baseMapResolution = 512;
            terrainData.SetDetailResolution(512, 16);
            terrainData.size = new Vector3(TerrainSize, TerrainHeight, TerrainSize);
            terrainData.terrainLayers = terrainLayers;
            terrainData.SetHeights(0, 0, GenerateHeights(terrainData.heightmapResolution));
            terrainData.SetAlphamaps(0, 0, GenerateAlphamaps(terrainData.alphamapResolution, terrainLayers.Length));
            EditorUtility.SetDirty(terrainData);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "Orcish Starter Valley Terrain";
            terrainObject.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
            terrainObject.isStatic = true;

            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = false;
            return terrain;
        }

        private static TerrainLayer CreateTerrainLayer(string name, Material sourceMaterial, Color textureColor, float tileSize)
        {
            string texturePath = $"{TextureFolder}/{name}.png";
            Texture2D texture = CreateColorTexture(texturePath, textureColor);
            string layerPath = $"{TerrainFolder}/{name}.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            layer.diffuseTexture = texture;
            layer.tileSize = Vector2.one * tileSize;
            layer.smoothness = 0.08f;
            layer.metallic = 0f;
            if (sourceMaterial != null && sourceMaterial.HasProperty("_BaseColor"))
            {
                layer.diffuseRemapMax = sourceMaterial.GetColor("_BaseColor");
            }

            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Texture2D CreateColorTexture(string assetPath, Color color)
        {
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Texture2D texture = new(8, 8, TextureFormat.RGBA32, true);
            Color[] pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                float noise = UnityEngine.Random.Range(-0.035f, 0.035f);
                pixels[i] = new Color(
                    Mathf.Clamp01(color.r + noise),
                    Mathf.Clamp01(color.g + noise),
                    Mathf.Clamp01(color.b + noise),
                    1f);
            }

            texture.SetPixels(pixels);
            texture.Apply();

            File.WriteAllBytes(AssetPathToFullPath(assetPath), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath);

            if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        private static float[,] GenerateHeights(int resolution)
        {
            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 world = HeightmapToWorld(x, z, resolution);
                    heights[z, x] = CalculateNormalizedHeight(world);
                }
            }

            return heights;
        }

        private static float[,,] GenerateAlphamaps(int resolution, int layerCount)
        {
            float[,,] maps = new float[resolution, resolution, layerCount];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 world = HeightmapToWorld(x, z, resolution);
                    float path = Mathf.InverseLerp(19f, 2f, DistanceToStarterPath(world));
                    float rock = Mathf.InverseLerp(0.08f, 0.17f, CalculateNormalizedHeight(world));
                    float grass = Mathf.Clamp01(0.35f + Mathf.PerlinNoise(world.x * 0.016f + 2.1f, world.y * 0.016f + 7.4f) * 0.35f);
                    float clay = Mathf.Clamp01(0.45f + Mathf.PerlinNoise(world.x * 0.012f - 5.2f, world.y * 0.012f + 1.8f) * 0.25f);

                    maps[z, x, 0] = grass * (1f - path) * (1f - rock * 0.55f);
                    maps[z, x, 1] = clay * (1f - path);
                    maps[z, x, 2] = rock * (1f - path * 0.65f);
                    maps[z, x, 3] = path;

                    float sum = 0f;
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        sum += maps[z, x, layer];
                    }

                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        maps[z, x, layer] /= sum;
                    }
                }
            }

            return maps;
        }

        private static Vector2 HeightmapToWorld(int x, int z, int resolution)
        {
            float normalizedX = x / (float)(resolution - 1);
            float normalizedZ = z / (float)(resolution - 1);
            return new Vector2((normalizedX - 0.5f) * TerrainSize, (normalizedZ - 0.5f) * TerrainSize);
        }

        private static float CalculateNormalizedHeight(Vector2 world)
        {
            float radial = world.magnitude / (TerrainSize * 0.5f);
            float broadNoise = Mathf.PerlinNoise(world.x * 0.008f + 10.3f, world.y * 0.008f - 3.7f);
            float fineNoise = Mathf.PerlinNoise(world.x * 0.025f - 8.1f, world.y * 0.025f + 4.2f);
            float ridge = Mathf.SmoothStep(0.52f, 1f, radial) * 0.16f;
            float height = 0.035f + broadNoise * 0.045f + fineNoise * 0.015f + ridge;

            height = Flatten(height, world, new Vector2(-42f, -178f), 46f, 0.035f, 0.86f);
            height = Flatten(height, world, new Vector2(-34f, -118f), 62f, 0.042f, 0.78f);
            height = Flatten(height, world, new Vector2(82f, -48f), 54f, 0.05f, 0.65f);
            height = Flatten(height, world, new Vector2(-132f, 128f), 42f, 0.065f, 0.58f);
            return Mathf.Clamp01(height);
        }

        private static float Flatten(float currentHeight, Vector2 world, Vector2 center, float radius, float targetHeight, float strength)
        {
            float factor = 1f - Mathf.SmoothStep(0f, radius, Vector2.Distance(world, center));
            return Mathf.Lerp(currentHeight, targetHeight, factor * strength);
        }

        private static float DistanceToStarterPath(Vector2 world)
        {
            float distance = float.MaxValue;
            distance = Mathf.Min(distance, DistanceToSegment(world, new Vector2(-42f, -178f), new Vector2(-34f, -118f)));
            distance = Mathf.Min(distance, DistanceToSegment(world, new Vector2(-34f, -118f), new Vector2(82f, -48f)));
            distance = Mathf.Min(distance, DistanceToSegment(world, new Vector2(82f, -48f), new Vector2(145f, 72f)));
            distance = Mathf.Min(distance, DistanceToSegment(world, new Vector2(-34f, -118f), new Vector2(-132f, 128f)));
            return distance;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 segment = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, a + segment * t);
        }

        private static void BuildLighting()
        {
            GameObject lightObject = new("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.35f;
            light.color = new Color(1f, 0.86f, 0.68f);
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.sun = light;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.54f, 0.46f, 0.39f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 520f;

            GameObject volumeObject = new("Global Volume");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/DefaultVolumeProfile.asset");
        }

        private static void BuildWorldDressing(Terrain terrain, WorldPalette palette, Transform worldRoot, Transform markerRoot)
        {
            CreateMarker(markerRoot, terrain, "PlayerSpawn", WorldRegionMarkerType.PlayerSpawn, new Vector2(-42f, -178f), 8f);
            CreateMarker(markerRoot, terrain, "RazorcragCamp", WorldRegionMarkerType.QuestHub, new Vector2(-34f, -118f), 34f);
            CreateMarker(markerRoot, terrain, "TrainingRing", WorldRegionMarkerType.QuestObjective, new Vector2(-72f, -132f), 16f);
            CreateMarker(markerRoot, terrain, "BristlebackField", WorldRegionMarkerType.CombatArea, new Vector2(82f, -48f), 42f);
            CreateMarker(markerRoot, terrain, "AshCanyon", WorldRegionMarkerType.CombatArea, new Vector2(145f, 72f), 48f);
            CreateMarker(markerRoot, terrain, "SmolderingCave", WorldRegionMarkerType.Landmark, new Vector2(-132f, 128f), 28f);

            GameObject camp = new("Razorcrag Camp");
            camp.transform.SetParent(worldRoot);
            Vector2 campCenter = new(-34f, -118f);
            for (int i = 0; i < 24; i++)
            {
                float angle = i / 24f * Mathf.PI * 2f;
                if (Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, 20f)) < 28f)
                {
                    continue;
                }

                Vector2 p = campCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 36f;
                CreatePrimitive(terrain, PrimitiveType.Cylinder, $"Palisade Post {i:00}", p, 1.7f, Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f), new Vector3(0.22f, 1.7f, 0.22f), palette.Wood, camp.transform);
            }

            CreatePrimitive(terrain, PrimitiveType.Cube, "Command Hide Tent", campCenter + new Vector2(-8f, 5f), 1.1f, Quaternion.Euler(0f, 24f, 0f), new Vector3(7f, 1.1f, 4.6f), palette.Hide, camp.transform);
            CreatePrimitive(terrain, PrimitiveType.Cube, "Supplies Shelter", campCenter + new Vector2(10f, -8f), 0.8f, Quaternion.Euler(0f, -18f, 0f), new Vector3(5f, 0.8f, 3.8f), palette.Hide, camp.transform);
            CreateCapsule(terrain, "Quest Giver - Warchief Placeholder", campCenter + new Vector2(-4f, -2f), palette.Friendly, camp.transform);
            CreateCapsule(terrain, "Quest Giver - Scout Placeholder", campCenter + new Vector2(9f, 5f), palette.Friendly, camp.transform);

            GameObject training = new("Training Ring");
            training.transform.SetParent(worldRoot);
            for (int i = 0; i < 6; i++)
            {
                float angle = i / 6f * Mathf.PI * 2f;
                Vector2 p = new Vector2(-72f, -132f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 10f;
                CreatePrimitive(terrain, PrimitiveType.Cylinder, $"Training Dummy {i + 1}", p, 1.2f, Quaternion.identity, new Vector3(0.38f, 1.2f, 0.38f), palette.Wood, training.transform);
            }

            GameObject creatures = new("Placeholder Creature Spawns");
            creatures.transform.SetParent(worldRoot);
            for (int i = 0; i < 10; i++)
            {
                Vector2 p = new Vector2(82f, -48f) + UnityEngine.Random.insideUnitCircle * 34f;
                CreateCapsule(terrain, $"Bristleback Creature Placeholder {i + 1:00}", p, palette.Hostile, creatures.transform);
            }

            for (int i = 0; i < 8; i++)
            {
                Vector2 p = new Vector2(145f, 72f) + UnityEngine.Random.insideUnitCircle * 40f;
                CreateCapsule(terrain, $"Ash Canyon Creature Placeholder {i + 1:00}", p, palette.Hostile, creatures.transform);
            }

            GameObject landmarks = new("Landmarks And Rocks");
            landmarks.transform.SetParent(worldRoot);
            for (int i = 0; i < 46; i++)
            {
                Vector2 p = UnityEngine.Random.insideUnitCircle * 230f;
                if (DistanceToStarterPath(p) < 10f)
                {
                    continue;
                }

                float size = UnityEngine.Random.Range(0.7f, 2.8f);
                CreatePrimitive(terrain, PrimitiveType.Sphere, $"Basalt Rock {i:00}", p, size * 0.45f, UnityEngine.Random.rotation, new Vector3(size * 1.25f, size * 0.55f, size), palette.Rock, landmarks.transform);
            }

            CreatePrimitive(terrain, PrimitiveType.Cube, "Smoldering Cave Mouth", new Vector2(-132f, 128f), 3.1f, Quaternion.Euler(0f, 34f, 0f), new Vector3(9f, 3.1f, 2.2f), palette.Rock, landmarks.transform);
            CreatePrimitive(terrain, PrimitiveType.Cylinder, "Central Banner Pole", campCenter + new Vector2(0f, 0f), 3.6f, Quaternion.identity, new Vector3(0.16f, 3.6f, 0.16f), palette.Wood, camp.transform);
        }

        private static void CreateMarker(Transform parent, Terrain terrain, string id, WorldRegionMarkerType type, Vector2 point, float radius)
        {
            GameObject marker = new(id);
            marker.transform.SetParent(parent);
            marker.transform.position = GroundPosition(terrain, point);
            marker.AddComponent<WorldRegionMarker>().Configure(type, id, radius);
        }

        private static GameObject InstantiatePlayer(Terrain terrain, WorldPalette palette, MMOPlayerMovementConfig movementConfig)
        {
            GameObject prefab = BuildPlayerPrefab(palette, movementConfig);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Unable to instantiate player prefab.");
            }

            instance.name = "Player";
            Vector2 spawnPoint = new(-42f, -178f);
            instance.transform.SetPositionAndRotation(GroundPosition(terrain, spawnPoint) + Vector3.up * 1.05f, Quaternion.Euler(0f, 18f, 0f));
            return instance;
        }

        private static GameObject BuildPlayerPrefab(WorldPalette palette, MMOPlayerMovementConfig movementConfig)
        {
            string prefabPath = $"{PrefabFolder}/PlayerCapsule.prefab";
            GameObject root = new("PlayerCapsule");
            root.tag = "Player";
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.radius = 0.36f;
            controller.height = 2f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.42f;

            root.AddComponent<MMOInputReader>();
            MMOPlayerMotor motor = root.AddComponent<MMOPlayerMotor>();
            SetObjectReference(motor, "movementConfig", movementConfig);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Capsule Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(0.72f, 1f, 0.72f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.GetComponent<Renderer>().sharedMaterial = palette.Player;

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return saved;
        }

        private static Camera CreateGameplayCamera(GameObject player, MMOThirdPersonCameraConfig cameraConfig)
        {
            GameObject cameraObject = new("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = player.transform.position + new Vector3(0f, 4.5f, -8f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 650f;
            cameraObject.AddComponent<AudioListener>();
            MMOThirdPersonCamera controller = cameraObject.AddComponent<MMOThirdPersonCamera>();
            SetObjectReference(controller, "cameraConfig", cameraConfig);
            return camera;
        }

        private static void WirePlayerCamera(GameObject player, Camera camera)
        {
            MMOInputReader inputReader = player.GetComponent<MMOInputReader>();
            MMOThirdPersonCamera cameraController = camera.GetComponent<MMOThirdPersonCamera>();
            cameraController.SetTarget(player.transform, inputReader);
            SetObjectReference(player.GetComponent<MMOPlayerMotor>(), "cameraController", cameraController);
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(camera);
        }

        private static void CreateNavigationSurface(Transform worldRoot)
        {
            GameObject navigation = new("Navigation");
            navigation.transform.SetParent(worldRoot);
            NavMeshSurface surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        }

        private static void CreateCapsule(Terrain terrain, string name, Vector2 point, Material material, Transform parent)
        {
            CreatePrimitive(terrain, PrimitiveType.Capsule, name, point, 1f, Quaternion.identity, new Vector3(0.8f, 1f, 0.8f), material, parent);
        }

        private static GameObject CreatePrimitive(Terrain terrain, PrimitiveType primitiveType, string name, Vector2 point, float centerHeight, Quaternion rotation, Vector3 scale, Material material, Transform parent)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent);
            primitive.transform.position = GroundPosition(terrain, point) + Vector3.up * centerHeight;
            primitive.transform.rotation = rotation;
            primitive.transform.localScale = scale;
            primitive.isStatic = true;

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static Vector3 GroundPosition(Terrain terrain, Vector2 point)
        {
            Vector3 position = new(point.x, 0f, point.y);
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            return position;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"Missing serialized property {propertyName} on {target.name}.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string scenePath)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene scene in scenes)
            {
                if (scene.path == scenePath)
                {
                    scene.enabled = true;
                    return;
                }
            }

            Array.Resize(ref scenes, scenes.Length + 1);
            scenes[^1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = scenes;
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }

        private static string AssetPathToFullPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath["Assets/".Length..]).Replace('\\', '/');
        }

        private sealed class WorldPalette
        {
            public Material Grass;
            public Material Clay;
            public Material Dirt;
            public Material Rock;
            public Material Wood;
            public Material Hide;
            public Material Player;
            public Material Friendly;
            public Material Hostile;
            public Material Marker;
        }
    }
}
