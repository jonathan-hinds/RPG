using System;
using System.Collections.Generic;
using RPGClone.Combat;
using UnityEngine;

namespace RPGClone.Vfx.Physical
{
    /// <summary>
    /// Receiver-local presentation relay for Gouge damage results. It deliberately uses local arrival
    /// time rather than comparing clocks across machines, and it never mutates combat or buff state.
    /// </summary>
    public static class GougeVFXEventRelay
    {
        public const string AbilityId = "warrior_gouge";

        public readonly struct DamagePresentation
        {
            public readonly CombatEventRecord Record;
            public readonly MMOCombatant Source;
            public readonly MMOCombatant Target;
            public readonly float ReceivedAt;

            public DamagePresentation(CombatEventRecord record, MMOCombatant source, MMOCombatant target, float receivedAt)
            {
                Record = record;
                Source = source;
                Target = target;
                ReceivedAt = receivedAt;
            }
        }

        private static readonly Dictionary<MMOCombatant, DamagePresentation> RecentByTarget = new();
        private static bool subscribed;

        public static event Action<DamagePresentation> DamageResolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (subscribed)
            {
                MMOCombatEventStream.CombatEventResolved -= OnCombatEventResolved;
            }

            subscribed = false;
            RecentByTarget.Clear();
            DamageResolved = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureSubscribed();
        }

        public static bool TryGetRecent(MMOCombatant target, float maxAgeSeconds, out DamagePresentation presentation)
        {
            EnsureSubscribed();
            if (target != null
                && RecentByTarget.TryGetValue(target, out presentation)
                && Time.realtimeSinceStartup - presentation.ReceivedAt <= Mathf.Max(0f, maxAgeSeconds))
            {
                return true;
            }

            presentation = default;
            return false;
        }

        private static void EnsureSubscribed()
        {
            if (subscribed)
            {
                return;
            }

            MMOCombatEventStream.CombatEventResolved += OnCombatEventResolved;
            subscribed = true;
        }

        private static void OnCombatEventResolved(
            CombatEventRecord record,
            MMOCombatant source,
            MMOCombatant target,
            RPGClone.Abilities.MMOAbilityDefinition ability)
        {
            if (record == null
                || record.eventType != CombatEventType.DamageResolved
                || target == null
                || ability == null
                || !string.Equals(ability.AbilityId, AbilityId, StringComparison.Ordinal))
            {
                return;
            }

            DamagePresentation presentation = new(record, source, target, Time.realtimeSinceStartup);
            RecentByTarget[target] = presentation;
            DamageResolved?.Invoke(presentation);

            if (RecentByTarget.Count > 64)
            {
                PruneOldEntries();
            }
        }

        private static void PruneOldEntries()
        {
            float cutoff = Time.realtimeSinceStartup - 12f;
            List<MMOCombatant> expiredKeys = null;
            foreach (KeyValuePair<MMOCombatant, DamagePresentation> entry in RecentByTarget)
            {
                if (entry.Key != null && entry.Value.ReceivedAt >= cutoff)
                {
                    continue;
                }

                expiredKeys ??= new List<MMOCombatant>();
                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
            {
                return;
            }

            foreach (MMOCombatant key in expiredKeys)
            {
                RecentByTarget.Remove(key);
            }
        }
    }
}
