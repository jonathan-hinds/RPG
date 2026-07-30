using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.Vfx.Shaman
{
    [DisallowMultipleComponent]
    public sealed class EmpowerWeaponPersistentVFX : MonoBehaviour
    {
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int EmissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int BoundsMinId = Shader.PropertyToID("_BoundsMin");
        private static readonly int BoundsSizeId = Shader.PropertyToID("_BoundsSize");
        private static readonly int FlowAxisId = Shader.PropertyToID("_FlowAxis");
        private static readonly int PatternScaleId = Shader.PropertyToID("_PatternScale");
        private static readonly int PulseSpeedId = Shader.PropertyToID("_PulseSpeed");
        private static readonly int RuneIntensityId = Shader.PropertyToID("_RuneIntensity");
        private static readonly int FlowIntensityId = Shader.PropertyToID("_FlowIntensity");
        private static readonly int TravelSpeedId = Shader.PropertyToID("_TravelSpeed");
        private static readonly int EdgeBrightnessId = Shader.PropertyToID("_EdgeBrightness");
        private static readonly int SurfaceExtrusionId = Shader.PropertyToID("_SurfaceExtrusion");

        [SerializeField] private EmpowerWeaponVFXProfile profile;
        [SerializeField] private Material surfaceOverlayMaterial;
        [SerializeField] private Transform surfaceOverlayRoot;
        [SerializeField] private Transform auraRoot;
        [SerializeField] private ParticleSystem auraParticles;
        [SerializeField] private ParticleSystem motesParticles;
        [SerializeField] private ParticleSystem arcParticles;
        [SerializeField] private TrailRenderer broadTrail;
        [SerializeField] private TrailRenderer highlightTrail;

        private readonly List<Renderer> overlayRenderers = new();
        private readonly List<GameObject> overlayObjects = new();
        private MaterialPropertyBlock propertyBlock;
        private MMOEquipmentVisualInstanceMarker weaponMarker;
        private float fadeStartedAt;
        private float currentIntensity;
        private float targetIntensity = 1f;
        private float trailStopsAt;
        private bool fadingOut;

        public bool FadeComplete => fadingOut && currentIntensity <= 0.001f;
        public MMOEquipmentVisualInstanceMarker WeaponMarker => weaponMarker;

        private void LateUpdate()
        {
            if (profile == null)
            {
                return;
            }

            float duration = fadingOut ? profile.FadeOutDuration : profile.FadeInDuration;
            float progress = Mathf.Clamp01((Time.time - fadeStartedAt) / Mathf.Max(0.01f, duration));
            float destination = fadingOut ? 0f : targetIntensity;
            currentIntensity = Mathf.Lerp(currentIntensity, destination, Smooth01(progress));
            if (progress >= 1f)
            {
                currentIntensity = destination;
            }

            ApplyIntensity(currentIntensity);

            bool trailActive = !fadingOut && Time.time < trailStopsAt;
            if (broadTrail != null) broadTrail.emitting = trailActive;
            if (highlightTrail != null) highlightTrail.emitting = trailActive;
        }

        private void OnDestroy()
        {
            for (int i = overlayObjects.Count - 1; i >= 0; i--)
            {
                if (overlayObjects[i] != null)
                {
                    Destroy(overlayObjects[i]);
                }
            }

            overlayObjects.Clear();
            overlayRenderers.Clear();
        }

        public void Attach(MMOEquipmentVisualInstanceMarker marker)
        {
            weaponMarker = marker;
            if (marker == null || profile == null)
            {
                return;
            }

            transform.SetParent(marker.transform, false);
            BuildSurfaceOverlays(marker);
            FitToWeapon(marker);
            targetIntensity = marker.PresentationState == MMOEquipmentAttachmentPresentationState.Stowed
                ? profile.SheathedIntensity
                : 1f;
            currentIntensity = 0f;
            fadeStartedAt = Time.time;
            fadingOut = false;
            SetEmission(true);
            ApplyIntensity(0f);
        }

        public void TriggerAttackTrail()
        {
            if (fadingOut || profile == null)
            {
                return;
            }

            trailStopsAt = Time.time + profile.AttackTrailDuration;
            broadTrail?.Clear();
            highlightTrail?.Clear();
            motesParticles?.Emit(2);
        }

        public void FadeOut()
        {
            if (fadingOut)
            {
                return;
            }

            fadingOut = true;
            fadeStartedAt = Time.time;
            trailStopsAt = float.NegativeInfinity;
            SetEmission(false);
        }

        public void ConfigureAuthoring(
            EmpowerWeaponVFXProfile newProfile,
            Material newSurfaceOverlayMaterial,
            Transform newSurfaceOverlayRoot,
            Transform newAuraRoot,
            ParticleSystem newAuraParticles,
            ParticleSystem newMotesParticles,
            ParticleSystem newArcParticles,
            TrailRenderer newBroadTrail,
            TrailRenderer newHighlightTrail)
        {
            profile = newProfile;
            surfaceOverlayMaterial = newSurfaceOverlayMaterial;
            surfaceOverlayRoot = newSurfaceOverlayRoot;
            auraRoot = newAuraRoot;
            auraParticles = newAuraParticles;
            motesParticles = newMotesParticles;
            arcParticles = newArcParticles;
            broadTrail = newBroadTrail;
            highlightTrail = newHighlightTrail;
        }

        private void FitToWeapon(MMOEquipmentVisualInstanceMarker marker)
        {
            Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
            bool initialized = false;
            Vector3 localMin = Vector3.zero;
            Vector3 localMax = Vector3.zero;
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer is ParticleSystemRenderer || renderer is TrailRenderer
                    || renderer.transform.IsChildOf(transform))
                {
                    continue;
                }

                Bounds bounds = renderer.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                {
                    Vector3 world = new(
                        x == 0 ? min.x : max.x,
                        y == 0 ? min.y : max.y,
                        z == 0 ? min.z : max.z);
                    Vector3 local = marker.transform.InverseTransformPoint(world);
                    if (!initialized)
                    {
                        localMin = localMax = local;
                        initialized = true;
                    }
                    else
                    {
                        localMin = Vector3.Min(localMin, local);
                        localMax = Vector3.Max(localMax, local);
                    }
                }
            }

            Vector3 size = initialized ? localMax - localMin : new Vector3(0.2f, 1f, 0.2f);
            Vector3 center = initialized ? (localMin + localMax) * 0.5f : Vector3.zero;
            int axisIndex = size.y >= size.x && size.y >= size.z ? 1 : size.x >= size.z ? 0 : 2;
            Vector3 axis = axisIndex == 0 ? Vector3.right : axisIndex == 1 ? Vector3.up : Vector3.forward;
            float length = Mathf.Max(0.35f, size[axisIndex]);
            float crossA = size[(axisIndex + 1) % 3];
            float crossB = size[(axisIndex + 2) % 3];
            float radius = Mathf.Max(profile.AuraWidth, Mathf.Max(crossA, crossB) * 0.62f);

            if (auraRoot != null)
            {
                auraRoot.localPosition = center;
                auraRoot.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
                auraRoot.localScale = new Vector3(radius, length, radius);
            }

            Vector3 endpointA = center + axis * length * 0.5f;
            Vector3 endpointB = center - axis * length * 0.5f;
            Vector3 trailPoint = endpointA.sqrMagnitude >= endpointB.sqrMagnitude ? endpointA : endpointB;
            if (broadTrail != null)
            {
                broadTrail.transform.localPosition = trailPoint;
                broadTrail.widthMultiplier = profile.TrailWidth;
            }

            if (highlightTrail != null)
            {
                highlightTrail.transform.localPosition = trailPoint;
                highlightTrail.widthMultiplier = profile.TrailWidth * 0.28f;
            }
        }

        private void BuildSurfaceOverlays(MMOEquipmentVisualInstanceMarker marker)
        {
            if (surfaceOverlayMaterial == null || surfaceOverlayRoot == null)
            {
                return;
            }

            foreach (MeshRenderer source in marker.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (source == null || source.transform.IsChildOf(transform))
                {
                    continue;
                }

                MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null)
                {
                    continue;
                }

                GameObject overlayObject = new($"{source.name} Energy Overlay");
                overlayObject.transform.SetParent(source.transform, false);
                MeshFilter filter = overlayObject.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer overlay = overlayObject.AddComponent<MeshRenderer>();
                overlay.sharedMaterial = surfaceOverlayMaterial;
                ConfigureOverlayRenderer(overlay);
                ConfigureSurfaceMapping(overlay, sourceFilter.sharedMesh.bounds);
                overlayObjects.Add(overlayObject);
                overlayRenderers.Add(overlay);
            }

            foreach (SkinnedMeshRenderer source in marker.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (source == null || source.sharedMesh == null || source.transform.IsChildOf(transform))
                {
                    continue;
                }

                GameObject overlayObject = new($"{source.name} Energy Overlay");
                overlayObject.transform.SetParent(source.transform.parent, false);
                overlayObject.transform.localPosition = source.transform.localPosition;
                overlayObject.transform.localRotation = source.transform.localRotation;
                overlayObject.transform.localScale = source.transform.localScale;
                SkinnedMeshRenderer overlay = overlayObject.AddComponent<SkinnedMeshRenderer>();
                overlay.sharedMesh = source.sharedMesh;
                overlay.bones = source.bones;
                overlay.rootBone = source.rootBone;
                overlay.sharedMaterial = surfaceOverlayMaterial;
                ConfigureOverlayRenderer(overlay);
                ConfigureSurfaceMapping(overlay, source.sharedMesh.bounds);
                overlayObjects.Add(overlayObject);
                overlayRenderers.Add(overlay);
            }
        }

        private static void ConfigureOverlayRenderer(Renderer overlay)
        {
            overlay.shadowCastingMode = ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            overlay.lightProbeUsage = LightProbeUsage.Off;
            overlay.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private void ConfigureSurfaceMapping(Renderer overlay, Bounds localBounds)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            Vector3 size = localBounds.size;
            int axisIndex = size.y >= size.x && size.y >= size.z ? 1 : size.x >= size.z ? 0 : 2;
            Vector3 flowAxis = axisIndex == 0 ? Vector3.right : axisIndex == 1 ? Vector3.up : Vector3.forward;
            overlay.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(BoundsMinId, localBounds.min);
            propertyBlock.SetVector(
                BoundsSizeId,
                new Vector4(
                    Mathf.Max(0.0001f, size.x),
                    Mathf.Max(0.0001f, size.y),
                    Mathf.Max(0.0001f, size.z),
                    0f));
            propertyBlock.SetVector(FlowAxisId, flowAxis);
            overlay.SetPropertyBlock(propertyBlock);
        }

        private void SetEmission(bool enabled)
        {
            SetParticleEmission(auraParticles, enabled);
            SetParticleEmission(motesParticles, enabled);
            SetParticleEmission(arcParticles, enabled);
            if (enabled)
            {
                auraParticles?.Play(true);
                motesParticles?.Play(true);
                arcParticles?.Play(true);
            }
        }

        private static void SetParticleEmission(ParticleSystem system, bool enabled)
        {
            if (system == null) return;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = enabled;
            if (!enabled) system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void ApplyIntensity(float intensity)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            float scaledEmission = profile != null ? profile.WeaponEmissionIntensity * intensity : intensity;
            foreach (Renderer renderer in overlayRenderers)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(OpacityId, intensity * 0.86f);
                propertyBlock.SetFloat(EmissionIntensityId, scaledEmission);
                propertyBlock.SetFloat(PatternScaleId, profile.SurfacePatternScale);
                propertyBlock.SetFloat(PulseSpeedId, profile.PulseSpeed);
                propertyBlock.SetFloat(RuneIntensityId, profile.RuneIntensity);
                propertyBlock.SetFloat(FlowIntensityId, profile.SurfaceFlowIntensity);
                propertyBlock.SetFloat(TravelSpeedId, profile.TravellingPulseSpeed);
                propertyBlock.SetFloat(EdgeBrightnessId, profile.EdgeCoronaIntensity);
                propertyBlock.SetFloat(SurfaceExtrusionId, profile.SurfaceLift);
                renderer.SetPropertyBlock(propertyBlock);
            }

            if (profile == null) return;
            SetParticleRate(auraParticles, profile.ParticleAmount * 0.05f * intensity);
            SetParticleRate(motesParticles, profile.ParticleAmount * 0.12f * intensity);
            SetParticleRate(arcParticles, profile.ArcFrequency * 0.55f * intensity);
            SetRendererProperties(
                auraParticles != null ? auraParticles.GetComponent<ParticleSystemRenderer>() : null,
                profile.AuraIntensity,
                intensity);
            SetRendererProperties(
                motesParticles != null ? motesParticles.GetComponent<ParticleSystemRenderer>() : null,
                profile.AuraIntensity * 0.72f,
                intensity);
            SetRendererProperties(
                arcParticles != null ? arcParticles.GetComponent<ParticleSystemRenderer>() : null,
                profile.ArcBrightness,
                intensity);
            SetRendererProperties(broadTrail, profile.TrailIntensity, intensity);
            SetRendererProperties(highlightTrail, profile.TrailIntensity * 1.2f, intensity);
        }

        private void SetRendererProperties(Renderer renderer, float brightness, float opacity)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(BrightnessId, brightness);
            propertyBlock.SetFloat(OpacityId, opacity);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static void SetParticleRate(ParticleSystem system, float rate)
        {
            if (system == null) return;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = Mathf.Max(0f, rate);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
