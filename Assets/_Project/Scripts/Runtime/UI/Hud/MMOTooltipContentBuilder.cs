using System.Collections.Generic;
using System.Text;
using RPGClone.Abilities;
using RPGClone.Buffs;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Inventory;
using RPGClone.Quests;
using RPGClone.Services;
using UnityEngine;

namespace RPGClone.UI
{
    public static class MMOTooltipContentBuilder
    {
        public static MMOTooltipContent BuildItem(
            MMOItemDefinition item,
            MMOQuestLog questLog = null,
            MMOTooltipTheme theme = null)
        {
            if (item == null)
            {
                return null;
            }

            theme ??= MMOTooltipTheme.LoadDefault();
            MMOTooltipContent content = new(item.DisplayName, GetQualityColor(item.Quality));

            if (item.IsEquipment)
            {
                AddEquipmentDetails(content, item, theme);
            }
            else
            {
                content.Add(GetItemCategoryLabel(item), theme.BodyFontSize, FontStyle.Normal, theme.PrimaryText);
            }

            if (item.IsConsumable)
            {
                string effect = BuildConsumableEffectText(item);
                content.Add(effect, theme.BodyFontSize, FontStyle.Normal, theme.PositiveText, theme.SectionSpacing);
            }

            foreach (string questLine in BuildQuestLines(item, questLog))
            {
                content.Add(questLine, theme.BodyFontSize, FontStyle.Normal, theme.DescriptionText, theme.SectionSpacing);
            }

            if (!string.IsNullOrWhiteSpace(item.Description))
            {
                content.Add(item.Description, theme.BodyFontSize, FontStyle.Normal, theme.DescriptionText, theme.SectionSpacing);
            }

            if (item.VendorValueCopper > 0)
            {
                content.Add(
                    $"Sell Price: {MMOCurrencyWallet.FormatCopper(item.VendorValueCopper)}",
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.PriceText,
                    theme.SectionSpacing);
            }

            return content;
        }

        public static MMOTooltipContent BuildAbility(
            MMOAbilityDefinition ability,
            MMOCharacterIdentity caster,
            MMOTooltipTheme theme = null)
        {
            if (ability == null)
            {
                return null;
            }

            theme ??= MMOTooltipTheme.LoadDefault();
            MMOTooltipContent content = new(ability.DisplayName, theme.PrimaryText);
            content.AddDouble(
                ability.ManaCost > 0 ? $"{ability.ManaCost} Mana" : string.Empty,
                ability.Range > 0f ? $"{ability.Range:0.#} yd range" : string.Empty,
                theme.BodyFontSize,
                FontStyle.Normal,
                theme.PrimaryText);
            content.AddDouble(
                FormatCastTime(ability),
                ability.CooldownSeconds > 0f ? $"{FormatDuration(ability.CooldownSeconds)} cooldown" : string.Empty,
                theme.BodyFontSize,
                FontStyle.Normal,
                theme.PrimaryText);

            if (!string.IsNullOrWhiteSpace(ability.Description))
            {
                content.Add(
                    ability.Description,
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.DescriptionText,
                    theme.SectionSpacing);
            }

            bool firstEffect = string.IsNullOrWhiteSpace(ability.Description);
            foreach (MMOAbilityEffectDefinition effect in ability.Effects)
            {
                string summary = BuildAbilityEffectSummary(ability, effect, caster);
                if (string.IsNullOrWhiteSpace(summary))
                {
                    continue;
                }

                content.Add(
                    summary,
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.DescriptionText,
                    firstEffect ? theme.SectionSpacing : theme.LineSpacing);
                firstEffect = false;
            }

            return content;
        }

        public static MMOTooltipContent BuildBuff(MMOActiveBuff buff, MMOTooltipTheme theme = null)
        {
            if (buff == null)
            {
                return null;
            }

            theme ??= MMOTooltipTheme.LoadDefault();
            Color titleColor = buff.IsHarmful ? theme.NegativeText : theme.PrimaryText;
            MMOTooltipContent content = new(buff.DisplayName, titleColor);
            content.Add(buff.Description, theme.BodyFontSize, FontStyle.Normal, theme.PrimaryText);
            content.Add(
                $"Remaining: {FormatDuration(buff.RemainingSeconds)}",
                theme.BodyFontSize,
                FontStyle.Normal,
                buff.IsNearExpiry ? theme.NegativeText : theme.SecondaryText,
                theme.SectionSpacing);
            return content;
        }

