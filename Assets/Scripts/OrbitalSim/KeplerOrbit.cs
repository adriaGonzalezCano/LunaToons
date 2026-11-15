using System;

namespace Apollo.OrbitalSim
{
    /// <summary>
    /// Two-body Keplerian orbit around a spherical Earth, no perturbations.
    /// Analytic propagation: the state is a pure function of mission elapsed time,
    /// so it is deterministic and independent of frame rate.
    /// </summary>
    public sealed class KeplerOrbit
    {
        public const double MuEarth = 3.986004418e14;   // m^3/s^2
        public const double EarthRadius = 6_371_000.0;  // m, mean radius (spherical Earth)

        public double SemiMajorAxis { get; }
        public double Eccentricity { get; }
        public double Inclination { get; }          // rad
        public double RightAscension { get; }       // rad, longitude of ascending node
        public double ArgumentOfPeriapsis { get; }  // rad
        public double MeanAnomalyAtEpoch { get; }   // rad, at mission elapsed time 0

        public double MeanMotion { get; }           // rad/s
        public double Period { get; }               // s

        public double PeriapsisAltitude => SemiMajorAxis * (1 - Eccentricity) - EarthRadius; // m
        public double ApoapsisAltitude => SemiMajorAxis * (1 + Eccentricity) - EarthRadius;  // m

        public KeplerOrbit(double semiMajorAxis, double eccentricity, double inclination,
                           double rightAscension, double argumentOfPeriapsis, double meanAnomalyAtEpoch)
        {
            if (semiMajorAxis <= 0) throw new ArgumentOutOfRangeException(nameof(semiMajorAxis));
            if (eccentricity < 0 || eccentricity >= 1) throw new ArgumentOutOfRangeException(nameof(eccentricity), "Only elliptical orbits (0 <= e < 1) are supported.");
            // A periapsis below the surface is allowed on purpose: a burn can create such an orbit. SpacecraftSimulation handles it.

            SemiMajorAxis = semiMajorAxis;
            Eccentricity = eccentricity;
            Inclination = inclination;
            RightAscension = rightAscension;
            ArgumentOfPeriapsis = argumentOfPeriapsis;
            MeanAnomalyAtEpoch = meanAnomalyAtEpoch;

            MeanMotion = Math.Sqrt(MuEarth / (semiMajorAxis * semiMajorAxis * semiMajorAxis));
            Period = 2 * Math.PI / MeanMotion;
        }

        /// <summary>Circular orbit at the given altitude (m) and inclination (degrees).</summary>
        public static KeplerOrbit Circular(double altitudeMeters, double inclinationDegrees)
        {
            return new KeplerOrbit(EarthRadius + altitudeMeters, 0.0, inclinationDegrees * Math.PI / 180.0, 0.0, 0.0, 0.0);
        }

        public OrbitalState Propagate(double missionElapsedTime)
        {
            double e = Eccentricity;

            // Mean anomaly wrapped to [-pi, pi] for a well-conditioned Newton solve.
            double m = MeanAnomalyAtEpoch + MeanMotion * missionElapsedTime;
            m = Math.IEEERemainder(m, 2 * Math.PI);

            double ecc = SolveKepler(m, e);
            double cosE = Math.Cos(ecc), sinE = Math.Sin(ecc);
            double a = SemiMajorAxis;

            // Perifocal frame: x toward periapsis, y 90 deg ahead in the orbital plane.
            double r = a * (1 - e * cosE);
            double xp = a * (cosE - e);
            double yp = a * Math.Sqrt(1 - e * e) * sinE;

            double vScale = Math.Sqrt(MuEarth * a) / r;
            double vxp = -vScale * sinE;
            double vyp = vScale * Math.Sqrt(1 - e * e) * cosE;

            Vector3d position = ToInertial(xp, yp);
            Vector3d velocity = ToInertial(vxp, vyp);

            return new OrbitalState(missionElapsedTime, position, velocity,
                                    position.Magnitude - EarthRadius, velocity.Magnitude, Period);
        }

        /// <summary>Below this eccentricity an orbit is treated as exactly circular (periapsis direction undefined).</summary>
        public const double CircularEccentricity = 1e-8;
        /// <summary>Below this sin(inclination) an orbit is treated as exactly equatorial (node undefined).</summary>
        public const double EquatorialSinInclination = 1e-9;

