using RPGClone.CharacterSelection;
using RPGClone.Characters;
using UnityEngine;

namespace RPGClone.Debugging
{
    public static class MMODebugCharacterSessionBootstrap
    {
        private const string DebugCharacterId = "debug-default-player";

        public static void SelectDefaultDebugCharacter()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (MMOCharacterSession.HasSelectedCharacter)
            {
                return;
            }

            MMOCharacterSession.Select(new MMOCharacterSaveData
            {
                characterId = DebugCharacterId,
                characterName = "Debug Warrior",
                race = MMOPlayableRace.Orc,
                characterClass = MMOPlayableClass.Warrior,
                level = 1,
                sceneName = "OrcishStarterValley",
                position = new Vector3SaveData(new Vector3(-42f, 15.9735f, -178f)),
                rotationEuler = new Vector3SaveData(new Vector3(0f, 18f, 0f))
            });
#endif
        }
    }
}
