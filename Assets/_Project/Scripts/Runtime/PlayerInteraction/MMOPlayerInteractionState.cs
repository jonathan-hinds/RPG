using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.PlayerInteraction
{
    public static class MMOPlayerInteractionState
    {
        private static readonly List<MMODuelSessionSnapshot> Duels = new();
        private static readonly List<MMOTradeSessionSnapshot> Trades = new();

        public static event Action Changed;
        public static IReadOnlyList<MMODuelSessionSnapshot> DuelSessions => Duels;
        public static IReadOnlyList<MMOTradeSessionSnapshot> TradeSessions => Trades;

        public static void Reset()
        {
            Duels.Clear();
            Trades.Clear();
            MMOPlayerInteractionService.ResetRuntimeState();
            Changed?.Invoke();
        }

        public static void Upsert(MMODuelSessionSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.duelId))
            {
                return;
            }

            MMODuelSessionSnapshot clone = Clone(snapshot);
            int index = Duels.FindIndex(candidate => candidate != null && candidate.duelId == clone.duelId);
            if (index >= 0)
            {
                if (Duels[index].revision > clone.revision)
                {
                    return;
                }

                Duels[index] = clone;
            }
            else
            {
                Duels.Add(clone);
            }

            Changed?.Invoke();
        }

        public static void Upsert(MMOTradeSessionSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.tradeId))
            {
                return;
            }

            MMOTradeSessionSnapshot clone = Clone(snapshot);
            int index = Trades.FindIndex(candidate => candidate != null && candidate.tradeId == clone.tradeId);
            if (index >= 0)
            {
                if (Trades[index].revision > clone.revision)
                {
                    return;
                }

                Trades[index] = clone;
            }
            else
            {
                Trades.Add(clone);
            }

            Changed?.Invoke();
        }

        public static MMODuelSessionSnapshot FindDuel(string duelId)
        {
            return Duels.Find(candidate => candidate != null && candidate.duelId == duelId);
        }

        public static MMOTradeSessionSnapshot FindTrade(string tradeId)
        {
            return Trades.Find(candidate => candidate != null && candidate.tradeId == tradeId);
        }

        public static MMODuelSessionSnapshot FindCurrentDuel(string characterId)
        {
            return Duels.FindLast(candidate => candidate != null
                && candidate.Includes(characterId)
                && (candidate.status == MMODuelSessionStatus.Pending
                    || candidate.status == MMODuelSessionStatus.Countdown
                    || candidate.status == MMODuelSessionStatus.Active));
        }

        public static MMOTradeSessionSnapshot FindCurrentTrade(string characterId)
        {
            return Trades.FindLast(candidate => candidate != null
                && candidate.Includes(characterId)
                && candidate.status == MMOTradeSessionStatus.Open);
        }

        public static bool AreActivelyDueling(string firstCharacterId, string secondCharacterId)
        {
            return Duels.Exists(candidate => candidate != null
                && candidate.status == MMODuelSessionStatus.Active
                && candidate.Includes(firstCharacterId)
                && candidate.Includes(secondCharacterId));
        }

        public static string CreateNetworkSnapshotJson()
        {
            return JsonUtility.ToJson(new MMOPlayerInteractionNetworkSnapshot
            {
                duels = new List<MMODuelSessionSnapshot>(Duels),
                trades = new List<MMOTradeSessionSnapshot>(Trades)
            }, false);
        }

        public static void ApplyNetworkSnapshotJson(string json)
        {
            MMOPlayerInteractionNetworkSnapshot snapshot = string.IsNullOrWhiteSpace(json)
                ? new MMOPlayerInteractionNetworkSnapshot()
                : JsonUtility.FromJson<MMOPlayerInteractionNetworkSnapshot>(json) ?? new MMOPlayerInteractionNetworkSnapshot();
            Duels.Clear();
            Trades.Clear();
            foreach (MMODuelSessionSnapshot duel in snapshot.duels ?? new List<MMODuelSessionSnapshot>())
            {
                if (duel != null)
                {
                    Duels.Add(Clone(duel));
                }
            }

            foreach (MMOTradeSessionSnapshot trade in snapshot.trades ?? new List<MMOTradeSessionSnapshot>())
            {
                if (trade != null)
                {
                    Trades.Add(Clone(trade));
                }
            }

            Changed?.Invoke();
        }

        public static void Prune(long nowUtcTicks, long terminalRetentionTicks)
        {
            int removed = Duels.RemoveAll(candidate => candidate == null
                || (candidate.status != MMODuelSessionStatus.Pending
                    && candidate.status != MMODuelSessionStatus.Countdown
                    && candidate.status != MMODuelSessionStatus.Active
                    && nowUtcTicks - candidate.stateChangedUtcTicks > terminalRetentionTicks));
            removed += Trades.RemoveAll(candidate => candidate == null
                || (candidate.status != MMOTradeSessionStatus.Open
                    && nowUtcTicks - candidate.stateChangedUtcTicks > terminalRetentionTicks));
            if (removed > 0)
            {
                Changed?.Invoke();
            }
        }

        public static MMODuelSessionSnapshot Clone(MMODuelSessionSnapshot value)
        {
            return CloneSerializable(value);
        }

        public static MMOTradeSessionSnapshot Clone(MMOTradeSessionSnapshot value)
        {
            return CloneSerializable(value);
        }

        private static T CloneSerializable<T>(T value) where T : class
        {
            return value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value, false));
        }
    }
}
