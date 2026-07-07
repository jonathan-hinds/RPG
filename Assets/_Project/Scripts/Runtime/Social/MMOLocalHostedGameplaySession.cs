using System;
using RPGClone.Services;
using UnityEngine.SceneManagement;

namespace RPGClone.Social
{
    public sealed class MMOLocalHostedGameplaySession : IActiveGameplaySession
    {
        private readonly int capacity;
        private readonly int participantCount;

        public MMOLocalHostedGameplaySession(string sessionId, bool joinsAllowed, int capacity, int participantCount)
        {
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? "local-hosted-session" : sessionId;
            JoinsAllowed = joinsAllowed;
            this.capacity = Math.Max(1, capacity);
            this.participantCount = Math.Max(1, participantCount);
        }

        public string SessionId { get; }
        public bool IsHosting => true;
        public bool JoinsAllowed { get; }

        public MMOSessionPresenceRecord CreatePresenceRecord(string playerId, string characterId, string characterName)
        {
            long now = DateTime.UtcNow.Ticks;
            return new MMOSessionPresenceRecord
            {
                hostPlayerId = playerId ?? string.Empty,
                hostCharacterId = characterId ?? string.Empty,
                hostCharacterName = characterName ?? string.Empty,
                sessionId = SessionId,
                currentSceneName = SceneManager.GetActiveScene().name,
                capacity = capacity,
                participantCount = participantCount,
                joinsAllowed = JoinsAllowed,
                createdUtcTicks = now,
                updatedUtcTicks = now,
                privateConnectionData = MMOGameplaySessionService.JoinCode
            };
        }

        public static MMOLocalHostedGameplaySession FromCurrentGameplaySession()
        {
            MMOGameplaySessionService.EnsureLocalHostedSession();
            int participantCount = MMOGameplaySessionService.Players.Participants.Count;
            return new MMOLocalHostedGameplaySession(
                MMOGameplaySessionService.SessionId,
                MMOGameplaySessionService.IsLocalHostedSession,
                5,
                Math.Max(1, participantCount));
        }
    }
}
