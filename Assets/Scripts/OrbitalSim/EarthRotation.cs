using System;

namespace Apollo.OrbitalSim
{
    /// <summary>Geocentric latitude and longitude (radians). Longitude is east-positive, in [-pi, pi].</summary>
    public readonly struct GeoPoint
    {
        public readonly double LatitudeRad;
        public readonly double LongitudeRad;

        public GeoPoint(double latitudeRad, double longitudeRad)
        {
            LatitudeRad = latitudeRad;
            LongitudeRad = longitudeRad;
        }

        public double LatitudeDeg => LatitudeRad * 180.0 / Math.PI;
        public double LongitudeDeg => LongitudeRad * 180.0 / Math.PI;
    }

    /// <summary>
    /// Earth rotation about the +Z axis of the inertial frame at a constant sidereal rate. The orbit stays inertial;
    /// only the ground (map, texture, ground track) rotates. Pure function of mission elapsed time.
    /// </summary>
    public static class EarthRotation
    {
        public const double AngularVelocity = 7.2921159e-5; // rad/s (sidereal: one turn in 86164.09 s)
        public const double AngleAtEpoch = 0.0;             // rad: Greenwich meridian along +X at MET 0 (working assumption)

        /// <summary>Rotation angle of the Earth-fixed frame relative to inertial, wrapped to [-pi, pi].</summary>
        public static double Angle(double missionElapsedTime)
        {
            return Math.IEEERemainder(AngleAtEpoch + AngularVelocity * missionElapsedTime, 2 * Math.PI);
        }

        /// <summary>Sub-spacecraft point on the (spherical) Earth for an inertial position at the given time.</summary>
        public static GeoPoint SubPoint(Vector3d inertialPosition, double missionElapsedTime)
        {
            double latitude = Math.Asin(inertialPosition.Z / inertialPosition.Magnitude);
            double longitude = Math.IEEERemainder(
                Math.Atan2(inertialPosition.Y, inertialPosition.X) - AngleAtEpoch - AngularVelocity * missionElapsedTime,
                2 * Math.PI);
            return new GeoPoint(latitude, longitude);
        }
    }
}
