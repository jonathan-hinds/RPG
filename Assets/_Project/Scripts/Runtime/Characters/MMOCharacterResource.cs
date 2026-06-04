using System;
using UnityEngine;

namespace RPGClone.Characters
{
    [Serializable]
    public sealed class MMOCharacterResource
    {
        [SerializeField, Min(0)] private int currentValue;
        [SerializeField, Min(0)] private int maxValue = 100;

        public event Action Changed;

        public int CurrentValue => currentValue;
        public int MaxValue => maxValue;
        public float Normalized => maxValue <= 0 ? 0f : Mathf.Clamp01(currentValue / (float)maxValue);

        public MMOCharacterResource()
        {
            currentValue = maxValue;
        }

        public MMOCharacterResource(int maxValue)
        {
            Configure(maxValue, maxValue, false);
        }

        public void Configure(int newMaxValue, int newCurrentValue, bool notify = true)
        {
            maxValue = Mathf.Max(0, newMaxValue);
            currentValue = Mathf.Clamp(newCurrentValue, 0, maxValue);
            if (notify)
            {
                Changed?.Invoke();
            }
        }

        public void SetCurrent(int value)
        {
            int clampedValue = Mathf.Clamp(value, 0, maxValue);
            if (currentValue == clampedValue)
            {
                return;
            }

            currentValue = clampedValue;
            Changed?.Invoke();
        }

        public void SetMax(int value, bool keepPercentage = true)
        {
            int clampedMax = Mathf.Max(0, value);
            if (maxValue == clampedMax)
            {
                currentValue = Mathf.Clamp(currentValue, 0, maxValue);
                return;
            }

            float percentage = Normalized;
            maxValue = clampedMax;
            currentValue = keepPercentage ? Mathf.RoundToInt(maxValue * percentage) : Mathf.Clamp(currentValue, 0, maxValue);
            Changed?.Invoke();
        }
    }
}
