using System.Collections.Generic;
using RPGClone.CharacterSelection;
using RPGClone.Inventory;

namespace RPGClone.PlayerInteraction
{
    public static class MMOTradeTransaction
    {
        public static bool TryBuildSettlement(
            MMOCharacterSaveData initiator,
            MMOCharacterSaveData recipient,
            MMOTradeSessionSnapshot trade,
            out List<MMOInventorySlotSaveData> initiatorResult,
            out List<MMOInventorySlotSaveData> recipientResult,
            out int initiatorCopperResult,
            out int recipientCopperResult,
            out string failureReason)
        {
            initiatorResult = new List<MMOInventorySlotSaveData>();
            recipientResult = new List<MMOInventorySlotSaveData>();
            initiatorCopperResult = 0;
            recipientCopperResult = 0;
            failureReason = string.Empty;
            if (initiator == null || recipient == null || trade == null)
            {
                failureReason = "Trade participants are unavailable.";
                return false;
            }

            if (trade.initiatorCopper < 0 || trade.recipientCopper < 0
                || initiator.copper < trade.initiatorCopper
                || recipient.copper < trade.recipientCopper)
            {
                failureReason = "A player no longer has the offered money.";
                return false;
            }

            List<MMOItemStackData> initiatorSlots = BuildSlots(initiator, out string initiatorInventoryError);
            List<MMOItemStackData> recipientSlots = BuildSlots(recipient, out string recipientInventoryError);
            if (initiatorSlots == null || recipientSlots == null)
            {
                failureReason = !string.IsNullOrWhiteSpace(initiatorInventoryError)
                    ? initiatorInventoryError
                    : recipientInventoryError;
                return false;
            }

            if (!TryRemoveOffers(initiatorSlots, trade.initiatorOffers, out List<MMOItemStackData> toRecipient, out failureReason)
                || !TryRemoveOffers(recipientSlots, trade.recipientOffers, out List<MMOItemStackData> toInitiator, out failureReason))
            {
                return false;
            }

            if (!TryAddAll(initiatorSlots, toInitiator) || !TryAddAll(recipientSlots, toRecipient))
            {
                failureReason = "A player does not have enough inventory space for this trade.";
                return false;
            }

            initiatorResult = ToSaveData(initiatorSlots);
            recipientResult = ToSaveData(recipientSlots);
            initiatorCopperResult = initiator.copper - trade.initiatorCopper + trade.recipientCopper;
            recipientCopperResult = recipient.copper - trade.recipientCopper + trade.initiatorCopper;
            return true;
        }

        public static bool OfferMatchesInventory(
            MMOCharacterSaveData character,
            IReadOnlyList<MMOTradeOfferEntry> offers,
            out string failureReason)
        {
            failureReason = string.Empty;
            List<MMOItemStackData> slots = BuildSlots(character, out failureReason);
            return slots != null && TryRemoveOffers(slots, offers, out _, out failureReason);
        }

        private static List<MMOItemStackData> BuildSlots(MMOCharacterSaveData character, out string failureReason)
        {
            failureReason = string.Empty;
            if (character == null)
            {
                failureReason = "Character inventory is unavailable.";
                return null;
            }

            int capacity = MMOInventoryContainer.DefaultBackpackSlotCount;
            foreach (string bagItemId in character.equippedBagItemIds ?? new List<string>())
            {
                MMOItemDefinition bag = ResolveItem(bagItemId);
                if (bag != null && bag.IsContainer)
                {
                    capacity += bag.ContainerSlotCount;
                }
            }

            List<MMOItemStackData> slots = new(capacity);
            for (int i = 0; i < capacity; i++)
            {
                slots.Add(new MMOItemStackData());
            }

            HashSet<int> occupiedSlotIndices = new();
            foreach (MMOInventorySlotSaveData savedSlot in character.inventory ?? new List<MMOInventorySlotSaveData>())
            {
                if (savedSlot == null)
                {
                    continue;
                }

                if (savedSlot.slotIndex < 0 || savedSlot.slotIndex >= slots.Count
                    || savedSlot.quantity <= 0 || !occupiedSlotIndices.Add(savedSlot.slotIndex))
                {
                    failureReason = "A character inventory contains an invalid or duplicate slot.";
                    return null;
                }

                MMOItemDefinition item = ResolveItem(savedSlot.itemId);
                if (item == null)
                {
                    failureReason = $"Item '{savedSlot.itemId}' could not be resolved by the host.";
                    return null;
                }

                if (savedSlot.quantity > item.MaxStackSize)
                {
                    failureReason = $"The {item.DisplayName} stack is larger than the item stack limit.";
                    return null;
                }

                slots[savedSlot.slotIndex] = new MMOItemStackData(item, savedSlot.quantity);
            }

            return slots;
        }

