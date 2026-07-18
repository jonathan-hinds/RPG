using System.IO;
using NUnit.Framework;
using RPGClone.Animation;
using RPGClone.Characters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPGClone.EditorTests
{
    public sealed class MMOCreatureSurfaceTests
    {
        [Test]
        public void AllCreatureVisualDefinitions_UseUnlitShadowCastingSurfaces()
        {
            string[] definitionGuids = AssetDatabase.FindAssets(
                "t:MMOCreatureVisualDefinition",
                new[] { "Assets/Characters" });

            Assert.That(definitionGuids, Is.Not.Empty, "No creature visual definitions were found.");
            foreach (string definitionGuid in definitionGuids)
            {
                VerifyCreatureSurface(AssetDatabase.GUIDToAssetPath(definitionGuid));
            }
        }

        private static void VerifyCreatureSurface(string definitionPath)
        {
            MMOCreatureVisualDefinition definition =
                AssetDatabase.LoadAssetAtPath<MMOCreatureVisualDefinition>(definitionPath);
            Assert.That(definition, Is.Not.Null, definitionPath);

            string creatureFolder = Path.GetDirectoryName(definitionPath)?.Replace('\\', '/');
            string materialPath = $"{creatureFolder}/Materials/{definition.CreatureId}_Body.mat";
            Material bodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(bodyMaterial, Is.Not.Null, $"Missing body material for {definition.CreatureId}.");
            Assert.That(bodyMaterial.shader, Is.Not.Null, materialPath);
            Assert.That(
                bodyMaterial.shader.name,
                Is.EqualTo(MMOCharacterUnlitMaterialUtility.UnlitShaderName),
                materialPath);
            Assert.That(bodyMaterial.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0), materialPath);
            Assert.That(bodyMaterial.GetTexture("_BaseMap"), Is.SameAs(definition.DiffuseTexture), materialPath);

            string prefabPath = $"{creatureFolder}/Prefabs/{definition.CreatureId}Enemy.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, $"Missing creature prefab for {definition.CreatureId}.");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                Assert.That(instance.GetComponent<MMOCreatureAnimator>(), Is.Not.Null, prefabPath);

                Animator animator = instance.GetComponentInChildren<Animator>(true);
                Assert.That(animator, Is.Not.Null, prefabPath);
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null, prefabPath);

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty, prefabPath);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer is not MeshRenderer and not SkinnedMeshRenderer)
                    {
                        continue;
                    }

                    Assert.That(renderer.receiveShadows, Is.False, $"{prefabPath}: {renderer.name}");
                    Assert.That(renderer.shadowCastingMode, Is.Not.EqualTo(ShadowCastingMode.Off), renderer.name);
                    Assert.That(renderer.shadowCastingMode, Is.Not.EqualTo(ShadowCastingMode.ShadowsOnly), renderer.name);
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        Assert.That(material, Is.Not.Null, $"{prefabPath}: {renderer.name}");
                        Assert.That(
                            material.shader.name,
                            Is.EqualTo(MMOCharacterUnlitMaterialUtility.UnlitShaderName),
                            $"{prefabPath}: {renderer.name}");
                        Assert.That(material.FindPass("ShadowCaster"), Is.GreaterThanOrEqualTo(0), renderer.name);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