        public static string BuildAbilityEffectSummary(
            MMOAbilityDefinition ability,
            MMOAbilityEffectDefinition effect,
            MMOCharacterIdentity caster)
        {
            if (effect == null)
            {
                return string.Empty;
            }

            MMOAbilityAmountRange amount = ResolveAmountRange(ability, effect, caster);
            string amountText = FormatAmount(amount);
            string school = MMOUiFactory.FormatEnumLabel(effect.DamageSchool);
            string area = ability != null && ability.HasArea
                ? $" to targets within {ability.AreaRadius:0.#} yd"
                : string.Empty;

            switch (effect.EffectType)
            {
                case MMOAbilityEffectType.Damage:
                    return $"Deals {amountText} {school} damage{area}.";
                case MMOAbilityEffectType.Heal:
                    return $"Heals {amountText} health{area}.";
                case MMOAbilityEffectType.Charge:
                    return $"Charges the target and deals {amountText} {school} damage.";
                case MMOAbilityEffectType.PeriodicDamage:
                    string stackText = effect.StackLimit > 1
                        ? $" Stacks up to {effect.StackLimit} times."
                        : string.Empty;
                    return $"Deals {amountText} {school} damage over {FormatDuration(effect.DurationSeconds)}{area}.{stackText}";
                case MMOAbilityEffectType.TemporaryStatModifier:
                    return BuildTemporaryModifierSummary(effect);
                default:
                    return string.Empty;
            }
        }

        public static string FormatAmount(MMOAbilityAmountRange amount)
        {
            return amount.IsRange ? $"{amount.Minimum}-{amount.Maximum}" : amount.Minimum.ToString();
        }

        private static void AddEquipmentDetails(
            MMOTooltipContent content,
            MMOItemDefinition item,
            MMOTooltipTheme theme)
        {
            if (item.IsWeapon)
            {
                content.AddDouble(
                    item.IsTwoHandedWeapon ? "Two-Hand" : "Main Hand",
                    MMOUiFactory.FormatEnumLabel(item.WeaponType),
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.PrimaryText);
                content.AddDouble(
                    $"{item.WeaponMinDamage:0}-{item.WeaponMaxDamage:0} Damage",
                    $"Speed {item.WeaponSpeedSeconds:0.00}",
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.PrimaryText);
                content.Add(
                    $"({item.WeaponDps:0.0} damage per second)",
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.PrimaryText);
            }
            else if (item.IsShield)
            {
                content.AddDouble(
                    "Off Hand",
                    "Shield",
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.PrimaryText);
                content.Add($"{item.StatBonuses.BaseArmor} Armor", theme.BodyFontSize, FontStyle.Normal, theme.PrimaryText);
                content.Add($"{item.ShieldBlockValue} Block", theme.BodyFontSize, FontStyle.Normal, theme.PrimaryText);
            }
            else
            {
                content.AddDouble(
                    FormatEquipmentSlot(item.EquipmentSlot),
                    MMOUiFactory.FormatEnumLabel(item.ArmorWeight),
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.PrimaryText);
            }

            string classLine = FormatAllowedClasses(item);
            if (!string.IsNullOrWhiteSpace(classLine))
            {
                content.Add(classLine, theme.BodyFontSize, FontStyle.Normal, theme.PrimaryText, theme.SectionSpacing);
            }

            bool hasStats = false;
            foreach (string statLine in BuildStatLines(item.StatBonuses, item.IsShield))
            {
                content.Add(
                    statLine,
                    theme.BodyFontSize,
                    FontStyle.Normal,
                    theme.PositiveText,
                    hasStats ? theme.LineSpacing : theme.SectionSpacing);
                hasStats = true;
            }
        }