        private static bool TryRemoveOffers(
            List<MMOItemStackData> slots,
            IReadOnlyList<MMOTradeOfferEntry> offers,
            out List<MMOItemStackData> removed,
            out string failureReason)
        {
            removed = new List<MMOItemStackData>();
            failureReason = string.Empty;
            HashSet<int> sourceSlots = new();
            foreach (MMOTradeOfferEntry offer in offers ?? (IReadOnlyList<MMOTradeOfferEntry>)System.Array.Empty<MMOTradeOfferEntry>())
            {
                if (offer == null || offer.offerSlotIndex < 0 || offer.offerSlotIndex >= MMOPlayerInteractionAuthority.TradeSlotCount)
                {
                    failureReason = "A trade offer contains an invalid slot.";
                    return false;
                }

                if (offer.sourceInventorySlotIndex < 0 || offer.sourceInventorySlotIndex >= slots.Count
                    || offer.quantity <= 0 || !sourceSlots.Add(offer.sourceInventorySlotIndex))
                {
                    failureReason = "A trade offer references an invalid or duplicate inventory slot.";
                    return false;
                }

                MMOItemStackData source = slots[offer.sourceInventorySlotIndex];
                if (source.Item == null || source.Item.ItemId != offer.itemId || source.Quantity < offer.quantity)
                {
                    failureReason = "An offered item is no longer in the expected inventory slot.";
                    return false;
                }

                if (source.Item.ItemType == MMOItemType.Quest)
                {
                    failureReason = $"{source.Item.DisplayName} cannot be traded.";
                    return false;
                }

                removed.Add(new MMOItemStackData(source.Item, offer.quantity));
                source.Quantity -= offer.quantity;
                if (source.Quantity <= 0)
                {
                    slots[offer.sourceInventorySlotIndex] = new MMOItemStackData();
                }
            }

            return true;
        }

        private static bool TryAddAll(List<MMOItemStackData> slots, List<MMOItemStackData> incoming)
        {
            foreach (MMOItemStackData stack in incoming)
            {
                int remaining = stack.Quantity;
                for (int i = 0; i < slots.Count && remaining > 0; i++)
                {
                    MMOItemStackData existing = slots[i];
                    if (existing.Item != stack.Item || existing.Quantity >= stack.Item.MaxStackSize)
                    {
                        continue;
                    }

                    int accepted = System.Math.Min(remaining, stack.Item.MaxStackSize - existing.Quantity);
                    existing.Quantity += accepted;
                    remaining -= accepted;
                }

                for (int i = 0; i < slots.Count && remaining > 0; i++)
                {
                    if (slots[i].Item != null)
                    {
                        continue;
                    }

                    int accepted = System.Math.Min(remaining, stack.Item.MaxStackSize);
                    slots[i] = new MMOItemStackData(stack.Item, accepted);
                    remaining -= accepted;
                }

                if (remaining > 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<MMOInventorySlotSaveData> ToSaveData(List<MMOItemStackData> slots)
        {
            List<MMOInventorySlotSaveData> result = new();
            for (int i = 0; i < slots.Count; i++)
            {
                MMOItemStackData stack = slots[i];
                if (stack.Item == null || stack.Quantity <= 0)
                {
                    continue;
                }

                result.Add(new MMOInventorySlotSaveData
                {
                    slotIndex = i,
                    itemId = stack.Item.ItemId,
                    quantity = stack.Quantity
                });
            }

            return result;
        }

        private static MMOItemDefinition ResolveItem(string itemId)
        {
            MMOItemDefinition item = MMOItemCatalog.FindLoadedById(itemId);
            if (item != null)
            {
                return item;
            }

            foreach (MMOItemCatalog catalog in UnityEngine.Resources.LoadAll<MMOItemCatalog>(string.Empty))
            {
                item = catalog != null ? catalog.FindById(itemId) : null;
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        private sealed class MMOItemStackData
        {
            public MMOItemDefinition Item;
            public int Quantity;

            public MMOItemStackData()
            {
            }

            public MMOItemStackData(MMOItemDefinition item, int quantity)
            {
                Item = item;
                Quantity = quantity;
            }
        }
    }
}
