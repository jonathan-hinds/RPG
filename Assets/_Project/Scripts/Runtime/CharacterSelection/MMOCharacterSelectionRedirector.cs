using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.CharacterSelection
{
    public sealed class MMOCharacterSelectionRedirector : MonoBehaviour
    {
        [SerializeField] private string characterSelectionSceneName = "CharacterSelection";
        [SerializeField] private bool redirectWhenNoCharacterSession = true;

        private void Start()
        {
            if (!redirectWhenNoCharacterSession || MMOCharacterSession.HasSelectedCharacter)
            {
                return;
            }

            if (SceneManager.GetActiveScene().name == characterSelectionSceneName)
            {
                return;
            }

            SceneManager.LoadScene(characterSelectionSceneName);
        }

        public void Configure(string sceneName, bool redirectOnMissingSession)
        {
            characterSelectionSceneName = string.IsNullOrWhiteSpace(sceneName) ? characterSelectionSceneName : sceneName;
            redirectWhenNoCharacterSession = redirectOnMissingSession;
        }
    }
}
