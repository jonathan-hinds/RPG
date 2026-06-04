using System;
using RPGClone.Abilities;
using UnityEngine.InputSystem;

namespace RPGClone.UI
{
    [Serializable]
    public sealed class MMOActionBarSlot
    {
        public MMOAbilityDefinition ability;
        public Key key = Key.None;
    }
}
