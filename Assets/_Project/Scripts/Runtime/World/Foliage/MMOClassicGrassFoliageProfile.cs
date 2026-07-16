using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.World.Foliage
{
    [CreateAssetMenu(menuName = "RPG Clone/World/Classic Grass Foliage Profile")]
    public sealed class MMOClassicGrassFoliageProfile : ScriptableObject
    {
        public static readonly Color DefaultHealthyColor = new(0.42f, 0.70f, 0.48f, 1f);
        public static readonly Color DefaultDryColor = new(0.72f, 0.56f, 0.34f, 1f);

        [Min(32)]
        public int detailResolution = 192;

        [Min(8)]
        public int detailResolutionPerPatch = 16;

        [Range(0.05f, 1f)]
        public float terrainDetailDensity = 0.35f;

        [Min(8f)]
        public float detailDrawDistance = 92f;

        [Header("Terrain Wind")]
        [Tooltip("Strength of Unity Terrain's native grass and foliage bending.")]
        [Range(0f, 1f)]
        public float terrainWindStrength = 0.25f;

        [Tooltip("Distance that Unity Terrain's wind displaces grass and foliage vertices.")]
        [Range(0f, 1f)]
        public float terrainWindAmount = 0.25f;

        [Header("Terrain Placement")]
        [Tooltip("Terrain details are never painted above this unsigned slope angle. This applies equally to inclines and declines.")]
        [Range(0f, 90f)]
        public float maximumDetailSlopeDegrees = MMOTerrainDetailSlopePolicy.DefaultMaximumSlopeDegrees;

        [Tooltip("Foliage density fades over this many degrees before reaching the maximum slope. Set to zero for a hard cutoff.")]
        [Range(0f, 90f)]
        public float detailSlopeFadeRangeDegrees = MMOTerrainDetailSlopePolicy.DefaultSlopeFadeRangeDegrees;

        public int crossedPlaneCount = 3;
        public float cardWidth = 0.82f;
        public float cardHeight = 1.08f;
        public float alphaCutoff = 0.02f;

        [Header("Terrain Color Blending")]
        [Tooltip("Tint for larger, healthier grass clumps. This multiplies the grass texture and should match the terrain's grass palette.")]
        public Color healthyColor = DefaultHealthyColor;

        [Tooltip("Tint for smaller, drier grass clumps. Unity blends between this and Healthy Color using native terrain detail variation.")]
        public Color dryColor = DefaultDryColor;

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
    }
}
