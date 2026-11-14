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
    }
}
