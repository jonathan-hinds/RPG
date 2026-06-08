using System;
using RPGClone.World;
using UnityEngine;

namespace RPGClone.Quests
{
    [Serializable]
    public sealed class MMOQuestObjectiveMapHint
    {
        [SerializeField] private string markerId = "marker";
        [SerializeField] private string label;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField, Min(0f)] private float radius = 12f;
        [SerializeField] private MMOMapMarkerType markerType = MMOMapMarkerType.QuestObjective;
        [SerializeField] private bool area;

        public string MarkerId => string.IsNullOrWhiteSpace(markerId) ? label : markerId;
        public string Label => label;
        public Vector3 WorldPosition => worldPosition;
        public float Radius => radius;
        public MMOMapMarkerType MarkerType => markerType;
        public bool Area => area;

        public void Configure(string newMarkerId, string newLabel, Vector3 newWorldPosition, float newRadius, MMOMapMarkerType newMarkerType, bool newArea)
        {
            markerId = string.IsNullOrWhiteSpace(newMarkerId) ? newLabel : newMarkerId.Trim();
            label = newLabel;
            worldPosition = newWorldPosition;
            radius = Mathf.Max(0f, newRadius);
            markerType = newMarkerType;
            area = newArea;
        }
    }
}
