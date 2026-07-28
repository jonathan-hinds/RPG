using RPGClone.Characters;
using RPGClone.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.Targeting
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MMOTargetSelectionController))]
    public sealed class MMOSelectionIndicatorPresenter : MonoBehaviour
    {
        private const string DefaultStyleResourcePath = "RPGClone/Targeting/DefaultSelectionIndicatorStyle";
        private const string GroundRootName = "Selected Target Ground Indicator";
        private const string ReticleRootName = "Selected Target Reticle";

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");
        private static readonly int RadialOrbId = Shader.PropertyToID("_RadialOrb");

        [SerializeField] private MMOTargetSelectionController targetSelectionController;
        [SerializeField] private MMOSelectionIndicatorStyle style;

        private MaterialPropertyBlock propertyBlock;
        private MMOCharacterIdentity target;
        private Camera selectionCamera;
        private GameObject groundRoot;
        private GameObject reticleRoot;
        private Transform groundGlowTransform;
        private Transform groundCoreTransform;
        private Transform reticleOrbTransform;
        private Transform reticleGlowTransform;
        private Transform reticleCoreTransform;
        private MeshRenderer groundGlowRenderer;
        private MeshRenderer groundCoreRenderer;
        private MeshRenderer reticleOrbRenderer;
        private MeshRenderer reticleGlowRenderer;
        private MeshRenderer reticleCoreRenderer;
        private Material groundMaterial;
        private Material reticleOrbMaterial;
        private Material reticleMaterial;
        private Mesh groundMesh;
        private Mesh billboardMesh;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            ResolveDependencies();
            CreateVisualsIfNeeded();
            SetVisible(false);
        }

        private void OnEnable()
        {
            ResolveDependencies();
            if (targetSelectionController == null)
            {
                return;
            }

            targetSelectionController.TargetChanged -= OnTargetChanged;
            targetSelectionController.TargetChanged += OnTargetChanged;
            OnTargetChanged(targetSelectionController.CurrentTarget);
        }

        private void OnDisable()
        {
            if (targetSelectionController != null)
            {
                targetSelectionController.TargetChanged -= OnTargetChanged;
            }

            UnsubscribeFromTarget();
            target = null;
            SetVisible(false);
        }

        private void OnDestroy()
        {
            UnsubscribeFromTarget();
            DestroyRuntimeObject(groundRoot);
            DestroyRuntimeObject(reticleRoot);
            DestroyRuntimeObject(groundMaterial);
            DestroyRuntimeObject(reticleOrbMaterial);
            DestroyRuntimeObject(reticleMaterial);
            DestroyRuntimeObject(groundMesh);
            DestroyRuntimeObject(billboardMesh);
        }

        private void LateUpdate()
        {
            if (!ShouldShow())
            {
                SetVisible(false);
                return;
            }

            CreateVisualsIfNeeded();
            if (groundRoot == null || reticleRoot == null)
            {
                return;
            }

            Bounds bounds = ResolveTargetBounds(target);
            UpdateGroundIndicator(bounds);
            UpdateFloatingReticle(bounds);
            SetVisible(true);
        }

        private void OnTargetChanged(MMOCharacterIdentity newTarget)
        {
            if (target == newTarget)
            {
                return;
            }

            UnsubscribeFromTarget();
            target = newTarget;
            if (target != null)
            {
                target.Changed += OnTargetIdentityChanged;
            }

            AttachVisualsToTarget();
            RefreshTint();
            SetVisible(ShouldShow());
        }

        private void OnTargetIdentityChanged(MMOCharacterIdentity changedTarget)
        {
            if (changedTarget != target)
            {
                return;
            }

            RefreshTint();
            SetVisible(ShouldShow());
        }

        private void ResolveDependencies()
        {
            targetSelectionController ??= GetComponent<MMOTargetSelectionController>();
            style ??= Resources.Load<MMOSelectionIndicatorStyle>(DefaultStyleResourcePath);
            selectionCamera = MMORuntimeSceneReferences.MainCamera;
        }

        private void CreateVisualsIfNeeded()
        {
            if (groundRoot != null || style == null || style.IndicatorShader == null)
            {
                return;
            }

            groundMesh = CreateQuadMesh("Selection Indicator Ground Mesh", groundPlane: true);
            billboardMesh = CreateQuadMesh("Selection Indicator Billboard Mesh", groundPlane: false);
            groundMaterial = CreateMaterial(
                "Selection Indicator Ground Material",
                style.GroundMask,
                CompareFunction.LessEqual,
                (int)RenderQueue.Transparent + 20);
            reticleMaterial = CreateMaterial(
                "Selection Indicator Reticle Material",
                style.ReticleMask,
                CompareFunction.Always,
                (int)RenderQueue.Overlay);
            reticleOrbMaterial = CreateMaterial(
                "Selection Indicator White Orb Material",
                Texture2D.whiteTexture,
                CompareFunction.Always,
                (int)RenderQueue.Overlay - 1);
            reticleOrbMaterial.SetFloat(RadialOrbId, 1f);

            groundRoot = CreateRoot(GroundRootName);
            groundGlowRenderer = CreateLayer(
                groundRoot.transform,
                "Ground Glow",
                groundMesh,
                groundMaterial,
                out groundGlowTransform,
                10);
            groundCoreRenderer = CreateLayer(
                groundRoot.transform,
                "Ground Core",
                groundMesh,
                groundMaterial,
                out groundCoreTransform,
                11);

            reticleRoot = CreateRoot(ReticleRootName);
            reticleOrbRenderer = CreateLayer(
                reticleRoot.transform,
                "Reticle White Orb",
                billboardMesh,
                reticleOrbMaterial,
                out reticleOrbTransform,
                19);
            reticleGlowRenderer = CreateLayer(
                reticleRoot.transform,
                "Reticle Glow",
                billboardMesh,
                reticleMaterial,
                out reticleGlowTransform,
                20);
            reticleCoreRenderer = CreateLayer(
                reticleRoot.transform,
                "Reticle Core",
                billboardMesh,
                reticleMaterial,
                out reticleCoreTransform,
                21);
            SetLayerRecursively(reticleRoot, ResolveReticleLayer());

            AttachVisualsToTarget();
            RefreshTint();
        }

        private Material CreateMaterial(
            string materialName,
            Texture texture,
            CompareFunction depthTest,
            int renderQueue)
        {
            Material material = new(style.IndicatorShader)
            {
                name = materialName,
                hideFlags = HideFlags.DontSave,
                renderQueue = renderQueue
            };
            material.SetTexture(BaseMapId, texture);
            material.SetFloat(ZTestId, (float)depthTest);
            return material;
        }

        private static GameObject CreateRoot(string objectName)
        {
            return new GameObject(objectName)
            {
                hideFlags = HideFlags.DontSave
            };
        }

        private static MeshRenderer CreateLayer(
            Transform parent,
            string objectName,
            Mesh mesh,
            Material material,
            out Transform layerTransform,
            int sortingOrder)
        {
            GameObject layer = new(objectName)
            {
                hideFlags = HideFlags.DontSave
            };
            layerTransform = layer.transform;
            layerTransform.SetParent(parent, false);

            MeshFilter filter = layer.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = layer.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private void AttachVisualsToTarget()
        {
            Transform parent = target != null ? target.transform : transform;
            if (groundRoot != null)
            {
                groundRoot.transform.SetParent(parent, false);
            }

            if (reticleRoot != null)
            {
                reticleRoot.transform.SetParent(parent, false);
            }
        }

        private void UpdateGroundIndicator(Bounds bounds)
        {
            float radius = ResolveGroundRadius(bounds);
            Vector3 groundWorldPosition = new(bounds.center.x, bounds.min.y + style.GroundOffset, bounds.center.z);
            groundRoot.transform.localPosition = target.transform.InverseTransformPoint(groundWorldPosition);
            groundRoot.transform.localRotation = Quaternion.identity;
            groundRoot.transform.localScale = Vector3.one;

            float inverseScaleX = SafeInverse(target.transform.lossyScale.x);
            float inverseScaleZ = SafeInverse(target.transform.lossyScale.z);
            float diameter = radius * 2f;
            float rotation = Time.time * style.GroundRotationDegreesPerSecond;

            groundGlowTransform.localScale = new Vector3(
                diameter * 1.08f * inverseScaleX,
                1f,
                diameter * 1.08f * inverseScaleZ);
            groundGlowTransform.localRotation = Quaternion.Euler(0f, -rotation * 0.42f, 0f);

            groundCoreTransform.localScale = new Vector3(
                diameter * inverseScaleX,
                1f,
                diameter * inverseScaleZ);
            groundCoreTransform.localRotation = Quaternion.Euler(0f, rotation, 0f);
        }

        private void UpdateFloatingReticle(Bounds bounds)
        {
            if (selectionCamera == null)
            {
                selectionCamera = MMORuntimeSceneReferences.MainCamera;
            }

            Vector3 worldPosition = ResolveReticleAnchor(target, bounds);
            if (selectionCamera != null)
            {
                Vector3 cameraOffset = -selectionCamera.transform.forward * style.ReticleCameraOffset;
                cameraOffset.y = 0f;
                worldPosition += cameraOffset;
                reticleRoot.transform.rotation = selectionCamera.transform.rotation;
            }

            reticleRoot.transform.position = worldPosition;
            float pulse = 1f + Mathf.Sin(Time.time * Mathf.PI * 2f * style.PulseFrequency)
                * style.PulseScaleAmount;
            Vector3 inverseParentScale = new(
                SafeInverse(target.transform.lossyScale.x),
                SafeInverse(target.transform.lossyScale.y),
                SafeInverse(target.transform.lossyScale.z));
            Vector3 baseScale = inverseParentScale * (style.ReticleWorldSize * pulse);
            float rotation = Time.time * style.ReticleRotationDegreesPerSecond;

            reticleOrbTransform.localScale = baseScale * style.OrbScale;
            reticleOrbTransform.localRotation = Quaternion.identity;
            reticleGlowTransform.localScale = baseScale * 1.18f;
            reticleGlowTransform.localRotation = Quaternion.Euler(0f, 0f, -rotation * 0.35f);
            reticleCoreTransform.localScale = baseScale;
            reticleCoreTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        }

        private void RefreshTint()
        {
            if (style == null || target == null)
            {
                return;
            }

            Color tint = ResolveTargetColor(target);
            ApplyLayerProperties(groundGlowRenderer, tint, style.GroundGlowOpacity, 1.7f);
            ApplyLayerProperties(groundCoreRenderer, tint, style.GroundCoreOpacity, 1.2f);
            ApplyLayerProperties(reticleOrbRenderer, style.OrbColor, style.OrbOpacity, style.OrbIntensity);
            ApplyLayerProperties(reticleGlowRenderer, tint, style.ReticleGlowOpacity, 2.25f);
            ApplyLayerProperties(reticleCoreRenderer, tint, style.ReticleCoreOpacity, 1.65f);
        }

        private void ApplyLayerProperties(MeshRenderer renderer, Color tint, float opacity, float intensity)
        {
            if (renderer == null)
            {
                return;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(OpacityId, opacity);
            propertyBlock.SetFloat(IntensityId, intensity);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private Color ResolveTargetColor(MMOCharacterIdentity selectedTarget)
        {
            if (MMOGameplaySessionService.Players.Contains(selectedTarget))
            {
                return style.PlayerColor;
            }

            MMOCharacterIdentity localPlayer = MMOGameplaySessionService.LocalPlayer.Identity;
            bool isHostile = localPlayer != null
                ? MMOFactionRules.CanDamage(localPlayer, selectedTarget)
                : selectedTarget.Faction == MMOEntityFaction.Hostile;
            return isHostile ? style.HostileColor : style.NpcColor;
        }

        private bool ShouldShow()
        {
            return style != null
                && target != null
                && target.isActiveAndEnabled
                && target.Selectable
                && (!style.HideForLocalPlayer || MMOGameplaySessionService.LocalPlayer.Identity != target);
        }

        private void SetVisible(bool visible)
        {
            if (groundRoot != null && groundRoot.activeSelf != visible)
            {
                groundRoot.SetActive(visible);
            }

            if (reticleRoot != null && reticleRoot.activeSelf != visible)
            {
                reticleRoot.SetActive(visible);
            }
        }

        private void UnsubscribeFromTarget()
        {
            if (target != null)
            {
                target.Changed -= OnTargetIdentityChanged;
            }
        }

        private float ResolveGroundRadius(Bounds bounds)
        {
            float boundsRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) * style.BoundsRadiusMultiplier;
            return Mathf.Max(style.MinimumGroundRadius, boundsRadius);
        }

        private static Bounds ResolveTargetBounds(MMOCharacterIdentity selectedTarget)
        {
            Collider[] colliders = selectedTarget.GetComponentsInChildren<Collider>();
            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Collider collider in colliders)
            {
                if (collider == null || collider.isTrigger || !collider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds
                ? bounds
                : new Bounds(selectedTarget.transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        }

        private static Vector3 ResolveReticleAnchor(
            MMOCharacterIdentity selectedTarget,
            Bounds fallbackBounds)
        {
            if (TryResolveColliderBounds<CapsuleCollider>(selectedTarget, out Bounds capsuleBounds))
            {
                return capsuleBounds.center;
            }

            if (TryResolveColliderBounds<CharacterController>(selectedTarget, out Bounds controllerBounds))
            {
                return controllerBounds.center;
            }

            return fallbackBounds.center;
        }

        private static bool TryResolveColliderBounds<TCollider>(
            MMOCharacterIdentity selectedTarget,
            out Bounds combinedBounds)
            where TCollider : Collider
        {
            TCollider[] colliders = selectedTarget.GetComponentsInChildren<TCollider>();
            bool hasBounds = false;
            combinedBounds = default;
            foreach (TCollider collider in colliders)
            {
                if (collider == null || collider.isTrigger || !collider.enabled)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combinedBounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    combinedBounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private static Mesh CreateQuadMesh(string meshName, bool groundPlane)
        {
            Vector3[] vertices = groundPlane
                ? new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f),
                    new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, -0.5f)
                }
                : new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f),
                    new Vector3(0.5f, 0.5f, 0f),
                    new Vector3(0.5f, -0.5f, 0f)
                };

            Mesh mesh = new()
            {
                name = meshName,
                hideFlags = HideFlags.DontSave,
                vertices = vertices,
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(1f, 1f),
                    new Vector2(1f, 0f)
                },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float SafeInverse(float value)
        {
            return 1f / Mathf.Max(0.0001f, Mathf.Abs(value));
        }

        private int ResolveReticleLayer()
        {
            int layer = LayerMask.NameToLayer(style.ReticleLayerName);
            return layer >= 0 ? layer : gameObject.layer;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
            {
                return;
            }

            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void DestroyRuntimeObject(Object runtimeObject)
        {
            if (runtimeObject != null)
            {
                Destroy(runtimeObject);
            }
        }
    }
}
