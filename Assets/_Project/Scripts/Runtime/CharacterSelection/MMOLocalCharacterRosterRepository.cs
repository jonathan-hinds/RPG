using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace RPGClone.CharacterSelection
{
    public sealed class MMOLocalCharacterRosterRepository : MMOCharacterRosterRepository
    {
        private const string FileName = "rpg_clone_character_roster.json";

        private readonly string path;

        public MMOLocalCharacterRosterRepository()
        {
            path = Path.Combine(Application.persistentDataPath, FileName);
        }

        public async Task<MMOCharacterRosterSaveData> LoadAsync()
        {
            if (!File.Exists(path))
            {
                return new MMOCharacterRosterSaveData();
            }

            string json = await Task.Run(() => File.ReadAllText(path));
            return string.IsNullOrWhiteSpace(json)
                ? new MMOCharacterRosterSaveData()
                : JsonUtility.FromJson<MMOCharacterRosterSaveData>(json) ?? new MMOCharacterRosterSaveData();
        }

        public async Task SaveAsync(MMOCharacterRosterSaveData roster)
        {
            roster ??= new MMOCharacterRosterSaveData();
            string json = JsonUtility.ToJson(roster, true);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await Task.Run(() => File.WriteAllText(path, json));
        }
    }
}
