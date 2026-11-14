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

        private void Start()
        {
            if (host == null) host = FindFirstObjectByType<SimulationHost>();

            float earthDiameter = (float)(2.0 * KeplerOrbit.EarthRadius / metersPerUnit);
            ViewFactory.CreateMeshObject("Earth", transform, ViewFactory.CreateSphereMesh(64, 32), ViewFactory.EarthMaterial(), earthDiameter, Vector3.zero);

            marker = ViewFactory.CreateMeshObject("Spacecraft", transform, ViewFactory.CreateSphereMesh(12, 8), ViewFactory.Unlit(Color.red), 3f, Vector3.zero).transform;

            BuildTrajectory();
        }

        private void Update()
        {
            if (host == null || host.Orbit == null || marker == null) return;
            marker.localPosition = ViewMapping.ToUnity(host.CurrentState.Position, metersPerUnit);
        }

        private void BuildTrajectory()
        {
            var lineObject = new GameObject("Trajectory");
            lineObject.transform.SetParent(transform, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = trajectorySamples;
            line.widthMultiplier = 0.5f;
            line.sharedMaterial = ViewFactory.Unlit(Color.yellow);

            KeplerOrbit orbit = host.Orbit;
            for (int i = 0; i < trajectorySamples; i++)
            {
                double t = orbit.Period * i / trajectorySamples;
                line.SetPosition(i, ViewMapping.ToUnity(orbit.Propagate(t).Position, metersPerUnit));
            }
        }
    }
}
