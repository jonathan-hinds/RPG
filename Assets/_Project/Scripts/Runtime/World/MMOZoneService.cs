using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGClone.World
{
    public sealed class MMOZoneService : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private List<MMOZoneDefinition> zones = new();
        [SerializeField] private MMOZoneDefinition currentZone;
        [SerializeField] private bool autoResolvePlayer = true;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        private float nextRefreshTime;
        private MMOZoneDefinition runtimeTerrainZone;

        public event Action<MMOZoneDefinition> ZoneChanged;
        public MMOZoneDefinition CurrentZone => currentZone;
        public IReadOnlyList<MMOZoneDefinition> Zones => zones;

        private void Awake()
        {
            ResolveReferences();
            EnsureCurrentZone();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + refreshInterval;
            ResolveReferences();
            RefreshCurrentZone();
        }

        public void Configure(Transform newPlayer, IEnumerable<MMOZoneDefinition> newZones)
        {
            player = newPlayer;
            zones = newZones != null ? new List<MMOZoneDefinition>(newZones) : new List<MMOZoneDefinition>();
            EnsureCurrentZone();
            RefreshCurrentZone(true);
        }

        public void SetPlayer(Transform newPlayer)
        {
            player = newPlayer;
            RefreshCurrentZone(true);
        }

        private void ResolveReferences()
        {
            if (!autoResolvePlayer || player != null)
            {
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            player = playerObject != null ? playerObject.transform : null;
        }

        private void EnsureCurrentZone()
        {
            if (zones.Count == 0)
            {
                MMOZoneDefinition terrainZone = GetOrCreateRuntimeTerrainZone();
                if (terrainZone != null)
                {
                    zones.Add(terrainZone);
                }
            }

            currentZone ??= zones.Count > 0 ? zones[0] : null;
        }

        private void RefreshCurrentZone(bool forceEvent = false)
        {
            EnsureCurrentZone();
            MMOZoneDefinition nextZone = ResolveZoneForPlayer();
            if (nextZone == currentZone && !forceEvent)
            {
                return;
            }

            currentZone = nextZone;
            ZoneChanged?.Invoke(currentZone);
        }

        private MMOZoneDefinition ResolveZoneForPlayer()
        {
            if (player == null)
            {
                return currentZone != null ? currentZone : zones.Count > 0 ? zones[0] : null;
            }

            Vector3 position = player.position;
            foreach (MMOZoneDefinition zone in zones)
            {
                if (zone != null && zone.Contains(position))
                {
                    return zone;
                }
            }

            return currentZone != null ? currentZone : zones.Count > 0 ? zones[0] : null;
        }

        private MMOZoneDefinition GetOrCreateRuntimeTerrainZone()
        {
            if (runtimeTerrainZone != null)
            {
                return runtimeTerrainZone;
            }

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
            {
                return null;
            }

            Vector3 size = terrain.terrainData.size;
            Vector3 center = terrain.transform.position + size * 0.5f;
            Bounds bounds = new(center, new Vector3(size.x, Mathf.Max(size.y, 160f), size.z));
            runtimeTerrainZone = ScriptableObject.CreateInstance<MMOZoneDefinition>();
            runtimeTerrainZone.Configure("runtime_terrain_zone", terrain.name, bounds);
            return runtimeTerrainZone;
        }
    }
}