        private static MMOAbilityAmountRange ResolveAmountRange(
            MMOAbilityDefinition ability,
            MMOAbilityEffectDefinition effect,
            MMOCharacterIdentity caster)
        {
            bool usesCombatWeaponResolution = effect.EffectType == MMOAbilityEffectType.Damage
                && effect.DamageSchool == MMODamageSchool.Physical
                && (ability != null && ability.IsAutoAttack
                    || effect.AmountSource == MMOAbilityAmountSource.WeaponDamage);
            return usesCombatWeaponResolution
                ? MMOCombatResolver.CalculateWeaponDamageRange(caster, effect)
                : effect.CalculateAmountRange(caster);
        }

        private static string BuildTemporaryModifierSummary(MMOAbilityEffectDefinition effect)
        {
            List<string> clauses = new();
            if (effect.AttackPowerBonus > 0)
            {
                clauses.Add($"increases attack power by {effect.AttackPowerBonus}");
            }

            AddMultiplierClause(clauses, "attack power", effect.AttackPowerMultiplier);
            AddMultiplierClause(clauses, "attack speed", effect.AttackSpeedMultiplier);
            AddMultiplierClause(clauses, "health regeneration", effect.HealthRegenMultiplier);
            AddMultiplierClause(clauses, "mana regeneration", effect.ManaRegenMultiplier);
            AddMultiplierClause(clauses, "movement speed", effect.MovementSpeedMultiplier);
            if (effect.DamageTakenAsManaPercent > 0f)
            {
                clauses.Add($"converts {effect.DamageTakenAsManaPercent * 100f:0.#}% of damage taken into mana");
            }

            if (clauses.Count == 0)
            {
                return $"Applies an effect for {FormatDuration(effect.DurationSeconds)}.";
            }

            string joined = JoinClauses(clauses);
            StringBuilder result = new();
            result.Append(char.ToUpperInvariant(joined[0]));
            result.Append(joined.Substring(1));
            result.Append($" for {FormatDuration(effect.DurationSeconds)}.");
            return result.ToString();
        }

        private static void AddMultiplierClause(List<string> clauses, string label, float multiplier)
        {
            float percentage = (multiplier - 1f) * 100f;
            if (Mathf.Abs(percentage) < 0.05f)
            {
                return;
            }

            clauses.Add(
                percentage > 0f
                    ? $"increases {label} by {percentage:0.#}%"
                    : $"reduces {label} by {Mathf.Abs(percentage):0.#}%");
        }

        private static string JoinClauses(IReadOnlyList<string> clauses)
        {
            if (clauses.Count == 1)
            {
                return clauses[0];
            }

            if (clauses.Count == 2)
            {
                return $"{clauses[0]} and {clauses[1]}";
            }

            return $"{string.Join(", ", new List<string>(clauses).GetRange(0, clauses.Count - 1))}, and {clauses[clauses.Count - 1]}";
        }

        private static string GetItemCategoryLabel(MMOItemDefinition item)
        {
            if (item.IsContainer)
            {
                return $"{item.ContainerSlotCount} Slot Bag";
            }

            return item.ItemType switch
            {
                MMOItemType.Material => "Crafting Reagent",
                MMOItemType.Quest => "Quest Item",
                MMOItemType.Consumable => MMOUiFactory.FormatEnumLabel(item.ConsumableType),
                MMOItemType.Trash => "Trash",
                _ => MMOUiFactory.FormatEnumLabel(item.ItemType)
            };
        }

        private static string FormatEquipmentSlot(MMOEquipmentSlotType slot)
        {
            return slot switch
            {
                MMOEquipmentSlotType.Finger1 or MMOEquipmentSlotType.Finger2 => "Finger",
                MMOEquipmentSlotType.Trinket1 or MMOEquipmentSlotType.Trinket2 => "Trinket",
                MMOEquipmentSlotType.MainHand => "Main Hand",
                MMOEquipmentSlotType.OffHand => "Off Hand",
                _ => MMOUiFactory.FormatEnumLabel(slot)
            };
        }

