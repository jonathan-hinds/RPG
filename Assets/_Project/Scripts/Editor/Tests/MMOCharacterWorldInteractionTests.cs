#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Enemies;
using RPGClone.Multiplayer;
using RPGClone.Player;
using UnityEngine;
using UnityEngine.AI;

namespace RPGClone.EditorTests
{
    public sealed class MMOCharacterWorldInteractionTests
    {
        [Test]
        public void CharacterCollisionPolicy_AssignsBodyCollidersButPreservesTriggerLayers()
        {
            GameObject character = new("Collision Policy Character");
            GameObject bodyObject = new("Body Collider");
            GameObject triggerObject = new("Gameplay Trigger");
            try
            {
                bodyObject.transform.SetParent(character.transform);
                triggerObject.transform.SetParent(character.transform);
                BoxCollider bodyCollider = bodyObject.AddComponent<BoxCollider>();
                BoxCollider triggerCollider = triggerObject.AddComponent<BoxCollider>();
                triggerCollider.isTrigger = true;
                NavMeshAgent agent = character.AddComponent<NavMeshAgent>();

                character.AddComponent<MMOCharacterIdentity>();
                MMOCharacterCollisionPolicy.ApplyTo(character);

                int characterLayer = LayerMask.NameToLayer(MMOCharacterCollisionPolicy.CharacterLayerName);
                Assert.That(characterLayer, Is.GreaterThanOrEqualTo(0));
                Assert.That(bodyCollider.gameObject.layer, Is.EqualTo(characterLayer));
                Assert.That(triggerCollider.gameObject.layer, Is.EqualTo(0));
                Assert.That(Physics.GetIgnoreLayerCollision(characterLayer, characterLayer), Is.True);
                Assert.That(agent.obstacleAvoidanceType, Is.EqualTo(ObstacleAvoidanceType.NoObstacleAvoidance));
            }
            finally
            {
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void NpcFacingRotation_UsesPlanarDirectionOnly()
        {
            bool resolved = MMONpcInteractionFacing.TryGetPlanarFacingRotation(
                new Vector3(1f, 2f, 3f),
                new Vector3(5f, 200f, 3f),
                out Quaternion rotation);

            Assert.That(resolved, Is.True);
            Assert.That(Vector3.Angle(rotation * Vector3.forward, Vector3.right), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(rotation * Vector3.up, Vector3.up), Is.LessThan(0.01f));
        }

        [Test]
        public void NpcFacingSnapshot_KeepsOnlyLatestInteractorPerNpc()
        {
            const string sessionId = "npc-facing-test";
            const string interactionKey = "quest:warchief";
            MMOSharedSessionState.Reset();
            try
            {
                MMOSharedSessionState.UpsertNpcFacingSnapshot(CreateFacingSnapshot(
                    sessionId,
                    interactionKey,
                    "first-player",
                    new Vector3(1f, 0f, 2f)));
                MMOSharedSessionState.UpsertNpcFacingSnapshot(CreateFacingSnapshot(
                    sessionId,
                    interactionKey,
                    "last-player",
                    new Vector3(4f, 0f, 8f)));

                IReadOnlyList<MMONpcFacingSnapshot> snapshots =
                    MMOSharedSessionState.GetNpcFacingSnapshots(sessionId);

                Assert.That(snapshots, Has.Count.EqualTo(1));
                Assert.That(snapshots[0].actorCharacterId, Is.EqualTo("last-player"));
                Assert.That(snapshots[0].actorPosition.ToVector3(), Is.EqualTo(new Vector3(4f, 0f, 8f)));
            }
            finally
            {
                MMOSharedSessionState.Reset();
            }
        }

        [Test]
        public void EnemyEngagementDestination_StopsInsideAttackRangeInsteadOfAtPlayerCenter()
        {
            System.Reflection.MethodInfo method = typeof(MMOEnemyController).GetMethod(
                "CalculateEngagementDestination",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            Vector3 destination = method != null
                ? (Vector3)method.Invoke(null, new object[]
                {
                    new Vector3(10f, 0f, 0f),
                    Vector3.zero,
                    4f,
                    0.85f
                })
                : Vector3.zero;

            Assert.That(method, Is.Not.Null);
            Assert.That(destination, Is.Not.EqualTo(Vector3.zero));
            Assert.That(Vector3.Distance(destination, Vector3.zero), Is.EqualTo(3.4f).Within(0.001f));
            Assert.That(Vector3.Distance(destination, Vector3.zero), Is.LessThan(4f));
        }

        [Test]
        public void CameraCollisionMask_ExcludesAllCharacterBodies()
        {
            MMOThirdPersonCameraConfig config =
                ScriptableObject.CreateInstance<MMOThirdPersonCameraConfig>();
            try
            {
                config.collisionMask = ~0;
                int characterLayer =
                    LayerMask.NameToLayer(MMOCharacterCollisionPolicy.CharacterLayerName);

                Assert.That(characterLayer, Is.GreaterThanOrEqualTo(0));
                Assert.That(config.EffectiveCollisionMask & (1 << characterLayer), Is.Zero);
                Assert.That(
                    MMOThirdPersonCameraConfig.BuildDefaultCollisionMask() & (1 << characterLayer),
                    Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private static MMONpcFacingSnapshot CreateFacingSnapshot(
            string sessionId,
            string interactionKey,
            string actorCharacterId,
            Vector3 actorPosition)
        {
            return new MMONpcFacingSnapshot
            {
                sessionId = sessionId,
                npcInteractionKey = interactionKey,
                actorCharacterId = actorCharacterId,
                actorPosition = new Vector3SaveData(actorPosition),
                updatedUtcTicks = System.DateTime.UtcNow.Ticks
            };
        }
    }
}
#endif
