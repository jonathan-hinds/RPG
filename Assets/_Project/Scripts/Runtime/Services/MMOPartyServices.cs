using System;
using System.Collections.Generic;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using RPGClone.Quests;
using UnityEngine;

namespace RPGClone.Services
{
    [Serializable]
    public sealed class MMOPartyMember
    {
        public string participantId;
        public string characterId;
        public string displayName;
        public bool isLeader;
        public bool isLocal;
        public bool isConnected = true;

        public MMOPartyMember()
        {
        }

        public MMOPartyMember(MMOPlayerParticipant participant, bool leader)
        {
            participantId = participant.ParticipantId;
            characterId = participant.CharacterId;
            displayName = participant.Identity != null ? participant.Identity.DisplayName : participant.CharacterId;
            isLeader = leader;
            isLocal = participant.IsLocal;
            isConnected = participant.IsValid;
        }
    }

    [Serializable]
    public sealed class MMOPartySnapshot
    {
        public string sessionId;
        public string partyId;
        public string leaderCharacterId;
        public List<MMOPartyMember> members = new();

        public bool ContainsCharacter(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                return false;
            }

            foreach (MMOPartyMember member in members)
            {
                if (member != null && member.characterId == characterId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class MMOPartyService
    {
        private const int DefaultMaxPartySize = 5;

        public int MaxPartySize { get; set; } = DefaultMaxPartySize;

        public MMOPartySnapshot GetCurrentParty()
        {
            MMOPartySnapshot snapshot = new()
            {
                sessionId = MMOGameplaySessionService.SessionId ?? string.Empty,
                partyId = string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId)
                    ? "solo-party"
                    : $"{MMOGameplaySessionService.SessionId}:party"
            };

            List<MMOPlayerParticipant> participants = new(MMOGameplaySessionService.Players.Participants);
            if (participants.Count == 0 && MMOGameplaySessionService.LocalPlayer.Identity != null)
            {
                participants.Add(new MMOPlayerParticipant(
                    MMOGameplaySessionService.LocalPlayer.ParticipantId,
                    MMOGameplaySessionService.LocalPlayer.CharacterId,
                    true,
                    MMOGameplaySessionService.IsHostAuthority,
                    MMOGameplaySessionService.LocalPlayer.Identity));
            }

            MMOPlayerParticipant leader = default;
            foreach (MMOPlayerParticipant participant in participants)
            {
                if (participant.IsValid && participant.IsHostAuthority)
                {
                    leader = participant;
                    break;
                }
            }

            if (!leader.IsValid)
            {
                foreach (MMOPlayerParticipant participant in participants)
                {
                    if (participant.IsValid)
                    {
                        leader = participant;
                        break;
                    }
                }
            }

            snapshot.leaderCharacterId = leader.IsValid ? leader.CharacterId : MMOGameplaySessionService.HostCharacterId ?? string.Empty;
            int maxMembers = Mathf.Max(1, MaxPartySize);
            foreach (MMOPlayerParticipant participant in participants)
            {
                if (!participant.IsValid || snapshot.members.Count >= maxMembers)
                {
                    continue;
                }

                snapshot.members.Add(new MMOPartyMember(participant, participant.CharacterId == snapshot.leaderCharacterId));
            }

            return snapshot;
        }

        public bool ArePartyMembers(string firstCharacterId, string secondCharacterId)
        {
            if (string.IsNullOrWhiteSpace(firstCharacterId) || string.IsNullOrWhiteSpace(secondCharacterId))
            {
                return false;
            }

            MMOPartySnapshot party = GetCurrentParty();
            return party.ContainsCharacter(firstCharacterId) && party.ContainsCharacter(secondCharacterId);
        }
    }

    [Serializable]
    public sealed class MMORewardEligibilitySettings
    {
        [SerializeField, Min(1f)] private float partyCreditRadius = 80f;
        [SerializeField, Min(1f)] private float partyLootRadius = 80f;
        [SerializeField, Min(1f)] private float partyExperienceRadius = 80f;
        [SerializeField] private bool requireAlive = true;

        public float PartyCreditRadius => Mathf.Max(1f, partyCreditRadius);
        public float PartyLootRadius => Mathf.Max(1f, partyLootRadius);
        public float PartyExperienceRadius => Mathf.Max(1f, partyExperienceRadius);
        public bool RequireAlive => requireAlive;
    }

    public static class MMOPartyEligibilityService
    {
        public static bool IsEligibleForSharedEvent(
            MMOPlayerParticipant participant,
            Vector3 eventPosition,
            float range,
            bool requireAlive)
        {
            if (!participant.IsValid || string.IsNullOrWhiteSpace(MMOGameplaySessionService.SessionId))
            {
                return false;
            }

            GameObject participantObject = participant.GameObject;
            if (participantObject == null || !participantObject.activeInHierarchy)
            {
                return false;
            }

            if (range > 0f && participant.Transform != null)
            {
                float sqrRange = range * range;
                if ((participant.Transform.position - eventPosition).sqrMagnitude > sqrRange)
                {
                    return false;
                }
            }

            if (!requireAlive)
            {
                return true;
            }

            MMOCombatant combatant = participantObject.GetComponent<MMOCombatant>();
            return combatant == null || combatant.IsAlive;
        }
    }

    public static class MMORewardEligibilityService
    {
        public static List<MMOPlayerParticipant> GetEligiblePartyMembers(
            MMOPlayerParticipant eventSource,
            Vector3 eventPosition,
            float range,
            bool requireAlive = true)
        {
            List<MMOPlayerParticipant> eligible = new();
            MMOPartySnapshot party = MMOGameplaySessionService.Party.GetCurrentParty();
            foreach (MMOPlayerParticipant participant in MMOGameplaySessionService.Players.Participants)
            {
                if (!participant.IsValid || !party.ContainsCharacter(participant.CharacterId))
                {
                    continue;
                }

                if (MMOPartyEligibilityService.IsEligibleForSharedEvent(participant, eventPosition, range, requireAlive))
                {
                    eligible.Add(participant);
                }
            }

            if (eligible.Count == 0 && eventSource.IsValid)
            {
                eligible.Add(eventSource);
            }

            return eligible;
        }
    }

    public static class MMOPartyExperienceRewardService
    {
        public static void AwardEnemyExperience(
            MMOEnemyDefinition enemyDefinition,
            MMOPlayerParticipant sourceParticipant,
            Vector3 eventPosition,
            float range)
        {
            if (enemyDefinition == null || !MMOGameplaySessionService.IsHostAuthority)
            {
                return;
            }

            List<MMOPlayerParticipant> recipients = MMORewardEligibilityService.GetEligiblePartyMembers(sourceParticipant, eventPosition, range);
            if (recipients.Count == 0)
            {
                return;
            }

            foreach (MMOPlayerParticipant recipient in recipients)
            {
                if (!recipient.IsValid)
                {
                    continue;
                }

                int baseExperience = MMOExperienceScaling.CalculateMobExperience(enemyDefinition, recipient.Identity);
                int adjustedExperience = CalculatePartyExperience(baseExperience, recipients.Count);
                if (adjustedExperience <= 0)
                {
                    continue;
                }

                recipient.GameObject.GetComponent<MMOExperienceComponent>()?.AddExperience(adjustedExperience);
                RPGClone.Multiplayer.MMOSharedSessionState.PublishExperienceRewardEvent(
                    MMOGameplaySessionService.SessionId,
                    recipient.CharacterId,
                    enemyDefinition.name,
                    adjustedExperience,
                    recipient.IsLocal ? recipient.CharacterId : string.Empty);
            }
        }

        private static int CalculatePartyExperience(int baseExperience, int eligibleCount)
        {
            if (baseExperience <= 0)
            {
                return 0;
            }

            int count = Mathf.Max(1, eligibleCount);
            if (count == 1)
            {
                return baseExperience;
            }

            // Simple WoW-inspired approximation: split XP, then add a modest group bonus.
            float groupBonus = 1f + Mathf.Min(0.4f, (count - 1) * 0.1f);
            return Mathf.Max(1, Mathf.RoundToInt(baseExperience * groupBonus / count));
        }
    }

    public static class MMOPartyQuestCreditService
    {
        public static void AwardKillCredit(
            MMOEnemyDefinition enemyDefinition,
            string creatureId,
            string enemySpawnId,
            MMOPlayerParticipant sourceParticipant,
            Vector3 eventPosition,
            float range)
        {
            List<MMOPlayerParticipant> recipients = MMORewardEligibilityService.GetEligiblePartyMembers(sourceParticipant, eventPosition, range);
            foreach (MMOPlayerParticipant recipient in recipients)
            {
                MMOQuestLog questLog = recipient.GameObject != null ? recipient.GameObject.GetComponent<MMOQuestLog>() : null;
                if (questLog == null || !questLog.HasIncompleteKillObjective(enemyDefinition, creatureId))
                {
                    continue;
                }

                if (questLog.RecordCreatureKilled(enemyDefinition, creatureId))
                {
                    RPGClone.Multiplayer.MMOSharedSessionState.PublishQuestKillCreditEvent(
                        MMOGameplaySessionService.SessionId,
                        recipient.CharacterId,
                        enemySpawnId,
                        enemyDefinition != null ? enemyDefinition.name : string.Empty,
                        creatureId,
                        recipient.IsLocal ? recipient.CharacterId : string.Empty);
                }
            }
        }
    }
}