        private static string FormatAllowedClasses(MMOItemDefinition item)
        {
            if (item.AllowedClasses == null || item.AllowedClasses.Count == 0)
            {
                return string.Empty;
            }

            List<string> classNames = new();
            foreach (MMOPlayableClass playableClass in item.AllowedClasses)
            {
                classNames.Add(MMOUiFactory.FormatEnumLabel(playableClass));
            }

            return $"Classes: {string.Join(", ", classNames)}";
        }

        private static IEnumerable<string> BuildStatLines(MMOCharacterStats stats, bool armorAlreadyShown)
        {
            if (stats == null)
            {
                yield break;
            }

            if (stats.Stamina > 0) yield return $"+{stats.Stamina} Stamina";
            if (stats.Strength > 0) yield return $"+{stats.Strength} Strength";
            if (stats.Agility > 0) yield return $"+{stats.Agility} Agility";
            if (stats.Intellect > 0) yield return $"+{stats.Intellect} Intellect";
            if (stats.Spirit > 0) yield return $"+{stats.Spirit} Spirit";
            if (!armorAlreadyShown && stats.BaseArmor > 0) yield return $"{stats.BaseArmor} Armor";
            if (stats.BaseAttackPower > 0) yield return $"+{stats.BaseAttackPower} Attack Power";
            if (stats.BaseSpellPower > 0) yield return $"+{stats.BaseSpellPower} Spell Power";
        }

        private static string BuildConsumableEffectText(MMOItemDefinition item)
        {
            if (item.ExperienceRewardAmount > 0)
            {
                return $"Use: Grants {item.ExperienceRewardAmount} experience.";
            }

            List<string> effects = new();
            if (item.RestoreHealthAmount > 0)
            {
                effects.Add($"{item.RestoreHealthAmount} health");
            }

            if (item.RestoreManaAmount > 0)
            {
                effects.Add($"{item.RestoreManaAmount} mana");
            }

            if (effects.Count == 0)
            {
                return string.Empty;
            }

            string stationary = item.RequiresStationary ? " Must remain stationary." : string.Empty;
            return $"Use: Restores {string.Join(" and ", effects)} over {item.ConsumeDurationSeconds:0.#} sec.{stationary}";
        }

        private static IEnumerable<string> BuildQuestLines(MMOItemDefinition item, MMOQuestLog questLog)
        {
            if (item.ItemType != MMOItemType.Quest)
            {
                yield break;
            }

            if (questLog == null)
            {
                yield break;
            }

            foreach (MMOQuestRuntimeState state in questLog.ActiveQuests)
            {
                MMOQuestDefinition quest = state.Quest;
                if (quest == null)
                {
                    continue;
                }

                foreach (MMOQuestObjectiveDefinition objective in quest.Objectives)
                {
                    if (objective.RequiredItem == item || objective.UsableItem == item)
                    {
                        yield return $"Quest: {quest.DisplayName}";
                        break;
                    }
                }
            }
        }

        private static string FormatCastTime(MMOAbilityDefinition ability)
        {
            if (ability.IsChanneled)
            {
                return $"{ability.CastTimeSeconds:0.#} sec channel";
            }

            return ability.CastTimeSeconds > 0f
                ? $"{ability.CastTimeSeconds:0.#} sec cast"
                : "Instant";
        }

        private static string FormatDuration(float seconds)
        {
            if (seconds >= 60f)
            {
                float minutes = seconds / 60f;
                return Mathf.Approximately(minutes, Mathf.Round(minutes))
                    ? $"{Mathf.RoundToInt(minutes)} min"
                    : $"{minutes:0.#} min";
            }

            return $"{seconds:0.#} sec";
        }

        private static Color GetQualityColor(MMOItemQuality quality)
        {
            return quality switch
            {
                MMOItemQuality.Common => Color.white,
                MMOItemQuality.Uncommon => new Color(0.12f, 1f, 0f, 1f),
                MMOItemQuality.Rare => new Color(0f, 0.44f, 0.87f, 1f),
                MMOItemQuality.Epic => new Color(0.64f, 0.21f, 0.93f, 1f),
                _ => new Color(0.62f, 0.62f, 0.62f, 1f)
            };
        }
    }
}
