using System;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Warrior
{
    [DisallowMultipleComponent]
    public sealed class BerzerkitisVFX : MonoBehaviour, IMMOAbilityVfxInstance, IBerzerkitisVFX
    {
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int ScrollSpeedId = Shader.PropertyToID("_ScrollSpeed");
        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int DistortionId = Shader.PropertyToID("_DistortionStrength");

        [Header("Configuration")]
        [SerializeField] private BerzerkitisVFXProfile profile;
        [SerializeField] private GameObject leftHandPrefab;
        [SerializeField] private GameObject rightHandPrefab;
        [SerializeField] private bool destroyOnComplete = true;
        [SerializeField] private bool activationOnly;

        [Header("Character-Attached Activation")]
        [SerializeField] private Transform attachedEffectRoot;
        [SerializeField] private Renderer chestFlash;
        [SerializeField] private Renderer bodyEnvelope;
        [SerializeField] private Renderer rageSilhouette;
        [SerializeField] private Renderer heatDistortion;
        [SerializeField] private ParticleSystem flameColumns;
        [SerializeField] private ParticleSystem activationEmbers;
        [SerializeField] private ParticleSystem hotSparks;
        [SerializeField] private ParticleSystem attachedSmoke;
        [SerializeField] private LineRenderer waistBand;
        [SerializeField] private LineRenderer shoulderBand;
        [SerializeField] private LineRenderer leftArmTransfer;
        [SerializeField] private LineRenderer rightArmTransfer;

        [Header("Buff Emblem")]
        [SerializeField] private Transform emblemRoot;
        [SerializeField] private Renderer emblem;
        [SerializeField] private Renderer emblemGlow;
        [SerializeField] private ParticleSystem emblemEmbers;

        [Header("World-Space Activation")]
        [SerializeField] private Transform worldEffectRoot;
        [SerializeField] private Renderer shockwave;
        [SerializeField] private ParticleSystem groundDust;
        [SerializeField] private ParticleSystem worldEmbers;
        [SerializeField] private ParticleSystem worldSmoke;

        private MaterialPropertyBlock propertyBlock;
        private MMOAbilityVfxContext context;
        private MMOCharacterBuffController buffController;
        private MMOCombatant casterCombatant;
        private MMOAbilityVfxAnchors anchors;
        private BerzerkitisHandVFX leftHand;
        private BerzerkitisHandVFX rightHand;
        private Camera gameplayCamera;
        private Vector3 activationWorldPosition;
        private float startedAt;
        private float fallbackExpiresAt;
        private float fadeStartedAt;
        private bool playing;
        private bool handsStarted;
        private bool fading;
        private bool buffWasObserved;

        public event Action<BerzerkitisVFX> Completed;

        public bool IsPlaying => playing;
        public bool ReadyForPool => !playing;
        public BerzerkitisVFXProfile Profile => profile;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            HideActivationLayers();
            StopAllParticles();
        }

        private void OnDisable()
        {
            Unsubscribe();
            playing = false;
        }

        private void LateUpdate()
        {
            if (!playing || profile == null)
            {
                return;
            }

            if (worldEffectRoot != null)
            {
                worldEffectRoot.SetPositionAndRotation(activationWorldPosition, Quaternion.identity);
            }

            float elapsed = Time.time - startedAt;
            if (!fading)
            {
                AnimateActivation(elapsed);
                if (activationOnly)
                {
                    if (elapsed >= profile.ActivationDuration + 0.4f)
                    {
                        Complete();
                    }

                    return;
                }

                if (!handsStarted && elapsed >= 0.7f)
                {
                    StartHands();
                }

                CheckBuffState();
            }
            else if (Time.time - fadeStartedAt >= profile.BuffFadeOutDuration)
            {
                Complete();
            }
        }

        public void Initialize(MMOAbilityVfxContext newContext)
        {
            context = newContext;
            Transform caster = newContext.Target != null ? newContext.Target : newContext.Source;
            if (caster == null)
            {
                Debug.LogError("BerzerkitisVFX requires a caster transform.", this);
                StopImmediate();
                return;
            }

            Transform current = transform;
            while (current.parent != null && current.parent != caster)
            {
                current = current.parent;
            }

            BerzerkitisVFX[] existing = caster.GetComponentsInChildren<BerzerkitisVFX>(true);
            foreach (BerzerkitisVFX candidate in existing)
            {
                if (candidate != null && candidate != this && candidate.IsPlaying)
                {
                    candidate.StopImmediate();
                }
            }

            anchors = caster.GetComponent<MMOAbilityVfxAnchors>();
            buffController = caster.GetComponent<MMOCharacterBuffController>();
            casterCombatant = caster.GetComponent<MMOCombatant>();
            activationWorldPosition = caster.position;
            transform.position = caster.position;
            fallbackExpiresAt = Time.time + ResolveBuffDuration(newContext.Ability);
            buffWasObserved = HasBuff();
            if (!activationOnly)
            {
                Subscribe();
            }
            Play();
        }

        public void Play()
        {
            if (profile == null)
            {
                Debug.LogError("BerzerkitisVFX requires a BerzerkitisVFXProfile.", this);
                return;
            }

            ResetRuntimeHands();
            ConfigureParticleBudgets();
            activationWorldPosition = context.Source != null ? context.Source.position : transform.position;
            if (attachedEffectRoot != null)
            {
                attachedEffectRoot.localPosition = Vector3.zero;
                attachedEffectRoot.localRotation = Quaternion.identity;
                attachedEffectRoot.localScale = Vector3.one * profile.OverallActivationScale;
            }

            if (worldEffectRoot != null)
            {
                worldEffectRoot.SetPositionAndRotation(activationWorldPosition, Quaternion.identity);
                worldEffectRoot.localScale = Vector3.one * profile.OverallActivationScale;
            }

            PlayAllParticles();
            startedAt = Time.time;
            fadeStartedAt = float.NegativeInfinity;
            handsStarted = false;
            fading = false;
            playing = true;
            AnimateActivation(0f);
        }

        public void PulseHands()
        {
            leftHand?.PulseAttack();
            rightHand?.PulseAttack();
        }

        public void FadeOut()
        {
            if (!playing || fading)
            {
                return;
            }

            fading = true;
            fadeStartedAt = Time.time;
            HideActivationLayers();
            StopAttachedParticles(false);
            leftHand?.FadeOut();
            rightHand?.FadeOut();
            Unsubscribe();
        }

        public void StopImmediate()
        {
            if (!playing && !gameObject.activeInHierarchy)
            {
                return;
            }

            playing = false;
            fading = false;
            Unsubscribe();
            StopAllParticles();
            HideActivationLayers();
            leftHand?.StopImmediate();
            rightHand?.StopImmediate();
            if (destroyOnComplete && Application.isPlaying && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        public void ResetForPool()
        {
            bool previousDestroy = destroyOnComplete;
            destroyOnComplete = false;
            StopImmediate();
            destroyOnComplete = previousDestroy;
            ResetRuntimeHands();
            context = default;
            buffController = null;
            casterCombatant = null;
            anchors = null;
        }

        public void ConfigureAuthoring(
            BerzerkitisVFXProfile newProfile,
            GameObject newLeftHandPrefab,
            GameObject newRightHandPrefab,
            bool newDestroyOnComplete,
            Transform newAttachedEffectRoot,
            Renderer newChestFlash,
            Renderer newBodyEnvelope,
            Renderer newRageSilhouette,
            Renderer newHeatDistortion,
            ParticleSystem newFlameColumns,
            ParticleSystem newActivationEmbers,
            ParticleSystem newHotSparks,
            ParticleSystem newAttachedSmoke,
            LineRenderer newWaistBand,
            LineRenderer newShoulderBand,
            LineRenderer newLeftArmTransfer,
            LineRenderer newRightArmTransfer,
            Transform newEmblemRoot,
            Renderer newEmblem,
            Renderer newEmblemGlow,
            ParticleSystem newEmblemEmbers,
            Transform newWorldEffectRoot,
            Renderer newShockwave,
            ParticleSystem newGroundDust,
            ParticleSystem newWorldEmbers,
            ParticleSystem newWorldSmoke)
        {
            profile = newProfile;
            leftHandPrefab = newLeftHandPrefab;
            rightHandPrefab = newRightHandPrefab;
            destroyOnComplete = newDestroyOnComplete;
            attachedEffectRoot = newAttachedEffectRoot;
            chestFlash = newChestFlash;
            bodyEnvelope = newBodyEnvelope;
            rageSilhouette = newRageSilhouette;
            heatDistortion = newHeatDistortion;
            flameColumns = newFlameColumns;
            activationEmbers = newActivationEmbers;
            hotSparks = newHotSparks;
            attachedSmoke = newAttachedSmoke;
            waistBand = newWaistBand;
            shoulderBand = newShoulderBand;
            leftArmTransfer = newLeftArmTransfer;
            rightArmTransfer = newRightArmTransfer;
            emblemRoot = newEmblemRoot;
            emblem = newEmblem;
            emblemGlow = newEmblemGlow;
            emblemEmbers = newEmblemEmbers;
            worldEffectRoot = newWorldEffectRoot;
            shockwave = newShockwave;
            groundDust = newGroundDust;
            worldEmbers = newWorldEmbers;
            worldSmoke = newWorldSmoke;
        }

        public void ConfigureActivationOnly(bool value)
        {
            activationOnly = value;
        }

        private void AnimateActivation(float elapsed)
        {
            float duration = Mathf.Max(0.05f, profile.ActivationDuration);
            if (elapsed >= duration)
            {
                HideActivationLayers();
                StopAttachedParticles(false);
                return;
            }

            float chestAlpha = Pulse(elapsed, 0f, 0.11f, 0.42f);
            float chestScale = Mathf.Lerp(0.18f, 1.3f, Smooth01(Mathf.Clamp01(elapsed / 0.2f)));
            SetScale(chestFlash, Vector3.one * chestScale);
            SetRenderer(chestFlash, profile.Colors.WhiteHot, chestAlpha, profile.OverallBrightness * profile.ChestFlashBrightness, Vector2.zero, 0f);

            float envelopeAlpha = Pulse(elapsed, 0.11f, 0.43f, 1.12f) * profile.RageEnvelopeOpacity;
            float contraction = elapsed < 0.72f ? 1f : Mathf.Lerp(1f, 0.56f, Smooth01(Mathf.InverseLerp(0.72f, 1.18f, elapsed)));
            SetScale(bodyEnvelope, new Vector3(profile.RageEnvelopeSize, profile.RageEnvelopeSize * 1.25f, 1f) * contraction);
            SetRenderer(bodyEnvelope, profile.Colors.DeepOrange, envelopeAlpha, profile.OverallBrightness, new Vector2(0.08f, 0.62f), Mathf.InverseLerp(0.72f, 1.18f, elapsed));

            float silhouetteAlpha = Pulse(elapsed, 0.25f, 0.5f, 0.86f) * 0.68f;
            SetScale(rageSilhouette, new Vector3(profile.RageSilhouetteScale * 1.5f, profile.RageSilhouetteScale * 2.2f, 1f));
            SetRenderer(rageSilhouette, profile.Colors.BloodRed, silhouetteAlpha, profile.OverallBrightness * 1.1f, new Vector2(-0.05f, 0.85f), Mathf.InverseLerp(0.56f, 0.9f, elapsed));

            float distortionAlpha = Pulse(elapsed, 0.12f, 0.38f, 1.05f) * 0.6f;
            SetScale(heatDistortion, new Vector3(profile.RageEnvelopeSize * 0.9f, profile.RageEnvelopeSize * 1.22f, 1f));
            SetRenderer(heatDistortion, Color.white, distortionAlpha, 1f, new Vector2(0.06f, 0.42f), 0f, profile.HeatDistortionStrength);

            AnimateBands(elapsed);
            AnimateEmblem(elapsed);
            AnimateShockwave(elapsed);
            AnimateTransfers(elapsed);
            BillboardAttachedLayers(elapsed);
        }

        private void AnimateBands(float elapsed)
        {
            float alpha = Pulse(elapsed, 0.16f, 0.4f, 0.88f);
            if (waistBand != null)
            {
                waistBand.transform.localRotation = Quaternion.Euler(0f, elapsed * 390f, 8f);
                waistBand.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.14f, Mathf.Clamp01(elapsed / 0.8f));
                SetRenderer(waistBand, profile.Colors.DeepOrange, alpha, profile.OverallBrightness, new Vector2(-1.4f, 0f), Mathf.InverseLerp(0.62f, 0.9f, elapsed));
            }

            if (shoulderBand != null)
            {
                shoulderBand.transform.localRotation = Quaternion.Euler(12f, elapsed * -460f, -7f);
                shoulderBand.transform.localScale = Vector3.one * Mathf.Lerp(0.78f, 1.08f, Mathf.Clamp01(elapsed / 0.8f));
                SetRenderer(shoulderBand, profile.Colors.BloodRed, alpha * 0.82f, profile.OverallBrightness, new Vector2(1.7f, 0f), Mathf.InverseLerp(0.6f, 0.88f, elapsed));
            }
        }

        private void AnimateEmblem(float elapsed)
        {
            float emergenceTime = 0.3f / Mathf.Max(0.1f, profile.EmblemEmergenceSpeed);
            float end = Mathf.Min(profile.ActivationDuration, 0.3f + profile.EmblemLifetime);
            float alpha = Pulse(elapsed, emergenceTime, 0.6f, end);
            float grow = Smooth01(Mathf.InverseLerp(emergenceTime, 0.6f, elapsed));
            float dissolve = Mathf.Clamp01(Mathf.InverseLerp(end - 0.25f / Mathf.Max(0.1f, profile.EmblemDissolveSpeed), end, elapsed));
            float scalePulse = 1f + Mathf.Sin(Mathf.Clamp01(Mathf.InverseLerp(0.5f, 0.72f, elapsed)) * Mathf.PI) * profile.EmblemPulseIntensity;
            if (emblemRoot != null)
            {
                emblemRoot.localPosition = new Vector3(0f, profile.EmblemHeight + grow * 0.38f, 0.38f + grow * 0.18f);
                emblemRoot.localScale = Vector3.one * profile.EmblemScale * Mathf.Lerp(0.18f, 1f, grow) * scalePulse;
            }

            SetRenderer(emblem, Color.white, alpha, profile.OverallBrightness * 1.2f, Vector2.zero, dissolve);
            SetRenderer(emblemGlow, profile.Colors.DeepOrange, alpha * 0.7f, profile.OverallBrightness * profile.EmblemGlowBrightness, Vector2.zero, dissolve);
        }

        private void AnimateShockwave(float elapsed)
        {
            float ringDuration = 0.5f / Mathf.Max(0.1f, profile.ShockwaveSpeed);
            float progress = Mathf.Clamp01(elapsed / ringDuration);
            float alpha = elapsed <= ringDuration ? 1f - Smooth01(progress) : 0f;
            SetScale(shockwave, Vector3.one * Mathf.Lerp(0.25f, profile.ShockwaveSize, Smooth01(progress)));
            SetRenderer(shockwave, Color.white, alpha, profile.OverallBrightness * 1.1f, Vector2.zero, progress);
        }

        private void AnimateTransfers(float elapsed)
        {
            float alpha = Pulse(elapsed, 0.68f, 0.88f, 1.2f);
            UpdateArmTransfer(leftArmTransfer, ResolveChestPosition(), ResolveHandPosition(true), elapsed, alpha, -1f);
            UpdateArmTransfer(rightArmTransfer, ResolveChestPosition(), ResolveHandPosition(false), elapsed, alpha, 1f);
        }

        private void UpdateArmTransfer(LineRenderer line, Vector3 start, Vector3 end, float elapsed, float alpha, float direction)
        {
            if (line == null)
            {
                return;
            }

            int count = Mathf.Max(2, line.positionCount);
            line.positionCount = count;
            Vector3 axis = end - start;
            Vector3 sideAxis = Vector3.Cross(axis.normalized, Vector3.up);
            if (sideAxis.sqrMagnitude < 0.001f)
            {
                sideAxis = Vector3.right;
            }

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                float spiral = Mathf.Sin(t * Mathf.PI * 3f + elapsed * 18f * direction) * 0.08f * (1f - t * 0.45f);
                line.SetPosition(i, Vector3.Lerp(start, end, t) + sideAxis * spiral + Vector3.up * Mathf.Cos(t * Mathf.PI * 2f + elapsed * 14f) * 0.035f);
            }

            SetRenderer(line, profile.Colors.HotYellow, alpha, profile.OverallBrightness * 1.35f, new Vector2(-3.2f, 0f), Mathf.InverseLerp(1f, 1.2f, elapsed));
        }

        private void BillboardAttachedLayers(float elapsed)
        {
            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }

            if (gameplayCamera == null)
            {
                return;
            }

            Quaternion facing = gameplayCamera.transform.rotation;
            RotateRenderer(chestFlash, facing);
            RotateRenderer(bodyEnvelope, facing * Quaternion.Euler(0f, 0f, elapsed * -14f));
            RotateRenderer(rageSilhouette, facing);
            RotateRenderer(heatDistortion, facing);
            if (profile.EmblemFacesGameplayCamera && emblemRoot != null)
            {
                emblemRoot.rotation = facing;
            }
        }

        private void StartHands()
        {
            if (handsStarted)
            {
                return;
            }

            Transform caster = context.Target != null ? context.Target : context.Source;
            Transform leftAnchor = anchors != null && anchors.LeftHandAnchor != null ? anchors.LeftHandAnchor : caster;
            Transform rightAnchor = anchors != null && anchors.RightHandAnchor != null ? anchors.RightHandAnchor : caster;
            leftHand = InstantiateHand(leftHandPrefab, leftAnchor, BerzerkitisHandSide.Left);
            rightHand = InstantiateHand(rightHandPrefab, rightAnchor, BerzerkitisHandSide.Right);
            handsStarted = true;
            leftHand?.PulseAttack();
            rightHand?.PulseAttack();
        }

        private BerzerkitisHandVFX InstantiateHand(GameObject prefab, Transform parent, BerzerkitisHandSide expectedSide)
        {
            if (prefab == null || parent == null)
            {
                return null;
            }

            GameObject instance = Instantiate(prefab, parent);
            instance.name = prefab.name;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            BerzerkitisHandVFX hand = instance.GetComponent<BerzerkitisHandVFX>();
            if (hand == null)
            {
                Debug.LogError($"{prefab.name} does not contain BerzerkitisHandVFX.", instance);
                Destroy(instance);
                return null;
            }

            if (hand.Side != expectedSide)
            {
                Debug.LogWarning($"{prefab.name} is authored for {hand.Side} but was assigned to {expectedSide}.", instance);
            }

            hand.Play(profile);
            return hand;
        }

        private void CheckBuffState()
        {
            TryResolveBuffController();
            bool hasBuff = HasBuff();
            if (hasBuff)
            {
                buffWasObserved = true;
                return;
            }

            if ((buffWasObserved && Time.time - startedAt > 0.1f) || Time.time >= fallbackExpiresAt)
            {
                FadeOut();
            }
        }

        private bool HasBuff()
        {
            string abilityId = context.Ability != null ? context.Ability.AbilityId : "warrior_berzerkitis";
            return buffController != null && buffController.FindBuff(abilityId) != null;
        }

        private void TryResolveBuffController()
        {
            if (buffController != null)
            {
                return;
            }

            Transform caster = context.Target != null ? context.Target : context.Source;
            buffController = caster != null ? caster.GetComponent<MMOCharacterBuffController>() : null;
            if (buffController != null)
            {
                buffController.BuffsChanged -= OnBuffsChanged;
                buffController.BuffsChanged += OnBuffsChanged;
            }
        }

        private void OnBuffsChanged(MMOCharacterBuffController source)
        {
            if (source == buffController)
            {
                CheckBuffState();
            }
        }

        private void OnCombatEventResolved(CombatEventRecord record, MMOCombatant source, MMOCombatant target, MMOAbilityDefinition ability)
        {
            if (!playing || fading || source != casterCombatant || record == null)
            {
                return;
            }

            if (record.eventType == CombatEventType.DamageResolved && record.damageAmount > 0)
            {
                PulseHands();
            }
        }

        private void Subscribe()
        {
            if (buffController != null)
            {
                buffController.BuffsChanged -= OnBuffsChanged;
                buffController.BuffsChanged += OnBuffsChanged;
            }

            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            MMOCombatEventStream.CombatEventResolved += OnCombatEventResolved;
        }

        private void Unsubscribe()
        {
            if (buffController != null)
            {
                buffController.BuffsChanged -= OnBuffsChanged;
            }

            MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
        }

        private void Complete()
        {
            if (!playing)
            {
                return;
            }

            playing = false;
            fading = false;
            StopAllParticles();
            HideActivationLayers();
            Completed?.Invoke(this);
            if (destroyOnComplete && gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void ConfigureParticleBudgets()
        {
            ConfigureBurst(flameColumns, profile.FlameColumnCount, 1.8f, profile.FlameColumnHeight * 0.38f);
            ConfigureBurst(activationEmbers, profile.ActivationEmberCount, 2.8f, 0.16f);
            ConfigureBurst(hotSparks, Mathf.Max(4, profile.ActivationEmberCount / 3), 6.2f, 0.12f);
            ConfigureBurst(attachedSmoke, Mathf.Max(2, profile.DustAmount / 4), 0.7f, 0.55f);
            ConfigureBurst(emblemEmbers, Mathf.Max(4, profile.ActivationEmberCount / 4), 1.6f, 0.11f);
            ConfigureBurst(groundDust, profile.DustAmount, 2.2f, 0.72f);
            ConfigureBurst(worldEmbers, Mathf.Max(6, profile.ActivationEmberCount / 2), 2.5f, 0.13f);
            ConfigureBurst(worldSmoke, Mathf.Max(2, profile.DustAmount / 3), 0.55f, 0.65f);
        }

        private void PlayAllParticles()
        {
            PlayParticle(flameColumns);
            PlayParticle(activationEmbers);
            PlayParticle(hotSparks);
            PlayParticle(attachedSmoke);
            PlayParticle(emblemEmbers);
            PlayParticle(groundDust);
            PlayParticle(worldEmbers);
            PlayParticle(worldSmoke);
        }

        private void StopAttachedParticles(bool clear)
        {
            StopParticle(flameColumns, clear);
            StopParticle(activationEmbers, clear);
            StopParticle(hotSparks, clear);
            StopParticle(attachedSmoke, clear);
            StopParticle(emblemEmbers, clear);
        }

        private void StopAllParticles()
        {
            StopAttachedParticles(true);
            StopParticle(groundDust, true);
            StopParticle(worldEmbers, true);
            StopParticle(worldSmoke, true);
        }

        private void HideActivationLayers()
        {
            SetRenderer(chestFlash, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(bodyEnvelope, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(rageSilhouette, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(heatDistortion, Color.white, 0f, 0f, Vector2.zero, 1f, 0f);
            SetRenderer(emblem, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(emblemGlow, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(shockwave, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(waistBand, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(shoulderBand, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(leftArmTransfer, Color.white, 0f, 0f, Vector2.zero, 1f);
            SetRenderer(rightArmTransfer, Color.white, 0f, 0f, Vector2.zero, 1f);
        }

        private void SetRenderer(Renderer renderer, Color tint, float opacity, float brightness, Vector2 scroll, float dissolve, float distortion = 0f)
        {
            if (renderer == null)
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(TintId, tint);
            propertyBlock.SetFloat(OpacityId, Mathf.Clamp01(opacity));
            propertyBlock.SetFloat(BrightnessId, Mathf.Max(0f, brightness));
            propertyBlock.SetVector(ScrollSpeedId, scroll);
            propertyBlock.SetFloat(DissolveId, Mathf.Clamp01(dissolve));
            propertyBlock.SetFloat(DistortionId, Mathf.Max(0f, distortion));
            renderer.SetPropertyBlock(propertyBlock);
            propertyBlock.Clear();
        }

        private Vector3 ResolveChestPosition()
        {
            Transform caster = context.Target != null ? context.Target : context.Source;
            if (caster == null)
            {
                return transform.position + Vector3.up * 1.25f;
            }

            return anchors != null && anchors.CastingAnchor != null
                ? anchors.CastingAnchor.position
                : caster.TransformPoint(new Vector3(0f, 1.25f, 0.1f));
        }

        private Vector3 ResolveHandPosition(bool left)
        {
            Transform caster = context.Target != null ? context.Target : context.Source;
            Transform hand = anchors == null ? null : left ? anchors.LeftHandAnchor : anchors.RightHandAnchor;
            if (hand != null)
            {
                return hand.position;
            }

            return caster != null
                ? caster.TransformPoint(new Vector3(left ? -0.42f : 0.42f, 0.92f, 0.12f))
                : transform.position;
        }

        private static float ResolveBuffDuration(MMOAbilityDefinition ability)
        {
            float duration = 15f;
            if (ability == null)
            {
                return duration;
            }

            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                if (effect != null && effect.EffectType == MMOAbilityEffectType.TemporaryStatModifier)
                {
                    duration = Mathf.Max(duration, effect.DurationSeconds);
                }
            }

            return duration;
        }

        private void ResetRuntimeHands()
        {
            if (leftHand != null)
            {
                if (Application.isPlaying) Destroy(leftHand.gameObject); else DestroyImmediate(leftHand.gameObject);
            }

            if (rightHand != null)
            {
                if (Application.isPlaying) Destroy(rightHand.gameObject); else DestroyImmediate(rightHand.gameObject);
            }

            leftHand = null;
            rightHand = null;
            handsStarted = false;
        }

        private static void ConfigureBurst(ParticleSystem particles, int count, float speed, float size)
        {
            if (particles == null)
            {
                return;
            }

            ParticleSystem.MainModule main = particles.main;
            main.maxParticles = Mathf.Max(1, count * 2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.65f, speed * 1.18f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.65f, size * 1.2f);
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.SetBursts(count > 0
                ? new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 0, short.MaxValue)) }
                : Array.Empty<ParticleSystem.Burst>());
        }

        private static void PlayParticle(ParticleSystem particles)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }

        private static void StopParticle(ParticleSystem particles, bool clear)
        {
            if (particles == null) return;
            particles.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
        }

        private static void SetScale(Renderer renderer, Vector3 scale)
        {
            if (renderer != null) renderer.transform.localScale = scale;
        }

        private static void RotateRenderer(Renderer renderer, Quaternion rotation)
        {
            if (renderer != null) renderer.transform.rotation = rotation;
        }

        private static float Pulse(float value, float start, float peak, float end)
        {
            if (value < start || value >= end) return 0f;
            if (value <= peak) return Smooth01(Mathf.InverseLerp(start, peak, value));
            return 1f - Smooth01(Mathf.InverseLerp(peak, end, value));
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
