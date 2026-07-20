#if UNITY_EDITOR
using NUnit.Framework;
using RPGClone.CharacterSelection;
using RPGClone.Characters;
using RPGClone.Combat;
using RPGClone.Enemies;
using UnityEngine;

namespace RPGClone.EditorTests
{
    public sealed class MMOEnemyLeashStateMachineTests
    {
        [Test]
        public void CombatActivity_MovesAnchorAndAllowsLongDistanceKiting()
        {
            MMOEnemyLeashStateMachine leash = new();
            leash.Reset(Vector3.zero);
            leash.BeginEngagement(Vector3.zero);

            Assert.That(leash.IsBeyondLeash(new Vector3(29f, 0f, 0f), 28f), Is.True);

            leash.RecordCombatActivity(new Vector3(25f, 0f, 0f));

            Assert.That(leash.AnchorPosition, Is.EqualTo(new Vector3(25f, 0f, 0f)));
            Assert.That(leash.IsBeyondLeash(new Vector3(50f, 0f, 0f), 28f), Is.False);
        }

        [Test]
        public void ReturningHome_CannotBeCancelledByCombatActivity()
        {
            MMOEnemyLeashStateMachine leash = new();
            leash.Reset(Vector3.zero);
            leash.BeginEngagement(new Vector3(4f, 0f, 0f));
            leash.BeginReturnHome();
            Vector3 anchorBeforeAttack = leash.AnchorPosition;

            Assert.That(leash.BeginEngagement(new Vector3(20f, 0f, 0f)), Is.False);
            Assert.That(leash.RecordCombatActivity(new Vector3(20f, 0f, 0f)), Is.False);
            Assert.That(leash.IsReturningHome, Is.True);
            Assert.That(leash.AnchorPosition, Is.EqualTo(anchorBeforeAttack));
        }

        [Test]
        public void CompletingReturn_ResetsStateAndAnchorToSpawn()
        {
            Vector3 home = new(3f, 1f, 7f);
            MMOEnemyLeashStateMachine leash = new();
            leash.Reset(home);
            leash.BeginEngagement(new Vector3(20f, 1f, 7f));
            leash.RecordCombatActivity(new Vector3(25f, 1f, 7f));
            leash.BeginReturnHome();

            Assert.That(leash.IsAtHome(new Vector3(3.2f, 99f, 7f), home, 0.35f), Is.True);

            leash.CompleteReturnHome(home);

            Assert.That(leash.Phase, Is.EqualTo(MMOEnemyLeashPhase.Idle));
            Assert.That(leash.AnchorPosition, Is.EqualTo(home));
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
