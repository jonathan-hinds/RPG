using UnityEngine;

namespace RPGClone.World
{
    public enum MMOMapMarkerType
    {
        QuestObjective,
        QuestTurnIn,
        QuestNpc,
        QuestCreatureArea,
        QuestWorldObject,
        QuestArea,
        PlayerCharacter
    }

    public readonly struct MMOMapMarkerData
    {
        public readonly string MarkerId;
        public readonly string Label;
        public readonly string Detail;
        public readonly Vector3 WorldPosition;
        public readonly float Radius;
        public readonly MMOMapMarkerType MarkerType;
        public readonly Color Color;
        public readonly bool IsArea;
        public readonly float HeadingDegrees;

        public MMOMapMarkerData(
            string markerId,
            string label,
            string detail,
            Vector3 worldPosition,
            float radius,
            MMOMapMarkerType markerType,
            Color color,
            bool isArea,
            float headingDegrees = 0f)
        {
            MarkerId = markerId;
            Label = label;
            Detail = detail;
            WorldPosition = worldPosition;
            Radius = Mathf.Max(0f, radius);
            MarkerType = markerType;
            Color = color;
            IsArea = isArea;
            HeadingDegrees = headingDegrees;
        }
    }
}
