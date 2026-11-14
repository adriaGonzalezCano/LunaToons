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

        public KeplerOrbit(double semiMajorAxis, double eccentricity, double inclination,
                           double rightAscension, double argumentOfPeriapsis, double meanAnomalyAtEpoch)
        {
            if (semiMajorAxis <= 0) throw new ArgumentOutOfRangeException(nameof(semiMajorAxis));
            if (eccentricity < 0 || eccentricity >= 1) throw new ArgumentOutOfRangeException(nameof(eccentricity), "Only elliptical orbits (0 <= e < 1) are supported.");
            if (semiMajorAxis * (1 - eccentricity) <= EarthRadius) throw new ArgumentException("Periapsis is below the Earth surface.");

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
        private Vector3d ToInertial(double xp, double yp)
        {
            double cO = Math.Cos(RightAscension), sO = Math.Sin(RightAscension);
            double cw = Math.Cos(ArgumentOfPeriapsis), sw = Math.Sin(ArgumentOfPeriapsis);
            double ci = Math.Cos(Inclination), si = Math.Sin(Inclination);

            double x = (cO * cw - sO * sw * ci) * xp + (-cO * sw - sO * cw * ci) * yp;
            double y = (sO * cw + cO * sw * ci) * xp + (-sO * sw + cO * cw * ci) * yp;
            double z = (sw * si) * xp + (cw * si) * yp;
            return new Vector3d(x, y, z);
        }
    }
}
