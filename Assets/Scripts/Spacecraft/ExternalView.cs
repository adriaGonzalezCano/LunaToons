using Apollo.OrbitalSim;
using Apollo.Shared;
using UnityEngine;

namespace Apollo.Spacecraft
{
    /// <summary>
    /// What the crew sees outside: Earth and stars, driven by the shared simulation state.
    /// The interior stays at the origin at 1:1 scale. The outside world is uniformly scaled by 1/metersPerUnit
    /// around the camera, which keeps all viewing angles correct while avoiding float precision problems.
    /// Spacecraft attitude (V1, fixed): +X along velocity, -Z toward Earth (the window wall faces Earth), Y = orbit normal.
    /// </summary>
    public sealed class ExternalView : MonoBehaviour
    {
        [SerializeField] private SimulationHost host;
        [SerializeField] private float metersPerUnit = 1000f;
        [SerializeField] private int starCount = 400;

        private Transform root;
        private Transform earth;

        private void Awake()
        {
            if (host == null) host = FindFirstObjectByType<SimulationHost>();

            root = new GameObject("ExternalWorld").transform;
            root.SetParent(transform, false);

            float earthDiameter = (float)(2.0 * KeplerOrbit.EarthRadius / metersPerUnit);
            Mesh highResSphere = ViewFactory.CreateSphereMesh(128, 64); // fine mesh: the surface is only ~185 units away
            earth = ViewFactory.CreateMeshObject("Earth", root, highResSphere, ViewFactory.EarthSphereMaterial(), earthDiameter, Vector3.zero).transform;

            BuildStars();
        }

        private void LateUpdate()
        {
            if (host == null || host.Orbit == null) return;
            var state = host.CurrentState;

            Vector3 r = ViewMapping.ToUnity(state.Position);
            Vector3 v = ViewMapping.ToUnity(state.Velocity);

            Vector3 zc = r.normalized;                                  // craft +Z: away from Earth
            Vector3 xc = (v - Vector3.Dot(v, zc) * zc).normalized;     // craft +X: along velocity
            Vector3 yc = Vector3.Cross(zc, xc);                         // craft +Y: orbit normal
            Quaternion craftAttitude = Quaternion.LookRotation(zc, yc);

            // Rotate the outside world by the inverse attitude instead of rotating the interior.
            root.localRotation = Quaternion.Inverse(craftAttitude);
            earth.localPosition = -r / metersPerUnit;

            // Earth rotates under the inertial orbit. ECI rotation +theta about Z = Unity rotation -theta about Y (axis mapping flips handedness).
            earth.localRotation = Quaternion.Euler(0f, (float)(-EarthRotation.Angle(state.MissionElapsedTime) * Mathf.Rad2Deg), 0f);
        }

        private void BuildStars()
        {
            Mesh starMesh = ViewFactory.CreateSphereMesh(6, 4);
            Material starMaterial = ViewFactory.Unlit(Color.white);
            var random = new System.Random(1234);
            for (int i = 0; i < starCount; i++)
            {
                float cosTheta = (float)(random.NextDouble() * 2.0 - 1.0);
                float sinTheta = Mathf.Sqrt(1f - cosTheta * cosTheta);
                float phi = (float)(random.NextDouble() * 2.0 * Mathf.PI);
                Vector3 direction = new Vector3(sinTheta * Mathf.Cos(phi), cosTheta, sinTheta * Mathf.Sin(phi));
                ViewFactory.CreateMeshObject("Star", root, starMesh, starMaterial, 30f, direction * 14000f);
            }
        }
    }
}
