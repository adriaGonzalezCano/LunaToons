using System;

namespace Apollo.OrbitalSim
{
    /// <summary>
    /// The path the spacecraft follows while coasting (engine off): exact two-body motion from a state vector
    /// (position and velocity at an epoch), solved with universal variables. Because it starts from a state vector
    /// it has no orbital-element singularities (circular and equatorial orbits need no special cases), and any velocity
    /// change simply starts a new CoastOrbit from the new state. Bound (elliptical) orbits only.
    /// Like KeplerOrbit it is a pure function of mission time, so it is deterministic and independent of frame rate.
    /// </summary>
    public sealed class CoastOrbit
    {
        private static readonly double SqrtMu = Math.Sqrt(KeplerOrbit.MuEarth);

        private readonly Vector3d position0;
        private readonly Vector3d velocity0;
        private readonly double epoch;     // mission time of the starting state, s
        private readonly double radius0;   // m
        private readonly double alpha;     // 1 / semi-major axis, 1/m
        private readonly double sigma0;    // (r0 . v0) / sqrt(mu)

        // Osculating elements of this orbit, for telemetry and display. All derived from the starting state.
        public double SemiMajorAxis { get; }
        public double Eccentricity { get; }
        public double Inclination { get; }   // rad
        public double Period { get; }        // s
        public double PeriapsisAltitude => SemiMajorAxis * (1 - Eccentricity) - KeplerOrbit.EarthRadius; // m
        public double ApoapsisAltitude => SemiMajorAxis * (1 + Eccentricity) - KeplerOrbit.EarthRadius;  // m

        private CoastOrbit(Vector3d position, Vector3d velocity, double missionElapsedTime, double alpha)
        {
            position0 = position;
            velocity0 = velocity;
            epoch = missionElapsedTime;
            radius0 = position.Magnitude;
            this.alpha = alpha;
            sigma0 = Vector3d.Dot(position, velocity) / SqrtMu;

            SemiMajorAxis = 1.0 / alpha;
            Period = 2 * Math.PI / (SqrtMu * Math.Pow(alpha, 1.5));

            double v2 = Vector3d.Dot(velocity, velocity);
            Vector3d eccentricityVector = (position * (v2 - KeplerOrbit.MuEarth / radius0) - velocity * Vector3d.Dot(position, velocity)) * (1.0 / KeplerOrbit.MuEarth);
            Eccentricity = eccentricityVector.Magnitude;

            Vector3d h = Vector3d.Cross(position, velocity);
            Inclination = Math.Acos(Math.Max(-1.0, Math.Min(1.0, h.Z / h.Magnitude)));
        }

        /// <summary>Orbit through the given inertial state at the given mission time. False if the orbit is not bound (escape).</summary>
        public static bool TryCreate(Vector3d position, Vector3d velocity, double missionElapsedTime, out CoastOrbit orbit)
        {
            orbit = null;
            double r = position.Magnitude;
            if (!(r > 0)) return false;

            double alpha = 2.0 / r - Vector3d.Dot(velocity, velocity) / KeplerOrbit.MuEarth;
            if (!(alpha > 1e-12)) return false; // energy >= 0: parabolic or hyperbolic

            orbit = new CoastOrbit(position, velocity, missionElapsedTime, alpha);
            return true;
        }

        /// <summary>The coasting orbit that starts from the element-defined orbit's state at the given mission time.</summary>
        public static CoastOrbit FromKepler(KeplerOrbit orbit, double missionElapsedTime)
        {
            OrbitalState state = orbit.Propagate(missionElapsedTime);
            if (!TryCreate(state.Position, state.Velocity, missionElapsedTime, out CoastOrbit coast))
                throw new ArgumentException("The orbit is not bound.", nameof(orbit));
            return coast;
        }

        public OrbitalState Propagate(double missionElapsedTime)
        {
            // The motion repeats every period, so reduce the time span first (keeps precision over long spans).
            double dt = Math.IEEERemainder(missionElapsedTime - epoch, Period);

            // Solve the universal Kepler equation F(chi) = 0. F is strictly increasing (dF/dchi = r > 0) and the root lies within
            // +-2*pi/sqrt(alpha) for a time span of at most half a period, so a bracketed Newton iteration (bisection as the
            // fallback) always converges, even for eccentricities close to 1 where plain Newton can diverge.
            double bound = 2 * Math.PI / Math.Sqrt(alpha);
            double low = -bound, high = bound;
            double chi = Math.Max(low, Math.Min(high, SqrtMu * dt * alpha));
            double c, s, r;
            for (int i = 0; i < 200; i++)
            {
                double z = alpha * chi * chi;
                c = StumpffC(z);
                s = StumpffS(z);
                r = chi * chi * c + sigma0 * chi * (1 - z * s) + radius0 * (1 - z * c);
                double f = sigma0 * chi * chi * c + (1 - alpha * radius0) * chi * chi * chi * s + radius0 * chi - SqrtMu * dt;
                if (f < 0) low = chi; else high = chi;

                double next = chi - f / r;
                if (!(next > low && next < high)) next = 0.5 * (low + high); // Newton left the bracket (or is NaN): bisect
                bool converged = Math.Abs(next - chi) < 1e-9;
                chi = next;
                if (converged) break;
            }

            double zf = alpha * chi * chi;
            c = StumpffC(zf);
            s = StumpffS(zf);
            r = chi * chi * c + sigma0 * chi * (1 - zf * s) + radius0 * (1 - zf * c);

            double fCoefficient = 1 - chi * chi * c / radius0;
            double gCoefficient = dt - chi * chi * chi * s / SqrtMu;
            double fDot = SqrtMu / (r * radius0) * chi * (zf * s - 1);
            double gDot = 1 - chi * chi * c / r;

            Vector3d position = position0 * fCoefficient + velocity0 * gCoefficient;
            Vector3d velocity = position0 * fDot + velocity0 * gDot;
            return new OrbitalState(missionElapsedTime, position, velocity, position.Magnitude - KeplerOrbit.EarthRadius, velocity.Magnitude, Period);
        }

        private static double StumpffC(double z)
        {
            if (Math.Abs(z) < 1e-6) return 0.5 - z / 24.0 + z * z / 720.0;
            if (z > 0) return (1 - Math.Cos(Math.Sqrt(z))) / z;
            return (Math.Cosh(Math.Sqrt(-z)) - 1) / -z;
        }

        private static double StumpffS(double z)
        {
            if (Math.Abs(z) < 1e-6) return 1.0 / 6.0 - z / 120.0 + z * z / 5040.0;
            if (z > 0)
            {
                double root = Math.Sqrt(z);
                return (root - Math.Sin(root)) / (root * root * root);
            }
            double rootNegative = Math.Sqrt(-z);
            return (Math.Sinh(rootNegative) - rootNegative) / (rootNegative * rootNegative * rootNegative);
        }
    }
}
