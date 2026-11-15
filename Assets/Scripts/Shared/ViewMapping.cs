using Apollo.OrbitalSim;
using UnityEngine;

namespace Apollo.Shared
{
    /// <summary>
    /// The only place where simulation space (ECI, Z north, right-handed, metres) becomes Unity space (Y up, left-handed).
    /// Mapping: Unity (x, y, z) = ECI (x, z, y). Optionally scaled by metersPerUnit.
    /// </summary>
    public static class ViewMapping
    {
        public static Vector3 ToUnity(Vector3d eci, double metersPerUnit = 1.0)
        {
            return new Vector3((float)(eci.X / metersPerUnit), (float)(eci.Z / metersPerUnit), (float)(eci.Y / metersPerUnit));
        }

        /// <summary>
        /// Orientation of the cabin (and of any object attached to the spacecraft) in Unity world space, from the simulated attitude.
        /// Body axes match the cabin: body +X = cabin +X (forward, couches face it), body +Y (left) = cabin +Z, body +Z (up) = cabin +Y.
        /// </summary>
        public static Quaternion CabinToWorld(Quaterniond attitude)
        {
            Vector3 cabinForwardZ = ToUnity(attitude.Rotate(new Vector3d(0, 1, 0)));
            Vector3 cabinUpY = ToUnity(attitude.Rotate(new Vector3d(0, 0, 1)));
            return Quaternion.LookRotation(cabinForwardZ, cabinUpY);
        }
    }
}
