using System;
using System.Collections.Generic;
using RPGClone.Inventory;
using UnityEngine;

namespace RPGClone.Quests
{
    [Serializable]
    public sealed class MMOQuestRewardDefinition
    {
        [SerializeField, Min(0)] private int experience;
        [SerializeField, Min(0)] private int moneyCopper;
        [SerializeField] private List<MMOItemStack> guaranteedItems = new();
        [SerializeField] private List<MMOItemDefinition> choiceItems = new();

        public int Experience => Mathf.Max(0, experience);
        public int MoneyCopper => Mathf.Max(0, moneyCopper);
        public IReadOnlyList<MMOItemStack> GuaranteedItems => guaranteedItems;
        public IReadOnlyList<MMOItemDefinition> ChoiceItems => choiceItems;

        public void Configure(
            int newExperience,
            int newMoneyCopper,
            IEnumerable<MMOItemStack> newGuaranteedItems = null,
            IEnumerable<MMOItemDefinition> newChoiceItems = null)
        {
            experience = Mathf.Max(0, newExperience);
            moneyCopper = Mathf.Max(0, newMoneyCopper);
            guaranteedItems = newGuaranteedItems != null ? new List<MMOItemStack>(newGuaranteedItems) : new List<MMOItemStack>();
            choiceItems = newChoiceItems != null ? new List<MMOItemDefinition>(newChoiceItems) : new List<MMOItemDefinition>();
        }
    }
}
