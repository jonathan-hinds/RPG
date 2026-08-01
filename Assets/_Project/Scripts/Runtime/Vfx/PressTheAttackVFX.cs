using System;
using System.Collections.Generic;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.Vfx
{
    [DisallowMultipleComponent]
    public sealed class PressTheAttackVFX : MonoBehaviour, IMMOAbilityVfxInstance, IMMOAbilityVfxPoolReset
    {
        private const string PressTheAttackAbilityId = "warrior_press_the_attack";

        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int MainTintId = Shader.PropertyToID("_MainTint");
        private static readonly int DarkTintId = Shader.PropertyToID("_DarkTint");
        private static readonly int HighlightTintId = Shader.PropertyToID("_HighlightTint");
        private static readonly int MovementResponseId = Shader.PropertyToID("_MovementResponse");
        private static readonly int AttackResponseId = Shader.PropertyToID("_AttackResponse");
        private static readonly int FinalInstabilityId = Shader.PropertyToID("_FinalInstability");
        private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");
        private static readonly int LightningFrequencyId = Shader.PropertyToID("_LightningFrequency");
        private static readonly int LightningThicknessId = Shader.PropertyToID("_LightningThickness");
        private static readonly int LightningSpeedId = Shader.PropertyToID("_LightningSpeed");
        private static readonly int LightningDistortionId = Shader.PropertyToID("_LightningDistortion");
        private static readonly int EdgeGlowWidthId = Shader.PropertyToID("_EdgeGlowWidth");
        private static readonly int EdgeGlowIntensityId = Shader.PropertyToID("_EdgeGlowIntensity");
        private static readonly int SurfaceStreakSpeedId = Shader.PropertyToID("_SurfaceStreakSpeed");
        private static readonly int SurfaceLiftId = Shader.PropertyToID("_SurfaceLift");
        private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int TravelSpeedId = Shader.PropertyToID("_TravelSpeed");
        private static readonly int UndercoatIntensityId = Shader.PropertyToID("_UndercoatIntensity");
        private static readonly int BoundsMinId = Shader.PropertyToID("_BoundsMin");
        private static readonly int BoundsSizeId = Shader.PropertyToID("_BoundsSize");
        private static readonly int FlowAxisId = Shader.PropertyToID("_FlowAxis");
        private static readonly int ProjectionWorldToLocalId = Shader.PropertyToID("_ProjectionWorldToLocal");

        [Header("Configuration")]
        [SerializeField] private PressTheAttackVFXProfile profile;
        [SerializeField] private Material rageOverlayMaterial;
        [SerializeField] private Material lightningOverlayMaterial;
        [SerializeField] private Material edgeStreakOverlayMaterial;

        [Header("Hierarchy")]
        [SerializeField] private Transform dynamicOverlayRoot;
        [SerializeField] private Transform groundRoot;
        [SerializeField] private Renderer broadGroundRing;
        [SerializeField] private Renderer thinGroundRing;
        [SerializeField] private Renderer groundImpactMarks;

        [Header("Activation Flipbooks")]
        [SerializeField] private ParticleSystem activationRageBurst;
        [SerializeField] private ParticleSystem electricalSnap;
        [SerializeField] private ParticleSystem bodyEnergyPulse;
        [SerializeField] private ParticleSystem crimsonImpactFlash;
        [SerializeField] private ParticleSystem groundShockBurst;
        [SerializeField] private ParticleSystem redVapor;
        [SerializeField] private ParticleSystem fastStreakBurst;
        [SerializeField] private Light activationLight;

        [Header("Persistent Accents")]
        [SerializeField] private ParticleSystem surfaceSparks;
        [SerializeField] private ParticleSystem movementStreaks;
        [SerializeField] private ParticleSystem attackAccent;
        [SerializeField] private ParticleSystem confirmedHitAccent;
        [SerializeField] private bool activationOnly;

        private readonly List<SurfaceOverlayLayer> overlayLayers = new();
        private readonly List<GameObject> overlayObjects = new();
        private MaterialPropertyBlock propertyBlock;
        private MMOAbilityVfxContext context;
        private Transform caster;
        private MMOCharacterBuffController buffController;
        private MMOAbilitySystem abilitySystem;
        private MMOCombatant combatant;
        private MMOPlayerEquipmentVisuals equipmentVisuals;
        private MMOActiveBuff activeBuff;
        private Vector3 groundWorldPosition;
        private Vector3 previousCasterPosition;
        private float initializedAt;
        private float fadeStartedAt;
        private float currentIntensity;
        private float attackResponse;
        private bool buffObserved;
        private bool fadingOut;
        private bool initialized;
        private bool releaseRequested;
        private Bounds projectionBoundsLocal;

        public bool ReadyForPool => releaseRequested;

        private readonly struct SurfaceOverlayLayer
        {
            public SurfaceOverlayLayer(Renderer renderer, float liftMultiplier, float emissionMultiplier, float opacityMultiplier)
            {
                Renderer = renderer;
                LiftMultiplier = liftMultiplier;
                EmissionMultiplier = emissionMultiplier;
                OpacityMultiplier = opacityMultiplier;
            }

            public Renderer Renderer { get; }
            public float LiftMultiplier { get; }
            public float EmissionMultiplier { get; }
            public float OpacityMultiplier { get; }
        }

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            HideAuthoredLayers();
            StopAndClearAllParticles();
            if (activationLight != null)
            {
                activationLight.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (!initialized || profile == null || caster == null)
            {
                return;
            }

            float elapsed = Time.time - initializedAt;
            UpdateActivation(elapsed);

            if (activationOnly)
            {
                if (!fadingOut && elapsed >= profile.ActivationDuration)
                {
                    BeginFadeOut();
                }
            }
            else
            {
                SynchronizeAuthoritativeBuff();
            }

            UpdateResponseSignals();
            UpdatePersistentPresentation(elapsed);

            if (fadingOut && Time.time - fadeStartedAt >= profile.FadeOutDuration)
            {
                RequestRelease();
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            ClearSurfaceOverlays();
            StopAndClearAllParticles();
            initialized = false;
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            ResetState(false);
            context = newContext;
            caster = newContext.Source;
            if (caster == null || profile == null)
            {
                RequestRelease();
                return;
            }

            transform.SetParent(caster, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            groundWorldPosition = caster.position + Vector3.up * 0.035f;
            previousCasterPosition = caster.position;
            initializedAt = Time.time;
            fadeStartedAt = initializedAt;
            initialized = true;

            buffController = caster.GetComponent<MMOCharacterBuffController>();
            abilitySystem = caster.GetComponent<MMOAbilitySystem>();
            combatant = caster.GetComponent<MMOCombatant>();
            equipmentVisuals = caster.GetComponent<MMOPlayerEquipmentVisuals>();
            Subscribe();
            BuildSurfaceOverlays();
            FitPersistentParticlesToCharacter();
            PlayActivationParticles();

            activeBuff = buffController != null ? buffController.FindBuff(PressTheAttackAbilityId) : null;
            buffObserved = activeBuff != null;
            if (buffObserved && !activationOnly)
            {
                StartPersistentEmission();
            }
        }

        public void ResetForPool()
        {
            ResetState(true);
        }

        public void ConfigureAuthoring(
            PressTheAttackVFXProfile newProfile,
            Material newRageOverlayMaterial,
            Material newLightningOverlayMaterial,
            Material newEdgeStreakOverlayMaterial,
            Transform newDynamicOverlayRoot,
            Transform newGroundRoot,
            Renderer newBroadGroundRing,
            Renderer newThinGroundRing,
            Renderer newGroundImpactMarks,
            ParticleSystem newActivationRageBurst,
            ParticleSystem newElectricalSnap,
            ParticleSystem newBodyEnergyPulse,
            ParticleSystem newCrimsonImpactFlash,
            ParticleSystem newGroundShockBurst,
            ParticleSystem newRedVapor,
            ParticleSystem newFastStreakBurst,
            Light newActivationLight,
            ParticleSystem newSurfaceSparks,
            ParticleSystem newMovementStreaks,
            ParticleSystem newAttackAccent,
            ParticleSystem newConfirmedHitAccent)
        {
            profile = newProfile;
            rageOverlayMaterial = newRageOverlayMaterial;
            lightningOverlayMaterial = newLightningOverlayMaterial;
            edgeStreakOverlayMaterial = newEdgeStreakOverlayMaterial;
            dynamicOverlayRoot = newDynamicOverlayRoot;
            groundRoot = newGroundRoot;
            broadGroundRing = newBroadGroundRing;
            thinGroundRing = newThinGroundRing;
            groundImpactMarks = newGroundImpactMarks;
            activationRageBurst = newActivationRageBurst;
            electricalSnap = newElectricalSnap;
            bodyEnergyPulse = newBodyEnergyPulse;
            crimsonImpactFlash = newCrimsonImpactFlash;
            groundShockBurst = newGroundShockBurst;
            redVapor = newRedVapor;
            fastStreakBurst = newFastStreakBurst;
            activationLight = newActivationLight;
            surfaceSparks = newSurfaceSparks;
            movementStreaks = newMovementStreaks;
            attackAccent = newAttackAccent;
            confirmedHitAccent = newConfirmedHitAccent;
        }

        public void ConfigureActivationOnly(bool value)
        {
            activationOnly = value;
        }

        private void SynchronizeAuthoritativeBuff()
        {
            MMOActiveBuff observed = buffController != null
                ? buffController.FindBuff(PressTheAttackAbilityId)
                : null;
            if (observed != null)
            {
                activeBuff = observed;
                if (!buffObserved)
                {
                    buffObserved = true;
                    StartPersistentEmission();
                }

                return;
            }

            activeBuff = null;
            if (buffObserved)
            {
                BeginFadeOut();
            }
            else if (!fadingOut && Time.time - initializedAt >= profile.AuthoritativeBuffHandshakeTimeout)
            {
                // The short timeout only cleans up an unmatched presentation event; it never controls buff duration.
                BeginFadeOut();
            }
        }

        private void UpdateActivation(float elapsed)
        {
            float duration = Mathf.Max(0.01f, profile.ActivationDuration);
            float progress = Mathf.Clamp01(elapsed / duration);
            float impact = 1f - Smooth01(Mathf.InverseLerp(0.08f, 0.72f, progress));
            if (groundRoot != null)
            {
                groundRoot.SetPositionAndRotation(groundWorldPosition, Quaternion.Euler(90f, 0f, 0f));
            }

            SetActivationRenderer(broadGroundRing, Mathf.Lerp(0.25f, 3.7f, Smooth01(progress)), impact * 0.72f);
            SetActivationRenderer(thinGroundRing, Mathf.Lerp(0.18f, 3.15f, Smooth01(Mathf.Clamp01(progress * 1.2f))), impact);
            SetActivationRenderer(groundImpactMarks, Mathf.Lerp(0.35f, 2.6f, Smooth01(progress)), impact * (1f - progress));

            if (activationLight != null)
            {
                float lightProgress = elapsed / Mathf.Max(0.01f, profile.OptionalLightDuration);
                activationLight.enabled = lightProgress < 1f && profile.OptionalLightIntensity > 0f;
                activationLight.intensity = profile.OptionalLightIntensity * Mathf.Pow(1f - Mathf.Clamp01(lightProgress), 2f);
            }
        }

        private void UpdateResponseSignals()
        {
            float deltaTime = Mathf.Max(0.0001f, Time.deltaTime);
            Vector3 position = caster.position;
            Vector3 delta = position - previousCasterPosition;
            delta.y = 0f;
            float speed = delta.magnitude / deltaTime;
            previousCasterPosition = position;
            float movement = Mathf.Clamp01(speed / Mathf.Max(0.01f, profile.MovementSpeedForFullResponse));
            attackResponse = Mathf.MoveTowards(
                attackResponse,
                0f,
                deltaTime / Mathf.Max(0.01f, profile.AttackResponseDuration));

            float finalInstability = activeBuff != null && activeBuff.RemainingSeconds <= 1f
                ? 1f - activeBuff.RemainingSeconds
                : 0f;

            if (fadingOut)
            {
                currentIntensity = 1f - Mathf.Clamp01((Time.time - fadeStartedAt) / Mathf.Max(0.01f, profile.FadeOutDuration));
            }
            else if (buffObserved || activationOnly)
            {
                currentIntensity = Mathf.Clamp01((Time.time - initializedAt) / Mathf.Max(0.01f, profile.FadeInDuration));
            }

            float reveal = Mathf.Clamp01((Time.time - initializedAt - 0.12f) / 0.18f);
            float activationBoost = 1f - Mathf.Clamp01((Time.time - initializedAt) / 0.42f);
            ApplyOverlayProperties(
                currentIntensity,
                movement * profile.MovementResponse,
                attackResponse * profile.AttackResponse,
                finalInstability * profile.FinalSecondInstability,
                reveal,
                activationBoost);
            UpdateParticleRates(currentIntensity, movement, finalInstability);
        }

        private void UpdatePersistentPresentation(float elapsed)
        {
            if (dynamicOverlayRoot != null)
            {
                dynamicOverlayRoot.localRotation = Quaternion.Euler(0f, Mathf.Sin(elapsed * 1.9f) * 0.35f, 0f);
            }
        }

        private void ApplyOverlayProperties(
            float intensity,
            float movement,
            float attack,
            float finalInstability,
            float reveal,
            float activationBoost)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            float emission = profile.PersistentOverlayIntensity * intensity
                + profile.ActivationIntensity * activationBoost;
            foreach (SurfaceOverlayLayer layer in overlayLayers)
            {
                Renderer renderer = layer.Renderer;
                if (renderer == null)
                {
                    continue;
                }

                renderer.enabled = intensity > 0.001f || activationBoost > 0.001f;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(MainTintId, profile.MainCrimson);
                propertyBlock.SetColor(DarkTintId, profile.DarkRed);
                propertyBlock.SetColor(HighlightTintId, profile.Highlight);
                propertyBlock.SetFloat(
                    OpacityId,
                    Mathf.Clamp01((intensity + activationBoost * 0.82f) * layer.OpacityMultiplier));
                propertyBlock.SetFloat(EmissionIntensityId, emission * layer.EmissionMultiplier);
                propertyBlock.SetFloat(MovementResponseId, movement);
                propertyBlock.SetFloat(AttackResponseId, attack);
                propertyBlock.SetFloat(FinalInstabilityId, finalInstability);
                propertyBlock.SetFloat(RevealProgressId, reveal);
                propertyBlock.SetFloat(LightningFrequencyId, profile.LightningFrequency);
                propertyBlock.SetFloat(LightningThicknessId, profile.LightningThickness);
                propertyBlock.SetFloat(LightningSpeedId, profile.LightningSpeed);
                propertyBlock.SetFloat(LightningDistortionId, profile.LightningDistortion);
                propertyBlock.SetFloat(EdgeGlowWidthId, profile.EdgeGlowWidth);
                propertyBlock.SetFloat(EdgeGlowIntensityId, profile.EdgeGlowIntensity);
                propertyBlock.SetFloat(SurfaceStreakSpeedId, profile.SurfaceStreakSpeed);
                propertyBlock.SetFloat(SurfaceLiftId, profile.SurfaceLift * layer.LiftMultiplier);
                propertyBlock.SetFloat(PatternScaleId, profile.SurfacePatternScale);
                propertyBlock.SetFloat(PulseSpeedId, profile.SurfacePulseSpeed);
                propertyBlock.SetFloat(TravelSpeedId, profile.TravellingPulseSpeed);
                propertyBlock.SetFloat(UndercoatIntensityId, profile.RageUndercoatIntensity);
                propertyBlock.SetMatrix(ProjectionWorldToLocalId, caster.worldToLocalMatrix);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private void BuildSurfaceOverlays()
        {
            ClearSurfaceOverlays();
            if (caster == null)
            {
                return;
            }

            List<MeshRenderer> meshSources = new();
            List<SkinnedMeshRenderer> skinnedSources = new();
            List<Renderer> projectionSources = new();
            foreach (Renderer source in caster.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsEligibleSourceRenderer(source))
                {
                    continue;
                }

                switch (source)
                {
                    case MeshRenderer meshRenderer when meshRenderer.GetComponent<MeshFilter>()?.sharedMesh != null:
                        meshSources.Add(meshRenderer);
                        projectionSources.Add(meshRenderer);
                        break;
                    case SkinnedMeshRenderer skinnedRenderer when skinnedRenderer.sharedMesh != null:
                        skinnedSources.Add(skinnedRenderer);
                        projectionSources.Add(skinnedRenderer);
                        break;
                }
            }

            projectionBoundsLocal = CalculateProjectionBounds(projectionSources);

            foreach (MeshRenderer source in meshSources)
            {
                Mesh mesh = source.GetComponent<MeshFilter>().sharedMesh;

                CreateMeshOverlay(source, mesh, rageOverlayMaterial, "Charged Rage Surface Overlay", 1f, 0.52f, 0.62f);
                CreateMeshOverlay(source, mesh, lightningOverlayMaterial, "Crawling Red Electricity Overlay", 1.85f, 1.12f, 1f);
                CreateMeshOverlay(source, mesh, edgeStreakOverlayMaterial, "Rage Silhouette Overlay", 2.7f, 0.36f, 0.2f);
            }

            foreach (SkinnedMeshRenderer source in skinnedSources)
            {
                CreateSkinnedOverlay(source, rageOverlayMaterial, "Charged Rage Surface Overlay", 1f, 0.52f, 0.62f);
                CreateSkinnedOverlay(source, lightningOverlayMaterial, "Crawling Red Electricity Overlay", 1.85f, 1.12f, 1f);
                CreateSkinnedOverlay(source, edgeStreakOverlayMaterial, "Rage Silhouette Overlay", 2.7f, 0.36f, 0.2f);
            }
        }

        private void CreateMeshOverlay(
            MeshRenderer source,
            Mesh mesh,
            Material material,
            string suffix,
            float liftMultiplier,
            float emissionMultiplier,
            float opacityMultiplier)
        {
            if (material == null)
            {
                return;
            }

            GameObject overlayObject = new($"{source.name} {suffix}");
            overlayObject.AddComponent<PressTheAttackSurfaceOverlayMarker>();
            overlayObject.transform.SetParent(source.transform, false);
            MeshFilter filter = overlayObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer overlay = overlayObject.AddComponent<MeshRenderer>();
            overlay.sharedMaterials = RepeatMaterial(material, ResolveSubmeshCount(mesh, source.sharedMaterials.Length));
            ConfigureOverlayRenderer(overlay);
            overlayObjects.Add(overlayObject);
            overlayLayers.Add(new SurfaceOverlayLayer(overlay, liftMultiplier, emissionMultiplier, opacityMultiplier));
        }

        private void CreateSkinnedOverlay(
            SkinnedMeshRenderer source,
            Material material,
            string suffix,
            float liftMultiplier,
            float emissionMultiplier,
            float opacityMultiplier)
        {
            if (material == null)
            {
                return;
            }

            GameObject overlayObject = new($"{source.name} {suffix}");
            overlayObject.AddComponent<PressTheAttackSurfaceOverlayMarker>();
            overlayObject.transform.SetParent(source.transform.parent, false);
            overlayObject.transform.localPosition = source.transform.localPosition;
            overlayObject.transform.localRotation = source.transform.localRotation;
            overlayObject.transform.localScale = source.transform.localScale;
            SkinnedMeshRenderer overlay = overlayObject.AddComponent<SkinnedMeshRenderer>();
            overlay.sharedMesh = source.sharedMesh;
            overlay.bones = source.bones;
            overlay.rootBone = source.rootBone;
            overlay.localBounds = source.localBounds;
            overlay.updateWhenOffscreen = source.updateWhenOffscreen;
            overlay.sharedMaterials = RepeatMaterial(
                material,
                ResolveSubmeshCount(source.sharedMesh, source.sharedMaterials.Length));
            ConfigureOverlayRenderer(overlay);
            overlayObjects.Add(overlayObject);
            overlayLayers.Add(new SurfaceOverlayLayer(overlay, liftMultiplier, emissionMultiplier, opacityMultiplier));
        }

        private void ConfigureOverlayRenderer(Renderer overlay)
        {
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.reflectionProbeUsage = ReflectionProbeUsage.Off;
            overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            overlay.allowOcclusionWhenDynamic = true;
            propertyBlock ??= new MaterialPropertyBlock();
            overlay.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(BoundsMinId, projectionBoundsLocal.min);
            propertyBlock.SetVector(
                BoundsSizeId,
                new Vector4(
                    Mathf.Max(0.0001f, projectionBoundsLocal.size.x),
                    Mathf.Max(0.0001f, projectionBoundsLocal.size.y),
                    Mathf.Max(0.0001f, projectionBoundsLocal.size.z),
                    0f));
            Vector3 size = projectionBoundsLocal.size;
            Vector3 flowAxis = size.y >= size.x && size.y >= size.z
                ? Vector3.up
                : size.x >= size.z ? Vector3.right : Vector3.forward;
            propertyBlock.SetVector(FlowAxisId, flowAxis);
            propertyBlock.SetMatrix(ProjectionWorldToLocalId, caster.worldToLocalMatrix);
            overlay.SetPropertyBlock(propertyBlock);
        }

        private Bounds CalculateProjectionBounds(IReadOnlyList<Renderer> sources)
        {
            bool initializedBounds = false;
            Bounds result = default;
            for (int i = 0; i < sources.Count; i++)
            {
                Bounds worldBounds = sources[i].bounds;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3 worldCorner = new(
                        x == 0 ? worldBounds.min.x : worldBounds.max.x,
                        y == 0 ? worldBounds.min.y : worldBounds.max.y,
                        z == 0 ? worldBounds.min.z : worldBounds.max.z);
                    Vector3 casterLocalCorner = caster.InverseTransformPoint(worldCorner);
                    if (!initializedBounds)
                    {
                        result = new Bounds(casterLocalCorner, Vector3.zero);
                        initializedBounds = true;
                    }
                    else
                    {
                        result.Encapsulate(casterLocalCorner);
                    }
                }
            }

            if (!initializedBounds)
            {
                return new Bounds(new Vector3(0f, 1f, 0f), new Vector3(0.8f, 2f, 0.8f));
            }

            Vector3 padding = Vector3.Max(result.size * 0.025f, Vector3.one * 0.015f);
            result.Expand(padding * 2f);
            return result;
        }

        private bool IsEligibleSourceRenderer(Renderer source)
        {
            if (source == null || !source.enabled || source.forceRenderingOff || !source.gameObject.activeInHierarchy
                || source is ParticleSystemRenderer or TrailRenderer or LineRenderer or SpriteRenderer
                || source.transform.IsChildOf(transform)
                || source.GetComponent<PressTheAttackSurfaceOverlayMarker>() != null
                || source.GetComponentInParent<Canvas>() != null)
            {
                return false;
            }

            foreach (MonoBehaviour owner in source.GetComponentsInParent<MonoBehaviour>(true))
            {
                if (owner != null && owner != this && owner is IMMOAbilityVfxInstance)
                {
                    return false;
                }
            }

            foreach (Material material in source.sharedMaterials)
            {
                if (IsPresentationMaterial(material))
                {
                    return false;
                }
            }

            string sourceName = source.name;
            if (sourceName.Contains("VFX", StringComparison.OrdinalIgnoreCase)
                || sourceName.Contains("Nameplate", StringComparison.OrdinalIgnoreCase)
                || sourceName.Contains("Shadow", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            MMOEquipmentVisualInstanceMarker equipmentMarker = source.GetComponentInParent<MMOEquipmentVisualInstanceMarker>();
            return equipmentMarker == null
                || equipmentMarker.EquipmentSlot is not (MMOEquipmentSlotType.MainHand
                    or MMOEquipmentSlotType.OffHand
                    or MMOEquipmentSlotType.Ranged);
        }

        private static bool IsPresentationMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            Shader shader = material.shader;
            string shaderName = shader != null ? shader.name : string.Empty;
            int renderQueue = material.renderQueue >= 0
                ? material.renderQueue
                : shader != null ? shader.renderQueue : (int)RenderQueue.Geometry;
            string renderType = material.GetTag("RenderType", false, string.Empty);
            return renderQueue > (int)RenderQueue.AlphaTest
                || renderType.Contains("Transparent", StringComparison.OrdinalIgnoreCase)
                || shaderName.Contains("/VFX/", StringComparison.OrdinalIgnoreCase)
                || shaderName.Contains("Particle", StringComparison.OrdinalIgnoreCase);
        }

        private void FitPersistentParticlesToCharacter()
        {
            if (caster == null)
            {
                return;
            }

            bool initializedBounds = false;
            Bounds localBounds = default;
            foreach (Renderer source in caster.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsEligibleSourceRenderer(source))
                {
                    continue;
                }

                Bounds worldBounds = source.bounds;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3 world = new(
                        x == 0 ? worldBounds.min.x : worldBounds.max.x,
                        y == 0 ? worldBounds.min.y : worldBounds.max.y,
                        z == 0 ? worldBounds.min.z : worldBounds.max.z);
                    Vector3 local = caster.InverseTransformPoint(world);
                    if (!initializedBounds)
                    {
                        localBounds = new Bounds(local, Vector3.zero);
                        initializedBounds = true;
                    }
                    else
                    {
                        localBounds.Encapsulate(local);
                    }
                }
            }

            if (!initializedBounds)
            {
                localBounds = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(0.8f, 1.8f, 0.55f));
            }

            ConfigureParticleBox(surfaceSparks, localBounds.center, localBounds.size * 0.92f);
            Bounds movementBounds = localBounds;
            movementBounds.center = new Vector3(localBounds.center.x, localBounds.min.y + localBounds.size.y * 0.28f, localBounds.center.z);
            movementBounds.size = new Vector3(localBounds.size.x, localBounds.size.y * 0.48f, localBounds.size.z);
            ConfigureParticleBox(movementStreaks, movementBounds.center, movementBounds.size);
        }

        private void BeginFadeOut()
        {
            if (fadingOut)
            {
                return;
            }

            fadingOut = true;
            fadeStartedAt = Time.time;
            StopPersistentEmission();
        }

        private void StartPersistentEmission()
        {
            if (activationOnly || fadingOut)
            {
                return;
            }

            StartParticle(surfaceSparks);
            StartParticle(movementStreaks);
        }

        private void StopPersistentEmission()
        {
            StopParticleEmission(surfaceSparks);
            StopParticleEmission(movementStreaks);
        }

        private void UpdateParticleRates(float intensity, float movement, float finalInstability)
        {
            if (profile == null)
            {
                return;
            }

            SetParticleRate(surfaceSparks, profile.SurfaceSparkAmount * intensity * (0.55f + finalInstability * 0.45f));
            SetParticleRate(movementStreaks, profile.MovementStreakAmount * intensity * movement);
        }

        private void OnAbilityReleased(
            MMOAbilitySystem source,
            MMOAbilityDefinition ability,
            MMOCharacterIdentity target,
            Vector3 targetPosition,
            bool hasGroundTarget)
        {
            if (!initialized || fadingOut || ability == null || ability.AbilityId == PressTheAttackAbilityId)
            {
                return;
            }

            attackResponse = 1f;
            attackAccent?.Emit(Mathf.Max(1, profile.AttackAccentAmount));
            surfaceSparks?.Emit(2);
        }

        private void OnCombatEventResolved(
            CombatEventRecord record,
            MMOCombatant source,
            MMOCombatant target,
            MMOAbilityDefinition ability)
        {
            if (!initialized || fadingOut || record == null || source != combatant
                || record.eventType != CombatEventType.DamageResolved)
            {
                return;
            }

            attackResponse = 1f;
            if (confirmedHitAccent != null && target != null)
            {
                confirmedHitAccent.transform.position = target.transform.TransformPoint(new Vector3(0f, 1.05f, 0f));
                confirmedHitAccent.Emit(1);
            }
        }

        private void OnBuffsChanged(MMOCharacterBuffController source)
        {
            if (source == buffController && initialized && !activationOnly)
            {
                SynchronizeAuthoritativeBuff();
            }
        }

        private void OnEquipmentVisualsRebuilt(MMOPlayerEquipmentVisuals source)
        {
            if (source == equipmentVisuals && initialized && !fadingOut)
            {
                BuildSurfaceOverlays();
                FitPersistentParticlesToCharacter();
            }
        }

        private void OnCasterDied(MMOCombatant source)
        {
            if (source == combatant)
            {
                BeginFadeOut();
            }
        }

        private void Subscribe()
        {
            if (buffController != null)
            {
                buffController.BuffsChanged -= OnBuffsChanged;
                buffController.BuffsChanged += OnBuffsChanged;
            }

            if (abilitySystem != null)
            {
                abilitySystem.AbilityReleased -= OnAbilityReleased;
                abilitySystem.AbilityReleased += OnAbilityReleased;
            }

            if (equipmentVisuals != null)
            {
                equipmentVisuals.VisualsRebuilt -= OnEquipmentVisualsRebuilt;
                equipmentVisuals.VisualsRebuilt += OnEquipmentVisualsRebuilt;
            }

            if (combatant != null)
            {
                combatant.Died -= OnCasterDied;
                combatant.Died += OnCasterDied;
            }

            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            MMOCombatEventStream.CombatEventResolved += OnCombatEventResolved;
        }

        private void Unsubscribe()
        {
            if (buffController != null) buffController.BuffsChanged -= OnBuffsChanged;
            if (abilitySystem != null) abilitySystem.AbilityReleased -= OnAbilityReleased;
            if (equipmentVisuals != null) equipmentVisuals.VisualsRebuilt -= OnEquipmentVisualsRebuilt;
            if (combatant != null) combatant.Died -= OnCasterDied;
            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
        }

        private void PlayActivationParticles()
        {
            PlayParticle(activationRageBurst);
            PlayParticle(electricalSnap);
            PlayParticle(bodyEnergyPulse);
            PlayParticle(crimsonImpactFlash);
            PlayParticle(groundShockBurst);
            PlayParticle(redVapor);
            PlayParticle(fastStreakBurst);
            if (activationLight != null)
            {
                activationLight.color = profile.MainCrimson;
                activationLight.enabled = profile.OptionalLightIntensity > 0f;
                activationLight.intensity = profile.OptionalLightIntensity;
            }
        }

        private void RequestRelease()
        {
            if (releaseRequested)
            {
                return;
            }

            releaseRequested = true;
            MMOAbilityVfxPool.Release(gameObject);
        }

        private void ResetState(bool clearContext)
        {
            Unsubscribe();
            ClearSurfaceOverlays();
            StopAndClearAllParticles();
            HideAuthoredLayers();
            if (activationLight != null)
            {
                activationLight.enabled = false;
                activationLight.intensity = 0f;
            }

            activeBuff = null;
            buffController = null;
            abilitySystem = null;
            combatant = null;
            equipmentVisuals = null;
            caster = null;
            initializedAt = 0f;
            fadeStartedAt = 0f;
            currentIntensity = 0f;
            attackResponse = 0f;
            buffObserved = false;
            fadingOut = false;
            initialized = false;
            releaseRequested = false;
            if (clearContext)
            {
                context = default;
            }
        }

        private void ClearSurfaceOverlays()
        {
            for (int i = overlayObjects.Count - 1; i >= 0; i--)
            {
                GameObject overlayObject = overlayObjects[i];
                if (overlayObject == null)
                {
                    continue;
                }

                Renderer renderer = overlayObject.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }

                if (Application.isPlaying) Destroy(overlayObject);
                else DestroyImmediate(overlayObject);
            }

            overlayObjects.Clear();
            overlayLayers.Clear();
        }

        private void StopAndClearAllParticles()
        {
            foreach (ParticleSystem particles in EnumerateParticles())
            {
                particles?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private IEnumerable<ParticleSystem> EnumerateParticles()
        {
            yield return activationRageBurst;
            yield return electricalSnap;
            yield return bodyEnergyPulse;
            yield return crimsonImpactFlash;
            yield return groundShockBurst;
            yield return redVapor;
            yield return fastStreakBurst;
            yield return surfaceSparks;
            yield return movementStreaks;
            yield return attackAccent;
            yield return confirmedHitAccent;
        }

        private void HideAuthoredLayers()
        {
            SetActivationRenderer(broadGroundRing, 0f, 0f);
            SetActivationRenderer(thinGroundRing, 0f, 0f);
            SetActivationRenderer(groundImpactMarks, 0f, 0f);
        }

        private void SetActivationRenderer(Renderer renderer, float scale, float opacity)
        {
            if (renderer == null)
            {
                return;
            }

            renderer.transform.localScale = Vector3.one * scale;
            renderer.enabled = opacity > 0.001f;
            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OpacityId, opacity);
            propertyBlock.SetFloat(EmissionIntensityId, profile != null ? profile.ActivationIntensity : 1f);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static Material[] RepeatMaterial(Material material, int count)
        {
            Material[] materials = new Material[Mathf.Max(1, count)];
            Array.Fill(materials, material);
            return materials;
        }

        private static int ResolveSubmeshCount(Mesh mesh, int sourceMaterialCount)
        {
            return Mathf.Max(1, Mathf.Max(mesh != null ? mesh.subMeshCount : 1, sourceMaterialCount));
        }

        private static void ConfigureParticleBox(ParticleSystem particles, Vector3 center, Vector3 size)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.position = center;
            shape.scale = new Vector3(
                Mathf.Max(0.05f, size.x),
                Mathf.Max(0.05f, size.y),
                Mathf.Max(0.05f, size.z));
        }

        private static void StartParticle(ParticleSystem particles)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            particles.Play(true);
        }

        private static void PlayParticle(ParticleSystem particles)
        {
            if (particles == null)
            {
                return;
            }

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }

        private static void StopParticleEmission(ParticleSystem particles)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private static void SetParticleRate(ParticleSystem particles, float rate)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }

    [DisallowMultipleComponent]
    internal sealed class PressTheAttackSurfaceOverlayMarker : MonoBehaviour
    {
    }
}
