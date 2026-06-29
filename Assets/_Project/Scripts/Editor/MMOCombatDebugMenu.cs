using System.Reflection;
using RPGClone.Combat;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTools
{
    public static class MMOCombatDebugMenu
    {
        private static readonly MethodInfo ForceLeaveCombatMethod = typeof(MMOCombatant).GetMethod(
            "ForceLeaveCombat",
            BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("RPG Clone/Debug/Force Player In Combat")]
        public static void ForcePlayerInCombat()
        {
            MMOCombatant combatant = FindPlayerCombatant();
            if (combatant == null)
            {
                Debug.LogError("Could not find player MMOCombatant.");
                return;
            }

            combatant.RegisterCombatActivity();
            Debug.Log("Forced player into combat.");
        }

        [MenuItem("RPG Clone/Debug/Force Player Out Of Combat")]
        public static void ForcePlayerOutOfCombat()
        {
            MMOCombatant combatant = FindPlayerCombatant();
            if (combatant == null)
            {
                Debug.LogError("Could not find player MMOCombatant.");
                return;
            }

            ForceLeaveCombatMethod?.Invoke(combatant, null);
            Debug.Log("Forced player out of combat.");
        }

        private static MMOCombatant FindPlayerCombatant()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<MMOCombatant>() : null;
        }
    }
}
