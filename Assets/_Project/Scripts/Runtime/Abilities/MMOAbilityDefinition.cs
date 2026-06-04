using System.Collections.Generic;
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
        public IReadOnlyList<MMOAbilityEffectDefinition> Effects => effects;

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
            abilityId = string.IsNullOrWhiteSpace(newAbilityId) ? name : newAbilityId;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? abilityId : newDisplayName;
            description = newDescription;
            targetType = newTargetType;
            autoAttack = newAutoAttack;
            toggled = newToggled;
            range = Mathf.Max(0f, newRange);
            cooldownSeconds = Mathf.Max(0f, newCooldownSeconds);
            manaCost = Mathf.Max(0, newManaCost);
            effects = newEffects != null ? new List<MMOAbilityEffectDefinition>(newEffects) : new List<MMOAbilityEffectDefinition>();
        }
    }
}
