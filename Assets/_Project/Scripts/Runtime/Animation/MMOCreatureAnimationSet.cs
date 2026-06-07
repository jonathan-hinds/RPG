using UnityEngine;

namespace RPGClone.Animation
{
    [CreateAssetMenu(menuName = "RPG Clone/Animation/Creature Animation Set", fileName = "CreatureAnimationSet")]
    public sealed class MMOCreatureAnimationSet : ScriptableObject
    {
        public const string MoveSpeedParameter = "MoveSpeed";
        public const string Attack1Parameter = "Attack1";
        public const string Attack2Parameter = "Attack2";
        public const string DamageParameter = "Damage";
        public const string DeathParameter = "Death";
        public const string DeadParameter = "Dead";

        public const string IdlePlaceholderName = "MMO_Idle";
        public const string WalkPlaceholderName = "MMO_Walk";
        public const string RunPlaceholderName = "MMO_Run";
        public const string Attack1PlaceholderName = "MMO_Attack1";
        public const string Attack2PlaceholderName = "MMO_Attack2";
        public const string DamagePlaceholderName = "MMO_Damage";
        public const string DeathPlaceholderName = "MMO_Death";

        [Header("Controller")]
        [SerializeField] private RuntimeAnimatorController baseController;

        [Header("Locomotion")]
        [SerializeField] private AnimationClip idle;
        [SerializeField] private AnimationClip walk;
        [SerializeField] private AnimationClip run;
        [SerializeField, Min(0.01f)] private float walkSpeed = 1.45f;
        [SerializeField, Min(0.01f)] private float runSpeed = 4.25f;
        [SerializeField, Min(0f)] private float movementDampSeconds = 0.12f;

        [Header("Combat")]
        [SerializeField] private AnimationClip attack1;
        [SerializeField] private AnimationClip attack2;
        [SerializeField] private AnimationClip damage;
        [SerializeField] private AnimationClip death;
        [SerializeField] private bool alternateAttacks = true;
        [SerializeField, Min(0f)] private float attackPrioritySeconds = 0.8f;
        [SerializeField, Min(0f)] private float damageReactionCooldownSeconds = 0.45f;

        [Header("Presentation")]
        [SerializeField] private bool applyRootMotion;
        [SerializeField] private float modelYawOffsetDegrees;

        public RuntimeAnimatorController BaseController => baseController;
        public AnimationClip Idle => idle;
        public AnimationClip Walk => walk;
        public AnimationClip Run => run;
        public AnimationClip Attack1 => attack1;
        public AnimationClip Attack2 => attack2;
        public AnimationClip Damage => damage;
        public AnimationClip Death => death;
        public bool AlternateAttacks => alternateAttacks;
        public float AttackPrioritySeconds => attackPrioritySeconds;
        public float DamageReactionCooldownSeconds => damageReactionCooldownSeconds;
        public float MovementDampSeconds => movementDampSeconds;
        public bool ApplyRootMotion => applyRootMotion;
        public float ModelYawOffsetDegrees => modelYawOffsetDegrees;

        public float NormalizeMoveSpeed(float worldSpeed)
        {
            if (worldSpeed <= 0.03f)
            {
                return 0f;
            }

            float safeWalkSpeed = Mathf.Max(0.01f, walkSpeed);
            float safeRunSpeed = Mathf.Max(safeWalkSpeed + 0.01f, runSpeed);
            if (worldSpeed <= safeWalkSpeed)
            {
                return Mathf.Lerp(0f, 0.5f, worldSpeed / safeWalkSpeed);
            }

            float runBlend = Mathf.InverseLerp(safeWalkSpeed, safeRunSpeed, worldSpeed);
            return Mathf.Lerp(0.5f, 1f, runBlend);
        }

        public void Configure(
            RuntimeAnimatorController newBaseController,
            AnimationClip newIdle,
            AnimationClip newWalk,
            AnimationClip newRun,
            AnimationClip newAttack1,
            AnimationClip newAttack2,
            AnimationClip newDamage,
            AnimationClip newDeath,
            float newWalkSpeed,
            float newRunSpeed,
            float newAttackPrioritySeconds,
            float newDamageReactionCooldownSeconds,
            float newMovementDampSeconds,
            bool newApplyRootMotion,
            float newModelYawOffsetDegrees)
        {
            baseController = newBaseController;
            idle = newIdle;
            walk = newWalk;
            run = newRun;
            attack1 = newAttack1;
            attack2 = newAttack2;
            damage = newDamage;
            death = newDeath;
            walkSpeed = Mathf.Max(0.01f, newWalkSpeed);
            runSpeed = Mathf.Max(walkSpeed + 0.01f, newRunSpeed);
            attackPrioritySeconds = Mathf.Max(0f, newAttackPrioritySeconds);
            damageReactionCooldownSeconds = Mathf.Max(0f, newDamageReactionCooldownSeconds);
            movementDampSeconds = Mathf.Max(0f, newMovementDampSeconds);
            applyRootMotion = newApplyRootMotion;
            modelYawOffsetDegrees = newModelYawOffsetDegrees;
        }
    }
}
