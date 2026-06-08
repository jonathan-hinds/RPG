using System.Collections.Generic;
using RPGClone.Enemies;
using UnityEngine;

namespace RPGClone.Animation
{
    [CreateAssetMenu(menuName = "RPG Clone/Animation/Creature Visual Definition", fileName = "CreatureVisualDefinition")]
    public sealed class MMOCreatureVisualDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string creatureId = "creature";
        [SerializeField] private string displayName = "Creature";

        [Header("Assets")]
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private Texture2D diffuseTexture;
        [SerializeField] private Texture2D normalTexture;
        [SerializeField] private MMOCreatureAnimationSet animationSet;
        [SerializeField] private MMOEnemyDefinition defaultEnemyDefinition;
        [SerializeField] private List<MMOEnemyDefinition> matchingEnemyDefinitions = new();

        [Header("Scene Conversion")]
        [SerializeField] private List<string> sceneNamePrefixes = new();

        [Header("Sizing")]
        [SerializeField, Min(0.1f)] private float targetHeight = 2.25f;
        [SerializeField, Min(0.01f)] private float colliderRadius = 0.6f;
        [SerializeField] private Vector3 visualLocalOffset;
        [SerializeField] private Vector3 visualLocalEulerAngles;
        [SerializeField] private float modelYawOffsetDegrees;

        [Header("Material")]
        [SerializeField, Range(0f, 1f)] private float smoothness = 0.35f;
        [SerializeField, Range(0f, 1f)] private float metallic;

        public string CreatureId => string.IsNullOrWhiteSpace(creatureId) ? name : creatureId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? CreatureId : displayName;
        public GameObject ModelPrefab => modelPrefab;
        public Texture2D DiffuseTexture => diffuseTexture;
        public Texture2D NormalTexture => normalTexture;
        public MMOCreatureAnimationSet AnimationSet => animationSet;
        public MMOEnemyDefinition DefaultEnemyDefinition => defaultEnemyDefinition;
        public IReadOnlyList<MMOEnemyDefinition> MatchingEnemyDefinitions => matchingEnemyDefinitions;
        public IReadOnlyList<string> SceneNamePrefixes => sceneNamePrefixes;
        public float TargetHeight => targetHeight;
        public float ColliderRadius => colliderRadius;
        public Vector3 VisualLocalOffset => visualLocalOffset;
        public Vector3 VisualLocalEulerAngles => visualLocalEulerAngles;
        public float ModelYawOffsetDegrees => modelYawOffsetDegrees;
        public float Smoothness => smoothness;
        public float Metallic => metallic;

        public void Configure(
            string newCreatureId,
            string newDisplayName,
            GameObject newModelPrefab,
            Texture2D newDiffuseTexture,
            Texture2D newNormalTexture,
            MMOCreatureAnimationSet newAnimationSet,
            MMOEnemyDefinition newDefaultEnemyDefinition,
            IEnumerable<MMOEnemyDefinition> newMatchingEnemyDefinitions,
            IEnumerable<string> newSceneNamePrefixes,
            float newTargetHeight,
            float newColliderRadius,
            Vector3 newVisualLocalOffset,
            Vector3 newVisualLocalEulerAngles,
            float newModelYawOffsetDegrees,
            float newSmoothness,
            float newMetallic)
        {
            creatureId = string.IsNullOrWhiteSpace(newCreatureId) ? name : newCreatureId;
            displayName = string.IsNullOrWhiteSpace(newDisplayName) ? creatureId : newDisplayName;
            modelPrefab = newModelPrefab;
            diffuseTexture = newDiffuseTexture;
            normalTexture = newNormalTexture;
            animationSet = newAnimationSet;
            defaultEnemyDefinition = newDefaultEnemyDefinition;
            matchingEnemyDefinitions = newMatchingEnemyDefinitions != null
                ? new List<MMOEnemyDefinition>(newMatchingEnemyDefinitions)
                : new List<MMOEnemyDefinition>();
            sceneNamePrefixes = newSceneNamePrefixes != null
                ? new List<string>(newSceneNamePrefixes)
                : new List<string>();
            targetHeight = Mathf.Max(0.1f, newTargetHeight);
            colliderRadius = Mathf.Max(0.01f, newColliderRadius);
            visualLocalOffset = newVisualLocalOffset;
            visualLocalEulerAngles = newVisualLocalEulerAngles;
            modelYawOffsetDegrees = newModelYawOffsetDegrees;
            smoothness = Mathf.Clamp01(newSmoothness);
            metallic = Mathf.Clamp01(newMetallic);
        }
    }
}
