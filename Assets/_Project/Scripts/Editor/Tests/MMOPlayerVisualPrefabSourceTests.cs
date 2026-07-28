using System.Linq;
using NUnit.Framework;
using RPGClone.Characters;
using RPGClone.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RPGClone.EditorTools.Tests
{
    public sealed class MMOPlayerVisualPrefabSourceTests
    {
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player/PlayerCapsule.prefab";
        private const string GameplayScenePath = "Assets/Scenes/OrcishStarterValley.unity";

        [Test]
        public void GameplayPlayer_InheritsCharacterVisualFromPlayerPrefab()
        {
            Scene scene = SceneManager.GetSceneByPath(GameplayScenePath);
            bool closeSceneAfterTest = !scene.IsValid() || !scene.isLoaded;
            if (closeSceneAfterTest)
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject player = scene.GetRootGameObjects()
                    .Single(candidate => candidate.CompareTag("Player"));
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(player), Is.EqualTo(PlayerPrefabPath));

                Transform sceneVisual = player.transform.Find("Character Visual");
                Assert.That(sceneVisual, Is.Not.Null);
                Assert.That(PrefabUtility.IsAddedGameObjectOverride(sceneVisual.gameObject), Is.False,
                    "Gameplay scenes must not own a duplicate player visual.");
                Assert.That(PrefabUtility.GetAddedGameObjects(player)
                    .Any(added => added.instanceGameObject != null && added.instanceGameObject.name == "Character Visual"),
                    Is.False);

                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                Transform prefabVisual = playerPrefab.transform.Find("Character Visual");
                Assert.That(prefabVisual, Is.Not.Null);
                Assert.That(sceneVisual.localPosition, Is.EqualTo(prefabVisual.localPosition));
                Assert.That(sceneVisual.localRotation, Is.EqualTo(prefabVisual.localRotation));
                Assert.That(sceneVisual.localScale, Is.EqualTo(prefabVisual.localScale));

                MMOPlayerLocomotionAnimator locomotion = player.GetComponent<MMOPlayerLocomotionAnimator>();
                MMOPlayerCombatAnimator combat = player.GetComponent<MMOPlayerCombatAnimator>();
                Assert.That(locomotion, Is.Not.Null);
                Assert.That(combat, Is.Not.Null);

                Animator sceneAnimator = sceneVisual.GetComponent<Animator>();
                SerializedObject locomotionState = new(locomotion);
                SerializedObject combatState = new(combat);
                Assert.That(locomotionState.FindProperty("visualRoot").objectReferenceValue, Is.SameAs(sceneVisual));
                Assert.That(locomotionState.FindProperty("animator").objectReferenceValue, Is.SameAs(sceneAnimator));
                Assert.That(combatState.FindProperty("animator").objectReferenceValue, Is.SameAs(sceneAnimator));
            }
            finally
            {
                if (closeSceneAfterTest && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void GameplayNpcs_MatchPlayerVisualLocalTransform()
        {
            Scene scene = SceneManager.GetSceneByPath(GameplayScenePath);
            bool closeSceneAfterTest = !scene.IsValid() || !scene.isLoaded;
            if (closeSceneAfterTest)
            {
                scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                Transform playerVisual = playerPrefab.transform.Find("Character Visual");
                Assert.That(playerVisual, Is.Not.Null);

                MMONpcVisualAuthoring[] npcs = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MMONpcVisualAuthoring>(true))
                    .ToArray();
                Assert.That(npcs, Is.Not.Empty);
                foreach (MMONpcVisualAuthoring npc in npcs)
                {
                    Transform npcVisual = npc.transform.Find("Character Visual");
                    Assert.That(npcVisual, Is.Not.Null, npc.name);
                    Assert.That(npcVisual.localPosition, Is.EqualTo(playerVisual.localPosition), npc.name);
                    Assert.That(npcVisual.localRotation, Is.EqualTo(playerVisual.localRotation), npc.name);
                    Assert.That(npcVisual.localScale, Is.EqualTo(playerVisual.localScale), npc.name);
                }
            }
            finally
            {
                if (closeSceneAfterTest && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
