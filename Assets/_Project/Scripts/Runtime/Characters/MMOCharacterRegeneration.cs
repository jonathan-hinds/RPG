using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Characters
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    [RequireComponent(typeof(MMOCombatant))]
    public sealed class MMOCharacterRegeneration : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float outOfCombatDelaySeconds = 5f;
        [SerializeField, Min(0.1f)] private float tickSeconds = 1f;

        private MMOCharacterIdentity identity;
        private MMOCombatant combatant;
        private float nextRegenTime;
        private float nextAllowedRegenTime;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            combatant.CombatActivity += OnCombatActivity;
        }

        private void OnDisable()
        {
            if (combatant != null)
            {
                combatant.CombatActivity -= OnCombatActivity;
            }
        }

        private void Update()
        {
            EnsureReferences();
            if (identity == null || combatant == null || !combatant.IsAlive || Time.time < nextAllowedRegenTime || Time.time < nextRegenTime)
            {
                return;
            }

            nextRegenTime = Time.time + tickSeconds;
            ApplyRegeneration();
        }

        private void ApplyRegeneration()
        {
            MMOCharacterStats stats = identity.Stats;
            if (stats == null)
            {
                return;
            }

            int healthAmount = Mathf.FloorToInt(stats.HealthRegenPerSecond * tickSeconds);
            int manaAmount = Mathf.FloorToInt(stats.ManaRegenPerSecond * tickSeconds);

            if (healthAmount > 0 && identity.Health.CurrentValue < identity.Health.MaxValue)
            {
                identity.Health.SetCurrent(identity.Health.CurrentValue + healthAmount);
            }

            if (manaAmount > 0 && identity.Mana.CurrentValue < identity.Mana.MaxValue)
            {
                identity.Mana.SetCurrent(identity.Mana.CurrentValue + manaAmount);
            }
        }

        private void OnCombatActivity(MMOCombatant source)
        {
            nextAllowedRegenTime = Time.time + outOfCombatDelaySeconds;
        }

        private void EnsureReferences()
        {
            if (identity == null)
            {
                identity = GetComponent<MMOCharacterIdentity>();
            }

            if (combatant == null)
            {
                combatant = GetComponent<MMOCombatant>();
            }
        }
    }
}
