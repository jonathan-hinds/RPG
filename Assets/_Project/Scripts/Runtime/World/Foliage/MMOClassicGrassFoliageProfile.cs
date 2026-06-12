using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.World.Foliage
{
    [CreateAssetMenu(menuName = "RPG Clone/World/Classic Grass Foliage Profile")]
    public sealed class MMOClassicGrassFoliageProfile : ScriptableObject
    {
        [Min(32)]
        public int detailResolution = 192;

        [Min(8)]
        public int detailResolutionPerPatch = 16;

        [Range(0.05f, 1f)]
        public float terrainDetailDensity = 0.35f;

        [Min(8f)]
        public float detailDrawDistance = 92f;

        public int crossedPlaneCount = 3;
        public float cardWidth = 0.82f;
        public float cardHeight = 1.08f;
        public float alphaCutoff = 0.02f;
        [Range(0.05f, 1f)]
        public float opacity = 0.35f;

        public Color healthyColor = Color.white;
        public Color dryColor = Color.white;

        public List<MMOClassicGrassFoliageVariation> variations = new();
    }

    [Serializable]
    public sealed class MMOClassicGrassFoliageVariation
    {
        public string displayName;
        public Texture2D texture;
        public GameObject modelPrefab;

        [Tooltip("Minimum painted instance width for this detail type.")]
        [Min(0.01f)]
        public float minWidth = 0.72f;

        [Tooltip("Maximum painted instance width for this detail type.")]
        [Min(0.01f)]
        public float maxWidth = 1.16f;

        [Tooltip("Minimum painted instance height for this detail type.")]
        [Min(0.01f)]
        public float minHeight = 0.68f;

        [Tooltip("Maximum painted instance height for this detail type.")]
        [Min(0.01f)]
        public float maxHeight = 1.32f;

        [Tooltip("Maximum density value written into one Terrain detail cell when using the automated patch painter.")]
        [Min(1)]
        public int maxDensityPerCell = 2;

        public int noiseSeed = 1;
        public float clusterNoiseScale = 0.022f;
        public float clusterThreshold = 0.58f;
        public float fineNoiseScale = 0.115f;
        public float fineThreshold = 0.36f;

        [Range(0.05f, 1f)]
        public float opacity = 0.35f;
    }
}
