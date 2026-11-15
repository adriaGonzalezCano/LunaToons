using System;
using System.Collections.Generic;
using Apollo.OrbitalSim;
using Apollo.Shared;
using UnityEngine;

namespace Apollo.MissionControl
{
    /// <summary>
    /// Mission Control view 2: flat equirectangular Earth map with the ground track.
    /// The past track (up to one orbit back) is the recorded history; the predicted track (1.5 orbits ahead) is recomputed
    /// every frame from the current orbit and the Earth rotation, so both always agree with the simulation state.
    /// Map: 200 x 100 units, longitude -180..180 maps to x -100..100, latitude -90..90 maps to y -50..50.
    /// </summary>
    public sealed class GroundTrackView : MonoBehaviour
    {
        private const float MapWidth = 200f;
        private const float MapHeight = 100f;

        [SerializeField] private SimulationHost host;
        [SerializeField] private int samplesPerTrack = 240;

        private Camera mapCamera;
        private Transform marker;
        private Material pastMaterial, futureMaterial;
        private readonly List<LineRenderer> pastLines = new List<LineRenderer>();
        private readonly List<LineRenderer> futureLines = new List<LineRenderer>();
        private readonly List<Vector3> segment = new List<Vector3>();
        private readonly List<GeoPoint> points = new List<GeoPoint>();

        private void Start()
        {
            if (host == null) host = FindFirstObjectByType<SimulationHost>();
            mapCamera = GetComponentInChildren<Camera>(true);

            ViewFactory.CreateMeshObject("Map", transform, ViewFactory.CreateQuadMesh(), ViewFactory.FlatMapMaterial(), 1f, Vector3.zero)
                       .transform.localScale = new Vector3(MapWidth, MapHeight, 1f);

            BuildGraticule();
            pastMaterial = ViewFactory.Unlit(new Color(0.4f, 0.85f, 1f));
            futureMaterial = ViewFactory.Unlit(Color.yellow);
            marker = ViewFactory.CreateMeshObject("Spacecraft", transform, ViewFactory.CreateSphereMesh(12, 8), ViewFactory.Unlit(Color.red), 3.5f, new Vector3(0f, 0f, -0.6f)).transform;
        }

        private void Update()
        {
            if (host == null || host.Orbit == null || marker == null) return;

            OrbitalState state = host.CurrentState;
            CoastOrbit orbit = host.Orbit;
            double now = state.MissionElapsedTime;

            GeoPoint here = EarthRotation.SubPoint(state.Position, now);

            // Past: recorded history (the orbit may have changed since), up to one period back, joined to the current point.
            points.Clear();
            IReadOnlyList<TrackSample> history = host.TrackHistory.Samples;
            for (int i = 0; i < history.Count; i++)
                if (history[i].MissionElapsedTime >= now - orbit.Period) points.Add(history[i].Point);
            points.Add(here);
            RebuildTrack(pastLines, pastMaterial);

            // Future: predicted from the current orbit.
            points.Clear();
            for (int i = 0; i <= samplesPerTrack; i++)
            {
                double t = now + 1.5 * orbit.Period * i / samplesPerTrack;
                points.Add(EarthRotation.SubPoint(orbit.Propagate(t).Position, t));
            }
            RebuildTrack(futureLines, futureMaterial);

            marker.localPosition = new Vector3(ToMapX(here), ToMapY(here), -0.6f);
        }

        // Fit the whole map into the upper part of the screen, leaving the bottom for the telemetry strip.
        private void LateUpdate()
        {
            if (mapCamera == null) return;
            float size = Mathf.Max(MapHeight * 1.05f / 0.78f / 2f, MapWidth * 1.04f / mapCamera.aspect / 2f);
            mapCamera.orthographicSize = size;
            mapCamera.transform.localPosition = new Vector3(0f, -0.22f * size, -100f);
        }

        private static float ToMapX(GeoPoint p) => (float)(p.LongitudeRad / Math.PI) * (MapWidth / 2f);
        private static float ToMapY(GeoPoint p) => (float)(p.LatitudeRad / (Math.PI / 2.0)) * (MapHeight / 2f);

        private void RebuildTrack(List<LineRenderer> pool, Material material)
        {
            int used = 0;
            segment.Clear();
            double previousLongitude = 0.0;

            for (int i = 0; i < points.Count; i++)
            {
                GeoPoint point = points[i];

                // Break the line where the track wraps across the +-180 degree edge of the map.
                if (i > 0 && Math.Abs(point.LongitudeRad - previousLongitude) > Math.PI)
                    FlushSegment(pool, material, ref used);

                segment.Add(new Vector3(ToMapX(point), ToMapY(point), -0.3f));
                previousLongitude = point.LongitudeRad;
            }
            FlushSegment(pool, material, ref used);

            for (int k = used; k < pool.Count; k++) pool[k].positionCount = 0;
        }

        private void FlushSegment(List<LineRenderer> pool, Material material, ref int used)
        {
            if (segment.Count >= 2)
            {
                if (used == pool.Count) pool.Add(CreateLine("TrackSegment", material, 0.6f));
                LineRenderer line = pool[used++];
                line.positionCount = segment.Count;
                for (int i = 0; i < segment.Count; i++) line.SetPosition(i, segment[i]);
            }
            segment.Clear();
        }

        private void BuildGraticule()
        {
            Material grid = ViewFactory.Unlit(new Color(0.85f, 0.85f, 0.85f));
            Material equator = ViewFactory.Unlit(Color.white);

            for (int lat = -60; lat <= 60; lat += 30)
            {
                float y = lat / 90f * (MapHeight / 2f);
                LineRenderer line = CreateLine("Lat" + lat, lat == 0 ? equator : grid, lat == 0 ? 0.35f : 0.15f);
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(-MapWidth / 2f, y, -0.1f));
                line.SetPosition(1, new Vector3(MapWidth / 2f, y, -0.1f));
            }
            for (int lon = -150; lon <= 150; lon += 30)
            {
                float x = lon / 180f * (MapWidth / 2f);
                LineRenderer line = CreateLine("Lon" + lon, lon == 0 ? equator : grid, lon == 0 ? 0.35f : 0.15f);
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(x, -MapHeight / 2f, -0.1f));
                line.SetPosition(1, new Vector3(x, MapHeight / 2f, -0.1f));
            }
        }

        private LineRenderer CreateLine(string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.widthMultiplier = width;
            line.sharedMaterial = material;
            line.positionCount = 0;
            return line;
        }
    }
}
