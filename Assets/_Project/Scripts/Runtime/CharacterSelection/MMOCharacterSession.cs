using System;

namespace RPGClone.CharacterSelection
{
    public static class MMOCharacterSession
    {
        public static event Action Changed;

        public static MMOCharacterSaveData SelectedCharacter { get; private set; }

        public static bool HasSelectedCharacter => SelectedCharacter != null && !string.IsNullOrWhiteSpace(SelectedCharacter.characterId);

        public static void Select(MMOCharacterSaveData character)
        {
            SelectedCharacter = character;
            Changed?.Invoke();
        }

        public static void Clear()
        {
            SelectedCharacter = null;
            Changed?.Invoke();
        }
    }
}
