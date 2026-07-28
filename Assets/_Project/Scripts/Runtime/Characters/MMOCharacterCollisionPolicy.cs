using UnityEngine;
using UnityEngine.AI;

namespace RPGClone.Characters
{
    public static class MMOCharacterCollisionPolicy
    {
        public const string CharacterLayerName = "Characters";

        private static bool hasLoggedMissingLayer;

        public static void ApplyTo(GameObject characterRoot)
        {
            if (characterRoot == null)
            {
                return;
            }

            int characterLayer = LayerMask.NameToLayer(CharacterLayerName);
            if (characterLayer < 0)
            {
                if (!hasLoggedMissingLayer)
                {
                    hasLoggedMissingLayer = true;
                    Debug.LogError(
                        $"Required physics layer '{CharacterLayerName}' is missing. " +
                        "Character-to-character collision filtering cannot be applied.");
                }

                return;
            }

            if (!Physics.GetIgnoreLayerCollision(characterLayer, characterLayer))
            {
                Physics.IgnoreLayerCollision(characterLayer, characterLayer, true);
            }

            foreach (Collider collider in characterRoot.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null && !collider.isTrigger)
                {
                    collider.gameObject.layer = characterLayer;
                }
            }

            foreach (NavMeshAgent agent in characterRoot.GetComponentsInChildren<NavMeshAgent>(true))
            {
                if (agent != null)
                {
                    agent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
                }
            }
        }
    }
}
