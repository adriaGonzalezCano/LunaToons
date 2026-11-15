using Apollo.OrbitalSim;
using Apollo.Shared;
using UnityEngine;

namespace Apollo.MissionControl
{
    /// <summary>
    /// Mission Control 3D orbit view: scaled Earth, full orbit trajectory and the spacecraft marker.
    /// The marker is placed every frame from the shared simulation state; nothing here is animated independently.
    /// </summary>
    public sealed class OrbitView : MonoBehaviour
    {
        [SerializeField] private SimulationHost host;
        [SerializeField] private float metersPerUnit = 100000f; // 1 unit = 100 km
        [SerializeField] private int trajectorySamples = 256;

        private Transform marker;
        private Transform earth;
        private LineRenderer line;
        private int builtOrbitVersion = -1;

        private void Start()
        {
            if (host == null) host = FindFirstObjectByType<SimulationHost>();

            float earthDiameter = (float)(2.0 * KeplerOrbit.EarthRadius / metersPerUnit);
            earth = ViewFactory.CreateMeshObject("Earth", transform, ViewFactory.CreateSphereMesh(64, 32), ViewFactory.EarthSphereMaterial(), earthDiameter, Vector3.zero).transform;

            marker = ViewFactory.CreateMeshObject("Spacecraft", transform, ViewFactory.CreateSphereMesh(12, 8), ViewFactory.Unlit(Color.red), 3f, Vector3.zero).transform;

            CreateTrajectoryLine();
        }

        private void Update()
        {
            if (host == null || host.Orbit == null || marker == null || earth == null) return;
            if (host.OrbitVersion != builtOrbitVersion) RebuildTrajectory();

            var state = host.CurrentState;
            marker.localPosition = ViewMapping.ToUnity(state.Position, metersPerUnit);
            earth.localRotation = Quaternion.Euler(0f, (float)(-EarthRotation.Angle(state.MissionElapsedTime) * Mathf.Rad2Deg), 0f);
        }

        private void CreateTrajectoryLine()
        {
            var lineObject = new GameObject("Trajectory");
            lineObject.transform.SetParent(transform, false);
            line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = trajectorySamples;
            line.widthMultiplier = 0.5f;
            line.sharedMaterial = ViewFactory.Unlit(Color.yellow);
        }

        // The ring is the current orbit; it is rebuilt whenever a velocity change (or restart) replaces the orbit.
        private void RebuildTrajectory()
        {
            CoastOrbit orbit = host.Orbit;
            double now = host.CurrentState.MissionElapsedTime;
            for (int i = 0; i < trajectorySamples; i++)
            {
                double t = now + orbit.Period * i / trajectorySamples;
                line.SetPosition(i, ViewMapping.ToUnity(orbit.Propagate(t).Position, metersPerUnit));
            }
            builtOrbitVersion = host.OrbitVersion;
        }
    }
}
