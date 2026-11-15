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
        private Transform bodyAxes;
        private Transform earth;
        private LineRenderer line;
        private int builtOrbitVersion = -1;

        private void Start()
        {
            if (host == null) host = FindFirstObjectByType<SimulationHost>();

            float earthDiameter = (float)(2.0 * KeplerOrbit.EarthRadius / metersPerUnit);
            earth = ViewFactory.CreateMeshObject("Earth", transform, ViewFactory.CreateSphereMesh(64, 32), ViewFactory.EarthSphereMaterial(), earthDiameter, Vector3.zero).transform;

            marker = ViewFactory.CreateMeshObject("Spacecraft", transform, ViewFactory.CreateSphereMesh(12, 8), ViewFactory.Unlit(Color.red), 3f, Vector3.zero).transform;

            CreateBodyAxes();
            CreateTrajectoryLine();
        }

        // Body axes drawn at the spacecraft: white = nose (+X, longest), cyan = up (+Z), green = left (+Y).
        private void CreateBodyAxes()
        {
            bodyAxes = new GameObject("BodyAxes").transform;
            bodyAxes.SetParent(transform, false);
            AddAxisBar("Nose+X", Color.white, new Vector3(14f, 0.6f, 0.6f), new Vector3(7f, 0f, 0f));
            AddAxisBar("Up+Z", Color.cyan, new Vector3(0.6f, 9f, 0.6f), new Vector3(0f, 4.5f, 0f));
            AddAxisBar("Left+Y", Color.green, new Vector3(0.6f, 0.6f, 9f), new Vector3(0f, 0f, 4.5f));
        }

        private void AddAxisBar(string barName, Color color, Vector3 size, Vector3 center)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = barName;
            Destroy(bar.GetComponent<Collider>());
            bar.GetComponent<Renderer>().sharedMaterial = ViewFactory.Unlit(color);
            bar.transform.SetParent(bodyAxes, false);
            bar.transform.localScale = size;
            bar.transform.localPosition = center;
        }

        private void Update()
        {
            if (host == null || host.Orbit == null || marker == null || earth == null || bodyAxes == null) return;
            if (host.OrbitVersion != builtOrbitVersion) RebuildTrajectory();

            var state = host.CurrentState;
            marker.localPosition = ViewMapping.ToUnity(state.Position, metersPerUnit);
            bodyAxes.localPosition = marker.localPosition;
            bodyAxes.localRotation = ViewMapping.CabinToWorld(host.Attitude.Attitude);
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
