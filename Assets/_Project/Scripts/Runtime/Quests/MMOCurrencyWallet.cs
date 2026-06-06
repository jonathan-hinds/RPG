using System;
using UnityEngine;

namespace RPGClone.Quests
{
    public sealed class MMOCurrencyWallet : MonoBehaviour
    {
        [SerializeField, Min(0)] private int copper;

        public event Action<MMOCurrencyWallet> Changed;
        public int Copper => Mathf.Max(0, copper);

        public void AddCopper(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            copper += amount;
            Changed?.Invoke(this);
        }

        public void SetCopper(int amount)
        {
            int clampedAmount = Mathf.Max(0, amount);
            if (copper == clampedAmount)
            {
                return;
            }

            copper = clampedAmount;
            Changed?.Invoke(this);
        }

        public bool TrySpendCopper(int amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            if (copper < amount)
            {
                return false;
            }

            copper -= amount;
            Changed?.Invoke(this);
            return true;
        }

        public static string FormatCopper(int copperAmount)
        {
            int safeCopper = Mathf.Max(0, copperAmount);
            int gold = safeCopper / 10000;
            int silver = safeCopper % 10000 / 100;
            int copperOnly = safeCopper % 100;
            if (gold > 0)
            {
                return $"{gold}g {silver}s {copperOnly}c";
            }

            return silver > 0 ? $"{silver}s {copperOnly}c" : $"{copperOnly}c";
        }
    }
}
