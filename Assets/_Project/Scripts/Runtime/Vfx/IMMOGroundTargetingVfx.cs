using UnityEngine;

namespace RPGClone.Vfx
{
    public interface IMMOGroundTargetingVfx
    {
        void UpdatePreview(Vector3 position, Vector3 normal, float radius, bool isValid);
    }
}
