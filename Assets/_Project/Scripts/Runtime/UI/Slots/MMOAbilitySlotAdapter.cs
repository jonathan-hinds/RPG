using RPGClone.Abilities;
using UnityEngine;

namespace RPGClone.UI
{
    public static class MMOAbilitySlotAdapter
    {
        public static MMOSlotPresentation Present(
            MMOAbilityDefinition ability,
            string keybinding = null,
            bool active = false,
            bool usable = true,
            bool inRange = true,
            bool attention = false,
            bool procGlow = false,
            float cooldownNormalized = 0f,
            string cooldownText = null,
            bool selected = false)
        {
            return ability == null
                ? new MMOSlotPresentation(
                    secondaryText: keybinding,
                    selected: selected,
                    usable: usable,
                    inRange: inRange)
                : new MMOSlotPresentation(
                    icon: ability.Icon,
                    secondaryText: keybinding,
                    centerText: ability.Icon == null ? BuildFallbackLabel(ability.DisplayName) : null,
                    iconTint: Color.white,
                    selected: selected,
                    active: active,
                    usable: usable,
                    inRange: inRange,
                    attention: attention,
                    procGlow: procGlow,
                    cooldownNormalized: cooldownNormalized,
                    cooldownText: cooldownText);
        }

        private static string BuildFallbackLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            string[] words = value.Split(' ');
            return words.Length == 1
                ? value[..Mathf.Min(2, value.Length)].ToUpperInvariant()
                : $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[^1][0])}";
        }
    }
}
