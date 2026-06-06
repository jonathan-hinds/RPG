using RPGClone.Buffs;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Inventory
{
    [RequireComponent(typeof(MMOCharacterIdentity))]
    public sealed class MMOConsumableEffectController : MonoBehaviour
    {
        private MMOCharacterIdentity identity;
        private MMOCharacterBuffController buffController;

        private void Awake()
        {
            identity = GetComponent<MMOCharacterIdentity>();
            buffController = GetComponent<MMOCharacterBuffController>();
            if (buffController == null)
            {
                buffController = gameObject.AddComponent<MMOCharacterBuffController>();
            }
        }

        public bool TryConsume(MMOItemDefinition item)
        {
            if (item == null || !item.IsConsumable || identity == null)
            {
                return false;
            }

            if (buffController == null)
            {
                buffController = gameObject.AddComponent<MMOCharacterBuffController>();
            }

            string effectText = item.RestoreHealthAmount > 0 && item.RestoreManaAmount > 0
                ? $"Restores {item.RestoreHealthAmount} health and {item.RestoreManaAmount} mana over {item.ConsumeDurationSeconds:0.#} sec."
                : item.RestoreHealthAmount > 0
                    ? $"Restores {item.RestoreHealthAmount} health over {item.ConsumeDurationSeconds:0.#} sec."
                    : $"Restores {item.RestoreManaAmount} mana over {item.ConsumeDurationSeconds:0.#} sec.";
            buffController.ApplyBuff(new MMOBuffApplication
            {
                BuffId = $"consumable_{item.ConsumableType.ToString().ToLowerInvariant()}",
                DisplayName = item.DisplayName,
                Description = effectText + (item.RequiresStationary ? " Must remain stationary." : string.Empty),
                Icon = item.Icon,
                DurationSeconds = item.ConsumeDurationSeconds,
                BreakOnMovement = item.RequiresStationary,
                RestoreHealthTotal = item.RestoreHealthAmount,
                RestoreManaTotal = item.RestoreManaAmount,
                TickSeconds = 1f
            });
            return true;
        }
    }
}
