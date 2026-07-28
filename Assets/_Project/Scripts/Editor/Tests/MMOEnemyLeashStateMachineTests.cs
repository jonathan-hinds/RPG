#if UNITY_EDITOR
using NUnit.Framework;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using UnityEditor;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMOEnemyLeashStateMachineTests
    {
        [Test]
        public void CombatActivity_MovesAnchorAndRefreshesLeashGracePeriod()
        {
            MMOEnemyLeashStateMachine leash = new();
            leash.Reset(Vector3.zero);
            leash.BeginEngagement(Vector3.zero, 0f);

            Assert.That(leash.ShouldReturnHome(new Vector3(29f, 0f, 0f), 28f, 15f, 14.99f), Is.False);
            Assert.That(leash.ShouldReturnHome(new Vector3(29f, 0f, 0f), 28f, 15f, 15f), Is.True);

            leash.RecordCombatActivity(new Vector3(25f, 0f, 0f), 16f);

            Assert.That(leash.AnchorPosition, Is.EqualTo(new Vector3(25f, 0f, 0f)));
            Assert.That(leash.LastCombatActivityTime, Is.EqualTo(16f));
            Assert.That(leash.ShouldReturnHome(new Vector3(54f, 0f, 0f), 28f, 15f, 30.99f), Is.False);
            Assert.That(leash.ShouldReturnHome(new Vector3(50f, 0f, 0f), 28f, 15f, 31f), Is.False);
            Assert.That(leash.ShouldReturnHome(new Vector3(54f, 0f, 0f), 28f, 15f, 31f), Is.True);
        }

        [Test]
        public void ReturningHome_CannotBeCancelledByCombatActivity()
        {
            MMOEnemyLeashStateMachine leash = new();
            leash.Reset(Vector3.zero);
            leash.BeginEngagement(new Vector3(4f, 0f, 0f), 10f);
            leash.BeginReturnHome();
            Vector3 anchorBeforeAttack = leash.AnchorPosition;

            Assert.That(leash.BeginEngagement(new Vector3(20f, 0f, 0f), 20f), Is.False);
            Assert.That(leash.RecordCombatActivity(new Vector3(20f, 0f, 0f), 20f), Is.False);
            Assert.That(leash.IsReturningHome, Is.True);
            Assert.That(leash.AnchorPosition, Is.EqualTo(anchorBeforeAttack));
        }

        [Test]
        public void RepeatedEngagement_DoesNotRefreshGraceWithoutDamage()
        {
            MMOEnemyLeashStateMachine leash = new();
            leash.Reset(Vector3.zero);
            leash.BeginEngagement(Vector3.zero, 2f);

            leash.BeginEngagement(new Vector3(10f, 0f, 0f), 12f);

            Assert.That(leash.AnchorPosition, Is.EqualTo(Vector3.zero));
            Assert.That(leash.LastCombatActivityTime, Is.EqualTo(2f));
            Assert.That(leash.ShouldReturnHome(new Vector3(29f, 0f, 0f), 28f, 15f, 17f), Is.True);
        }

        [Test]
        public void CompletingReturn_ResetsStateAndAnchorToSpawn()
        {
            Vector3 home = new(3f, 1f, 7f);
            MMOEnemyLeashStateMachine leash = new();
            leash.Reset(home);
            leash.BeginEngagement(new Vector3(20f, 1f, 7f), 1f);
            leash.RecordCombatActivity(new Vector3(25f, 1f, 7f), 2f);
            leash.BeginReturnHome();

            Assert.That(leash.IsAtHome(new Vector3(3.2f, 99f, 7f), home, 0.35f), Is.True);

            leash.CompleteReturnHome(home);

            Assert.That(leash.Phase, Is.EqualTo(MMOEnemyLeashPhase.Idle));
            Assert.That(leash.AnchorPosition, Is.EqualTo(home));
            Assert.That(leash.LastCombatActivityTime, Is.EqualTo(float.NegativeInfinity));
        }

        [Test]
        public void ClassicPursuitDefaults_KeepStandardCreaturesFasterThanPlayers()
        {
            Assert.That(MMOClassicEnemyPursuitDefaults.CreatureToPlayerRunSpeedRatio, Is.EqualTo(8f / 7f).Within(0.0001f));
            Assert.That(MMOClassicEnemyPursuitDefaults.StandardChaseSpeed, Is.GreaterThan(MMOClassicEnemyPursuitDefaults.ProjectPlayerRunSpeed));
            Assert.That(MMOClassicEnemyPursuitDefaults.StandardLeashGraceSeconds, Is.EqualTo(15f));
        }

        [Test]
        public void AuthoredMobileEnemies_UseClassicPursuitAndLeashBaselines()
        {
            string[] definitionPaths =
            {
                "Assets/_Project/Configs/Enemies/Ash_Canyon_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/AshGeneral_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/Bristleback_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/Bristleback_Docile.asset",
                "Assets/_Project/Configs/Enemies/Ogre_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/Trog_Caster_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/Wolf_Aggressive.asset"
            };

            foreach (string path in definitionPaths)
            {
                MMOEnemyDefinition definition = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(path);
                Assert.That(definition, Is.Not.Null, path);
                Assert.That(definition.ChaseSpeed, Is.GreaterThanOrEqualTo(MMOClassicEnemyPursuitDefaults.StandardChaseSpeed - 0.001f), path);
                Assert.That(definition.LeashGraceSeconds, Is.EqualTo(MMOClassicEnemyPursuitDefaults.StandardLeashGraceSeconds).Within(0.001f), path);
            }
        }

        [Test]
        public void AuthoredOutdoorEnemies_UseClassicRespawnBaseline()
        {
            string[] definitionPaths =
            {
                "Assets/_Project/Configs/Enemies/Ash_Canyon_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/AshGeneral_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/Bristleback_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/Bristleback_Docile.asset",
                "Assets/_Project/Configs/Enemies/Ogre_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/Trog_Caster_Aggressive.asset",
                "Assets/_Project/Configs/Enemies/Wolf_Aggressive.asset"
            };

            foreach (string path in definitionPaths)
            {
                MMOEnemyDefinition definition = AssetDatabase.LoadAssetAtPath<MMOEnemyDefinition>(path);
                Assert.That(definition, Is.Not.Null, path);
                Assert.That(
                    definition.RespawnSeconds,
                    Is.EqualTo(MMOClassicRespawnDefaults.StandardOutdoorSeconds),
                    path);
            }
        }

        [Test]
        public void EnemySnapshot_PreservesLeashAnchorAndReturnState()
        {
            EnemySnapshot original = new()
            {
                inCombat = false,
                leashing = true,
                leashAnchorPosition = new Vector3SaveData(new Vector3(12f, 2f, -8f))
            };

            EnemySnapshot clone = JsonUtility.FromJson<EnemySnapshot>(JsonUtility.ToJson(original));

            Assert.That(clone.leashing, Is.True);
            Assert.That(clone.leashAnchorPosition.ToVector3(), Is.EqualTo(new Vector3(12f, 2f, -8f)));
        }

        [Test]
        public void HostileActionReceiver_CanRejectDirectDamageDuringEvade()
        {
            GameObject targetObject = new("Evading Damage Target");
            try
            {
                MMOCombatant target = targetObject.AddComponent<MMOCombatant>();
                TestHostileActionReceiver receiver = targetObject.AddComponent<TestHostileActionReceiver>();
                target.Identity.Health.Configure(100, 100);
                receiver.CanReceive = false;

                target.ApplyDamage(null, null, 25, false, false);

                Assert.That(target.Identity.Health.CurrentValue, Is.EqualTo(100));
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
            }
        }

        [Test]
        public void DisengageFromAllCombat_ClearsEveryPartyCombatLink()
        {
            GameObject enemyObject = new("Leashing Enemy");
            GameObject firstPlayerObject = new("First Player");
            GameObject secondPlayerObject = new("Second Player");
            try
            {
                MMOCombatant enemy = enemyObject.AddComponent<MMOCombatant>();
                MMOCombatant firstPlayer = firstPlayerObject.AddComponent<MMOCombatant>();
                MMOCombatant secondPlayer = secondPlayerObject.AddComponent<MMOCombatant>();
                enemy.Identity.Health.Configure(100, 100);
                firstPlayer.Identity.Health.Configure(100, 100);
                secondPlayer.Identity.Health.Configure(100, 100);
                enemy.EngageCombatWith(firstPlayer);
                enemy.EngageCombatWith(secondPlayer);

                enemy.DisengageFromAllCombat();

                Assert.That(enemy.IsInCombat, Is.False);
                Assert.That(firstPlayer.IsInCombat, Is.False);
                Assert.That(secondPlayer.IsInCombat, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(secondPlayerObject);
                Object.DestroyImmediate(firstPlayerObject);
                Object.DestroyImmediate(enemyObject);
            }
        }

        public sealed class TestHostileActionReceiver : MonoBehaviour, IMMOHostileActionReceiver
        {
            public bool CanReceive = true;
            public bool CanReceiveHostileActions => CanReceive;
        }
    }
}
#endif
