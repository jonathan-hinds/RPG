using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Vfx;
using UnityEngine;

namespace RPGClone.Abilities
{
    [CreateAssetMenu(menuName = "RPG Clone/Abilities/Ability", fileName = "Ability")]
    public sealed class MMOAbilityDefinition : ScriptableObject
    {
        [SerializeField] private string abilityId = "ability";
        [SerializeField] private string displayName = "Ability";
        [SerializeField, TextArea] private string description;
        [SerializeField] private Sprite icon;
        [SerializeField] private MMOAbilityTargetType targetType = MMOAbilityTargetType.Hostile;
        [SerializeField] private bool autoAttack;
        [SerializeField] private bool toggled;
        [SerializeField, Min(0f)] private float range = 3f;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(0)] private int manaCost;
        [SerializeField] private MMOAbilityManaCostSource manaCostSource = MMOAbilityManaCostSource.Flat;
        [SerializeField, Range(0f, 1f)] private float maximumManaCostPercent;
        [SerializeField, Min(0f)] private float castTimeSeconds;
        [SerializeField] private bool channeled;
        [SerializeField] private bool interruptOnMovement;
        [SerializeField] private bool castOnSelfWhenFriendlyTargetInvalid;
        [SerializeField] private bool resetCooldownOnCriticalHit;
        [SerializeField] private MMOAbilityAnimationStyle animationStyle = MMOAbilityAnimationStyle.Automatic;
        [SerializeField] private MMOAbilityVfxDefinition visualEffects;
        [Header("Area")]
        [SerializeField, Min(0f)] private float areaRadius;
        [SerializeField] private MMOAbilityAreaTargetFilter areaTargetFilter = MMOAbilityAreaTargetFilter.Hostile;
        [SerializeField] private List<MMOAbilityEffectDefinition> effects = new();

        public string AbilityId => string.IsNullOrWhiteSpace(abilityId) ? name : abilityId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public MMOAbilityTargetType TargetType => targetType;
        public bool IsAutoAttack => autoAttack;
        public bool IsToggled => toggled;
        public float Range => range;
        public float CooldownSeconds => cooldownSeconds;
        public int ManaCost => manaCost;
        public MMOAbilityManaCostSource ManaCostSource => manaCostSource;
        public float MaximumManaCostPercent => Mathf.Clamp01(maximumManaCostPercent);
        public float CastTimeSeconds => castTimeSeconds;
        public bool IsChanneled => channeled && castTimeSeconds > 0f;
        public bool InterruptOnMovement => interruptOnMovement;
        public bool CastOnSelfWhenFriendlyTargetInvalid => castOnSelfWhenFriendlyTargetInvalid;
        public bool ResetCooldownOnCriticalHit => resetCooldownOnCriticalHit;
        public MMOAbilityAnimationStyle AnimationStyle => animationStyle;
        public MMOAbilityVfxDefinition VisualEffects => visualEffects;
        public float AreaRadius => areaRadius;
        public MMOAbilityAreaTargetFilter AreaTargetFilter => areaTargetFilter;
        public bool HasArea => areaRadius > 0f;
        public bool RequiresGroundTarget => targetType == MMOAbilityTargetType.GroundArea;
        public IReadOnlyList<MMOAbilityEffectDefinition> Effects => effects;

        public int CalculateManaCost(MMOCharacterIdentity caster)
        {
            if (manaCostSource != MMOAbilityManaCostSource.MaximumManaPercentage)
            {
                return Mathf.Max(0, manaCost);
            }

            int maximumMana = caster != null ? caster.Mana.MaxValue : 0;
            return Mathf.Max(0, Mathf.CeilToInt(maximumMana * MaximumManaCostPercent));
        }

        public void Configure(
            string newAbilityId,
            string newDisplayName,
            string newDescription,
            MMOAbilityTargetType newTargetType,
            bool newAutoAttack,
            bool newToggled,
            float newRange,
            float newCooldownSeconds,
            int newManaCost,
            IEnumerable<MMOAbilityEffectDefinition> newEffects)
        {
            Configure(
                newAbilityId,
                newDisplayName,
                newDescription,
                newTargetType,
                newAutoAttack,
                newToggled,
                newRange,
                newCooldownSeconds,
                newManaCost,
                0f,
                false,
                false,
                false,
                false,
                0f,
                MMOAbilityAreaTargetFilter.Hostile,
                newEffects);
        }

