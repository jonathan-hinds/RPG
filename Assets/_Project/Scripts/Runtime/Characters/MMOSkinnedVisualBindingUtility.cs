using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.Characters
{
    public static class MMOSkinnedVisualBindingUtility
    {
        public static Dictionary<string, Transform> BuildSkeletonLookup(Transform root, Predicate<Transform> exclude = null)
        {
            Dictionary<string, Transform> transformsByName = new(StringComparer.Ordinal);
            if (root == null)
            {
                return transformsByName;
            }

            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                if (candidate != null && (exclude == null || !exclude(candidate)))
                {
                    transformsByName.TryAdd(candidate.name, candidate);
                }
            }

            return transformsByName;
        }

        public static bool TryRebind(
            SkinnedMeshRenderer skinnedRenderer,
            IReadOnlyDictionary<string, Transform> liveSkeleton,
            out List<string> missingBoneNames)
        {
            missingBoneNames = new List<string>();
            if (skinnedRenderer == null || liveSkeleton == null)
            {
                return false;
            }

            Transform[] sourceBones = skinnedRenderer.bones;
            if (sourceBones == null || sourceBones.Length == 0)
            {
                missingBoneNames.Add("no bone bindings");
                return false;
            }

            Transform[] reboundBones = new Transform[sourceBones.Length];
            for (int i = 0; i < sourceBones.Length; i++)
            {
                string boneName = sourceBones[i] != null ? sourceBones[i].name : string.Empty;
                if (string.IsNullOrEmpty(boneName) || !liveSkeleton.TryGetValue(boneName, out Transform liveBone))
                {
                    missingBoneNames.Add(string.IsNullOrEmpty(boneName) ? $"index {i}" : boneName);
                    continue;
                }

                reboundBones[i] = liveBone;
            }

            if (missingBoneNames.Count > 0)
            {
                return false;
            }

            Transform reboundRootBone = null;
            if (skinnedRenderer.rootBone != null)
            {
                liveSkeleton.TryGetValue(skinnedRenderer.rootBone.name, out reboundRootBone);
            }

            skinnedRenderer.bones = reboundBones;
            skinnedRenderer.rootBone = reboundRootBone ?? reboundBones[0];
            return true;
        }
    }
}
