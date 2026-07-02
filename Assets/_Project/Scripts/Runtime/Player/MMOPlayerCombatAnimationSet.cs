using RPGClone.Animation;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Player
{
    [CreateAssetMenu(menuName = "RPG Clone/Player/Combat Animation Set", fileName = "PlayerCombatAnimationSet")]
    public sealed class MMOPlayerCombatAnimationSet : ScriptableObject
    {
        public const string InCombatParameter = "InCombat";
        public const string ActionSpeedParameter = "ActionSpeed";
        public const string UpperBodyLayerName = "Upper Body";

        public const string CombatIdlePlaceholderName = "MMO_CombatIdle";
        public const string OneHandAttackPlaceholderName = "MMO_Attack1H";
        public const string TwoHandAttackPlaceholderName = "MMO_Attack2H";
        public const string UnarmedAttackPlaceholderName = "MMO_AttackUnarmed";
        public const string DamagePlaceholderName = "MMO_CombatDamage";
        public const string CastingPlaceholderName = "MMO_Casting";
        public const string CastPlaceholderName = "MMO_Cast";

        public const string LocomotionStatePath = "Base Layer.Locomotion";
        public const string CombatIdleStatePath = "Base Layer.CombatIdle";
        public const string OneHandAttackStatePath = "Base Layer.Attack1H";
        public const string TwoHandAttackStatePath = "Base Layer.Attack2H";
        public const string UnarmedAttackStatePath = "Base Layer.AttackUnarmed";
        public const string FullBodyDamageStatePath = "Base Layer.CombatDamage";
        public const string CastingStatePath = "Base Layer.Casting";
        public const string CastStatePath = "Base Layer.Cast";
        public const string UpperBodyEmptyStatePath = UpperBodyLayerName + ".Empty";
        public const string UpperBodyDamageStatePath = UpperBodyLayerName + ".Damage";

        [Header("Controller")]
        [SerializeField] private RuntimeAnimatorController baseController;

        [Header("Combat Stance")]
        [SerializeField] private AnimationClip combatIdle;
        [SerializeField] private AnimationClip twoHandCombatIdle;
        [SerializeField, Min(0f)] private float idleEnterTransitionSeconds = 0.16f;
        [SerializeField, Min(0f)] private float idleExitTransitionSeconds = 0.12f;
        [SerializeField, Min(0f)] private float stationarySpeedThreshold = 0.08f;

        [Header("Weapon Attacks")]
        [SerializeField] private AnimationClip oneHandAttack;
        [SerializeField] private AnimationClip twoHandAttack;
        [SerializeField] private AnimationClip unarmedAttack;
        [SerializeField, Range(0.05f, 0.95f)] private float attackImpactNormalizedTime = 0.65f;
        [SerializeField, Min(0.1f)] private float minAttackPlaybackSpeed = 1f;
        [SerializeField, Min(0.1f)] private float maxAttackPlaybackSpeed = 2.75f;
        [SerializeField, Min(0f)] private float attackTransitionSeconds = 0.04f;
        [SerializeField, Min(0.01f)] private float fallbackAttackDurationSeconds = 0.75f;

        [Header("Reactions")]
        [SerializeField] private AnimationClip damage;
        [SerializeField, Min(0f)] private float damageTransitionSeconds = 0.04f;
        [SerializeField, Min(0f)] private float damageReactionCooldownSeconds = 0.35f;
        [SerializeField, Min(0f)] private float deferredDamageReactionWindowSeconds = 0.35f;

        [Header("Casting")]
        [SerializeField] private AnimationClip casting;
        [SerializeField] private AnimationClip cast;
        [SerializeField, Min(0f)] private float castingTransitionSeconds = 0.08f;
        [SerializeField, Min(0f)] private float castTransitionSeconds = 0.04f;
        [SerializeField, Min(0.01f)] private float fallbackCastDurationSeconds = 0.55f;

        public RuntimeAnimatorController BaseController => baseController;
        public AnimationClip CombatIdle => combatIdle;
        public AnimationClip TwoHandCombatIdle => twoHandCombatIdle;
        public AnimationClip OneHandAttack => oneHandAttack;
        public AnimationClip TwoHandAttack => twoHandAttack;
        public AnimationClip UnarmedAttack => unarmedAttack;
        public AnimationClip Damage => damage;
        public AnimationClip Casting => casting;
        public AnimationClip Cast => cast;
        public float IdleEnterTransitionSeconds => idleEnterTransitionSeconds;
        public float IdleExitTransitionSeconds => idleExitTransitionSeconds;
        public float StationarySpeedThreshold => stationarySpeedThreshold;
        public float AttackImpactNormalizedTime => attackImpactNormalizedTime;
        public float AttackTransitionSeconds => attackTransitionSeconds;
        public float DamageTransitionSeconds => damageTransitionSeconds;
        public float DamageReactionCooldownSeconds => damageReactionCooldownSeconds;
        public float DeferredDamageReactionWindowSeconds => deferredDamageReactionWindowSeconds;
        public float CastingTransitionSeconds => castingTransitionSeconds;
        public float CastTransitionSeconds => castTransitionSeconds;

        public AnimationClip GetAttackClip(MMOWeaponType weaponType)
        {
            if (IsTwoHanded(weaponType) && twoHandAttack != null)
            {
                return twoHandAttack;
            }

            if (weaponType == MMOWeaponType.Unarmed && unarmedAttack != null)
            {
                return unarmedAttack;
            }

            return oneHandAttack != null ? oneHandAttack : twoHandAttack != null ? twoHandAttack : unarmedAttack;
        }

        public AnimationClip GetCombatIdleClip(MMOWeaponType weaponType)
        {
            if (IsTwoHanded(weaponType) && twoHandCombatIdle != null)
            {
                return twoHandCombatIdle;
            }

            return combatIdle;
        }

        public string GetAttackStatePath(MMOWeaponType weaponType)
        {
            if (IsTwoHanded(weaponType) && twoHandAttack != null)
            {
                return TwoHandAttackStatePath;
            }

            if (weaponType == MMOWeaponType.Unarmed && unarmedAttack != null)
            {
                return UnarmedAttackStatePath;
            }

            return OneHandAttackStatePath;
        }

        public float CalculateAttackPlaybackSpeed(MMOWeaponType weaponType, float swingDurationSeconds)
        {
            AnimationClip clip = GetAttackClip(weaponType);
            float clipLength = clip != null ? clip.length : fallbackAttackDurationSeconds;
            float safeSwingDuration = Mathf.Max(0.01f, swingDurationSeconds);
            float speed = clipLength > safeSwingDuration ? clipLength / safeSwingDuration : 1f;
            return Mathf.Clamp(speed, minAttackPlaybackSpeed, Mathf.Max(minAttackPlaybackSpeed, maxAttackPlaybackSpeed));
        }

        public float CalculateAttackLeadSeconds(MMOWeaponType weaponType, float swingDurationSeconds)
        {
            AnimationClip clip = GetAttackClip(weaponType);
            float clipLength = clip != null ? clip.length : fallbackAttackDurationSeconds;
            float speed = CalculateAttackPlaybackSpeed(weaponType, swingDurationSeconds);
            return clipLength * attackImpactNormalizedTime / Mathf.Max(0.01f, speed);
        }

        public float GetAttackDurationSeconds(MMOWeaponType weaponType, float playbackSpeed)
        {
            AnimationClip clip = GetAttackClip(weaponType);
            float clipLength = clip != null ? clip.length : fallbackAttackDurationSeconds;
            return clipLength / Mathf.Max(0.01f, playbackSpeed);
        }

        public float GetDamageDurationSeconds()
        {
            return damage != null ? damage.length : 0.45f;
        }

        public float GetCastDurationSeconds()
        {
            return cast != null ? cast.length : fallbackCastDurationSeconds;
        }

        public AnimationClip GetReplacementClip(AnimationClip placeholder)
        {
            if (placeholder == null)
            {
                return null;
            }

            return placeholder.name switch
            {
                CombatIdlePlaceholderName => combatIdle,
                OneHandAttackPlaceholderName => oneHandAttack,
                TwoHandAttackPlaceholderName => twoHandAttack != null ? twoHandAttack : oneHandAttack,
                UnarmedAttackPlaceholderName => unarmedAttack != null ? unarmedAttack : oneHandAttack,
                DamagePlaceholderName => damage,
                CastingPlaceholderName => casting,
                CastPlaceholderName => cast,
                MMOCreatureAnimationSet.IdlePlaceholderName => null,
                _ => null
            };
        }

        public void Configure(
            RuntimeAnimatorController newBaseController,
            AnimationClip newCombatIdle,
            AnimationClip newTwoHandCombatIdle,
            AnimationClip newOneHandAttack,
            AnimationClip newTwoHandAttack,
            AnimationClip newUnarmedAttack,
            AnimationClip newDamage,
            AnimationClip newCasting,
            AnimationClip newCast,
            float newAttackImpactNormalizedTime)
        {
            baseController = newBaseController;
            combatIdle = newCombatIdle;
            twoHandCombatIdle = newTwoHandCombatIdle;
            oneHandAttack = newOneHandAttack;
            twoHandAttack = newTwoHandAttack;
            unarmedAttack = newUnarmedAttack;
            damage = newDamage;
            casting = newCasting;
            cast = newCast;
            attackImpactNormalizedTime = Mathf.Clamp(newAttackImpactNormalizedTime, 0.05f, 0.95f);
        }

        private static bool IsTwoHanded(MMOWeaponType weaponType)
        {
            return weaponType == MMOWeaponType.TwoHandSword
                || weaponType == MMOWeaponType.TwoHandMace
                || weaponType == MMOWeaponType.Staff;
        }
    }
}
