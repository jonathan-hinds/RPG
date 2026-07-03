using System;
using System.Collections.Generic;
using RPGClone.Enemies;
using RPGClone.Inventory;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.Loot
{
    public static class MMOPersonalLootService
    {
        public static MMOCorpseLootState GenerateCorpseLoot(
            string enemySpawnId,
            MMOEnemyDefinition enemyDefinition,
            MMOPlayerParticipant sourceParticipant,
            Vector3 eventPosition,
            float range)
        {
            MMOCorpseLootState corpseState = new()
            {
                sessionId = MMOGameplaySessionService.SessionId,
                corpseId = enemySpawnId,
                enemySpawnId = enemySpawnId,
                updatedUtcTicks = DateTime.UtcNow.Ticks
            };

            List<MMOPlayerParticipant> recipients = MMORewardEligibilityService.GetEligiblePartyMembers(sourceParticipant, eventPosition, range);
            foreach (MMOPlayerParticipant recipient in recipients)
            {
                if (!recipient.IsValid)
                {
                    continue;
                }

                List<MMOItemStack> rolledLoot = enemyDefinition != null && enemyDefinition.LootTable != null
                    ? enemyDefinition.LootTable.GenerateLoot(recipient.GameObject)
                    : new List<MMOItemStack>();
                corpseState.personalLoot.Add(ToPersonalLootState(recipient, rolledLoot));
            }

            return corpseState;
        }

        public static void PublishCorpseLoot(MMOCorpseLootState state)
        {
            if (state == null)
            {
                return;
            }

            state.updatedUtcTicks = DateTime.UtcNow.Ticks;
            RPGClone.Multiplayer.MMOLocalSharedSessionStore.UpsertCorpseLootSnapshot(state);
        }

        public static List<MMOItemStack> ToItemStacks(MMOPersonalLootState state)
        {
            List<MMOItemStack> stacks = new();
            if (state == null || state.items == null || state.looted)
            {
                return stacks;
            }

            foreach (MMOPersonalLootItemState itemState in state.items)
            {
                if (itemState == null || string.IsNullOrWhiteSpace(itemState.itemId) || itemState.quantity <= 0)
                {
                    continue;
                }

                MMOItemDefinition item = ResolveItem(itemState.itemId);
                if (item != null)
                {
                    stacks.Add(new MMOItemStack(item, itemState.quantity));
                }
            }

            return stacks;
        }

        public static MMOItemDefinition ResolveItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            MMOItemDefinition[] items = Resources.FindObjectsOfTypeAll<MMOItemDefinition>();
            foreach (MMOItemDefinition item in items)
            {
                if (item != null && item.ItemId == itemId)
                {
                    return item;
                }
            }

            MMOItemCatalog[] catalogs = Resources.FindObjectsOfTypeAll<MMOItemCatalog>();
            foreach (MMOItemCatalog catalog in catalogs)
            {
                MMOItemDefinition item = catalog != null ? catalog.FindById(itemId) : null;
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        private static MMOPersonalLootState ToPersonalLootState(MMOPlayerParticipant recipient, IEnumerable<MMOItemStack> stacks)
        {
            MMOPersonalLootState state = new()
            {
                characterId = recipient.CharacterId,
                participantId = recipient.ParticipantId
            };

            if (stacks != null)
            {
                foreach (MMOItemStack stack in stacks)
                {
                    if (stack != null && !stack.IsEmpty)
                    {
                        state.items.Add(new MMOPersonalLootItemState(stack));
                    }
                }
            }

            state.looted = !state.HasLoot;
            return state;
        }
    }
}
