using System.IO;
using System.Threading.Tasks;
using RPGClone.Social;
using UnityEngine;

namespace RPGClone.CharacterSelection
{
    public sealed class MMOLocalCharacterRosterRepository : MMOCharacterRosterRepository
    {
        private const string FileNameFormat = "rpg_clone_character_roster_{0}.json";

        private readonly string path;

        public MMOLocalCharacterRosterRepository()
        {
            path = Path.Combine(Application.persistentDataPath, string.Format(FileNameFormat, Sanitize(MMOSocialIdentityService.AccountId)));
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

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "offline";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value.Replace(' ', '_');
        }
    }
}
