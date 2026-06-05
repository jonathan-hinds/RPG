using System.Threading.Tasks;

namespace RPGClone.CharacterSelection
{
    public interface MMOCharacterRosterRepository
    {
        Task<MMOCharacterRosterSaveData> LoadAsync();
        Task SaveAsync(MMOCharacterRosterSaveData roster);
    }
}
