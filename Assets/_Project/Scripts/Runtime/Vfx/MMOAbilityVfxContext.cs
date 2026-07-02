using RPGClone.Abilities;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Vfx
{
    public interface IMMOAbilityVfxInstance
    {
        void Initialize(MMOAbilityVfxContext context);
    }

    public readonly struct MMOAbilityVfxContext
    {
        public readonly MMOAbilitySystem SourceSystem;
        public readonly MMOAbilityDefinition Ability;
        public readonly MMOAbilityVfxDefinition Definition;
        public readonly Transform Source;
        public readonly Transform Target;
        public readonly Vector3 SourcePosition;
        public readonly Vector3 TargetPosition;
        public readonly bool HasGroundTarget;
        public readonly System.Action RequestHit;

        public MMOAbilityVfxContext(
            MMOAbilitySystem sourceSystem,
            MMOAbilityDefinition ability,
            MMOAbilityVfxDefinition definition,
            Transform source,
            Transform target,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            bool hasGroundTarget,
            System.Action requestHit)
        {
            SourceSystem = sourceSystem;
            Ability = ability;
            Definition = definition;
            Source = source;
            Target = target;
            SourcePosition = sourcePosition;
            TargetPosition = targetPosition;
            HasGroundTarget = hasGroundTarget;
            RequestHit = requestHit;
        }
    }
}
