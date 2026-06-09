using UnityEngine;

namespace RPGClone.World.Foliage
{
    [DisallowMultipleComponent]
    public sealed class MMOTerrainTreeCollisionDefinition : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float trunkRadius = 0.45f;
        [SerializeField, Min(0.01f)] private float trunkHeight = 4f;
        [SerializeField, Min(0f)] private float trunkCenterYOffset = 2f;

        public float TrunkRadius => trunkRadius;
        public float TrunkHeight => trunkHeight;
        public float TrunkCenterYOffset => trunkCenterYOffset;

        public void Configure(float radius, float height, float centerYOffset)
        {
            trunkRadius = Mathf.Max(0.01f, radius);
            trunkHeight = Mathf.Max(0.01f, height);
            trunkCenterYOffset = Mathf.Max(0f, centerYOffset);
        }
    }
}