        public void Configure(
            string newAbilityId,
            string newDisplayName,
            string newDescription,
            MMOAbilityTargetType newTargetType,
            bool newAutoAttack,
            bool newToggled,
            float newRange,
            float newCooldownSeconds,
            int newManaCost,
            float newCastTimeSeconds,
            bool newInterruptOnMovement,
            bool newCastOnSelfWhenFriendlyTargetInvalid,
            IEnumerable<MMOAbilityEffectDefinition> newEffects)
        {
            Configure(
                newAbilityId,
                newDisplayName,
                newDescription,
                newTargetType,
                newAutoAttack,
                newToggled,
                newRange,
                newCooldownSeconds,
                newManaCost,
                newCastTimeSeconds,
                false,
                newInterruptOnMovement,
                newCastOnSelfWhenFriendlyTargetInvalid,
                false,
                0f,
                MMOAbilityAreaTargetFilter.Hostile,
                newEffects);
        }

        public void Configure(
            string newAbilityId,
            string newDisplayName,
            string newDescription,
            MMOAbilityTargetType newTargetType,
            bool newAutoAttack,
            bool newToggled,
            float newRange,
            float newCooldownSeconds,
            int newManaCost,
            float newCastTimeSeconds,
            bool newInterruptOnMovement,
            bool newCastOnSelfWhenFriendlyTargetInvalid,
            float newAreaRadius,
            MMOAbilityAreaTargetFilter newAreaTargetFilter,
            IEnumerable<MMOAbilityEffectDefinition> newEffects)
        {
            Configure(
                newAbilityId,
                newDisplayName,
                newDescription,
                newTargetType,
                newAutoAttack,
                newToggled,
                newRange,
                newCooldownSeconds,
                newManaCost,
                newCastTimeSeconds,
                false,
                newInterruptOnMovement,
                newCastOnSelfWhenFriendlyTargetInvalid,
                false,
                newAreaRadius,
                newAreaTargetFilter,
                newEffects);
        }

        public void Configure(
            string newAbilityId,
            string newDisplayName,
            string newDescription,
            MMOAbilityTargetType newTargetType,
            bool newAutoAttack,
            bool newToggled,
            float newRange,
            float newCooldownSeconds,
            int newManaCost,
            float newCastTimeSeconds,
            bool newChanneled,
            bool newInterruptOnMovement,
            bool newCastOnSelfWhenFriendlyTargetInvalid,
            bool newResetCooldownOnCriticalHit,
            float newAreaRadius,
            MMOAbilityAreaTargetFilter newAreaTargetFilter,
            IEnumerable<MMOAbilityEffectDefinition> newEffects)
        {
            abilityId = string.IsNullOrWhiteSpace(newAbilityId) ? name : newAbilityId;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? abilityId : newDisplayName;
            description = newDescription;
            targetType = newTargetType;
            autoAttack = newAutoAttack;
            toggled = newToggled;
            range = Mathf.Max(0f, newRange);
            cooldownSeconds = Mathf.Max(0f, newCooldownSeconds);
            manaCost = Mathf.Max(0, newManaCost);
            manaCostSource = MMOAbilityManaCostSource.Flat;
            maximumManaCostPercent = 0f;
            castTimeSeconds = Mathf.Max(0f, newCastTimeSeconds);
            channeled = newChanneled;
            interruptOnMovement = newInterruptOnMovement;
            castOnSelfWhenFriendlyTargetInvalid = newCastOnSelfWhenFriendlyTargetInvalid;
            resetCooldownOnCriticalHit = newResetCooldownOnCriticalHit;
            areaRadius = Mathf.Max(0f, newAreaRadius);
            areaTargetFilter = newAreaTargetFilter;
            effects = newEffects != null ? new List<MMOAbilityEffectDefinition>(newEffects) : new List<MMOAbilityEffectDefinition>();
        }

        public void SetAnimationStyle(MMOAbilityAnimationStyle newAnimationStyle)
        {
            animationStyle = newAnimationStyle;
        }

        public void SetMaximumManaCost(float newMaximumManaCostPercent)
        {
            manaCost = 0;
            manaCostSource = MMOAbilityManaCostSource.MaximumManaPercentage;
            maximumManaCostPercent = Mathf.Clamp01(newMaximumManaCostPercent);
        }

        public void SetIcon(Sprite newIcon)
        {
            icon = newIcon;
        }

        public void SetVisualEffects(MMOAbilityVfxDefinition newVisualEffects)
        {
            visualEffects = newVisualEffects;
        }
    }
}
