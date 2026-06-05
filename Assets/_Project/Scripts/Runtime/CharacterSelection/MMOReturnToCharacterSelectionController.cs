using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.CharacterSelection
{
    public sealed class MMOReturnToCharacterSelectionController : MonoBehaviour
    {
        [SerializeField] private string characterSelectionSceneName = "CharacterSelection";
        [SerializeField] private MMOCharacterPersistenceAgent persistenceAgent;

        private bool isReturning;

        private void Awake()
        {
            if (persistenceAgent == null)
            {
                persistenceAgent = GetComponent<MMOCharacterPersistenceAgent>();
            }
        }

        public void Configure(string sceneName, MMOCharacterPersistenceAgent agent)
        {
            characterSelectionSceneName = string.IsNullOrWhiteSpace(sceneName) ? characterSelectionSceneName : sceneName;
            persistenceAgent = agent != null ? agent : persistenceAgent;
        }

        public void ReturnToCharacterSelection()
        {
            if (isReturning)
            {
                return;
            }

            _ = ReturnToCharacterSelectionAsync();
        }

        private async Task ReturnToCharacterSelectionAsync()
        {
            isReturning = true;
            if (persistenceAgent != null)
            {
                await persistenceAgent.SaveCurrentCharacterAsync();
            }

            MMOCharacterSession.Clear();
            SceneManager.LoadScene(characterSelectionSceneName);
        }
    }
}