        /// <summary>
        /// Builds the elliptical orbit that passes through the given inertial position and velocity at the given mission time
        /// (approach A: elements are recomputed after every velocity change). Returns false for escape trajectories (energy >= 0).
        /// Degenerate cases use fixed conventions so the orbit stays consistent: circular orbits get argument of periapsis 0
        /// (the anomaly is then the argument of latitude); equatorial orbits get node 0 (inclination exactly 0 or pi).
        /// </summary>
        public static bool TryFromState(Vector3d position, Vector3d velocity, double missionElapsedTime, out KeplerOrbit orbit)
        {
            orbit = null;
            double r = position.Magnitude;
            double v2 = Vector3d.Dot(velocity, velocity);
            double energy = v2 / 2 - MuEarth / r;
            if (energy >= 0) return false;

            double semiMajorAxis = -MuEarth / (2 * energy);
            Vector3d h = Vector3d.Cross(position, velocity);
            double hMagnitude = h.Magnitude;
            double rv = Vector3d.Dot(position, velocity);
            Vector3d eccentricityVector = (position * (v2 - MuEarth / r) - velocity * rv) * (1.0 / MuEarth);
            double e = eccentricityVector.Magnitude;
            if (e >= 1) return false;

            Vector3d hUnit = h * (1.0 / hMagnitude);
            var node = new Vector3d(-h.Y, h.X, 0.0);
            double nodeMagnitude = node.Magnitude;
            bool equatorial = nodeMagnitude < EquatorialSinInclination * hMagnitude;
            bool circular = e < CircularEccentricity;

            double inclination, rightAscension, argumentOfPeriapsis;
            if (equatorial)
            {
                rightAscension = 0.0;
                inclination = hUnit.Z > 0 ? 0.0 : Math.PI;
                double sign = hUnit.Z > 0 ? 1.0 : -1.0;
                argumentOfPeriapsis = circular ? 0.0 : Math.Atan2(sign * eccentricityVector.Y, eccentricityVector.X);
            }
            else
            {
                inclination = Math.Acos(Math.Max(-1.0, Math.Min(1.0, hUnit.Z)));
                rightAscension = Math.Atan2(node.Y, node.X);
                if (circular)
                {
                    argumentOfPeriapsis = 0.0;
                }
                else
                {
                    Vector3d nodeUnit = node * (1.0 / nodeMagnitude);
                    Vector3d inPlane = Vector3d.Cross(hUnit, nodeUnit);
                    argumentOfPeriapsis = Math.Atan2(Vector3d.Dot(eccentricityVector, inPlane), Vector3d.Dot(eccentricityVector, nodeUnit));
                }
            }

            // True anomaly measured in the same perifocal frame the propagation uses, so the two always agree.
            Vector3d p = Rotate(rightAscension, inclination, argumentOfPeriapsis, 1.0, 0.0);
            Vector3d q = Rotate(rightAscension, inclination, argumentOfPeriapsis, 0.0, 1.0);
            double trueAnomaly = Math.Atan2(Vector3d.Dot(position, q), Vector3d.Dot(position, p));
            double eccentricAnomaly = 2 * Math.Atan2(Math.Sqrt(1 - e) * Math.Sin(trueAnomaly / 2), Math.Sqrt(1 + e) * Math.Cos(trueAnomaly / 2));
            double meanAnomaly = eccentricAnomaly - e * Math.Sin(eccentricAnomaly);
            double meanMotion = Math.Sqrt(MuEarth / (semiMajorAxis * semiMajorAxis * semiMajorAxis));
            double meanAnomalyAtEpoch = Math.IEEERemainder(meanAnomaly - meanMotion * missionElapsedTime, 2 * Math.PI);

            orbit = new KeplerOrbit(semiMajorAxis, e, inclination, rightAscension, argumentOfPeriapsis, meanAnomalyAtEpoch);
            return true;
        }

        private static double SolveKepler(double meanAnomaly, double e)
        {
            double ecc = e < 0.8 ? meanAnomaly : Math.PI;
            for (int i = 0; i < 50; i++)
            {
                double delta = (ecc - e * Math.Sin(ecc) - meanAnomaly) / (1 - e * Math.Cos(ecc));
                ecc -= delta;
                if (Math.Abs(delta) < 1e-12) break;
            }
            return ecc;
        }

        // Rotation perifocal -> inertial: Rz(RAAN) * Rx(inclination) * Rz(argument of periapsis).
        private Vector3d ToInertial(double xp, double yp) => Rotate(RightAscension, Inclination, ArgumentOfPeriapsis, xp, yp);

        private static Vector3d Rotate(double rightAscension, double inclination, double argumentOfPeriapsis, double xp, double yp)
        {
            double cO = Math.Cos(rightAscension), sO = Math.Sin(rightAscension);
            double cw = Math.Cos(argumentOfPeriapsis), sw = Math.Sin(argumentOfPeriapsis);
            double ci = Math.Cos(inclination), si = Math.Sin(inclination);

            double x = (cO * cw - sO * sw * ci) * xp + (-cO * sw - sO * cw * ci) * yp;
            double y = (sO * cw + cO * sw * ci) * xp + (-sO * sw + cO * cw * ci) * yp;
            double z = (sw * si) * xp + (cw * si) * yp;
            return new Vector3d(x, y, z);
        }
    }
}
