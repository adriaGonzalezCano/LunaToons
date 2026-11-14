using System;
using Apollo.OrbitalSim;
using Apollo.Shared;
using UnityEditor;
using UnityEngine;

namespace Apollo.EditorTools
{
    /// <summary>
    /// Menu "Apollo > Run Simulation Checks": runs the real C# orbital code against basic orbital mechanics
    /// relationships and reports PASS/FAIL in the Console. No scene or Play mode needed.
    /// </summary>
    public static class ApolloSimulationChecks
    {
        private static int passed, failed;

        [MenuItem("Apollo/Run Simulation Checks")]
        public static void Run()
        {
            passed = 0;
            failed = 0;

            CheckCircular();
            CheckElliptical(KeplerOrbit.EarthRadius + 0.0, 12_000_000.0, 0.3, 51.6);
            CheckEarthRotation();
            CheckClock();
            CheckViewMapping();

            string summary = $"Apollo simulation checks: {passed} passed, {failed} failed.";
            if (failed == 0) Debug.Log(summary); else Debug.LogError(summary);
        }

        private static void CheckCircular()
        {
            double altitude = 185_000.0;
            var orbit = KeplerOrbit.Circular(altitude, 32.5);
            double r = KeplerOrbit.EarthRadius + altitude;

            Expect("Circular: period matches Kepler's third law",
                   Math.Abs(orbit.Period - 2 * Math.PI * Math.Sqrt(r * r * r / KeplerOrbit.MuEarth)) < 1e-6);
            Expect("Circular: apsis altitudes equal orbit altitude",
                   Math.Abs(orbit.PeriapsisAltitude - altitude) < 1e-6 && Math.Abs(orbit.ApoapsisAltitude - altitude) < 1e-6);

            double worstAltitude = 0, worstSpeed = 0, worstLatitude = 0;
            double circularSpeed = Math.Sqrt(KeplerOrbit.MuEarth / r);
            for (int i = 0; i <= 3000; i++)
            {
                OrbitalState s = orbit.Propagate(orbit.Period * 3 * i / 3000);
                worstAltitude = Math.Max(worstAltitude, Math.Abs(s.Altitude - altitude));
                worstSpeed = Math.Max(worstSpeed, Math.Abs(s.Speed - circularSpeed));
                worstLatitude = Math.Max(worstLatitude, Math.Abs(Math.Asin(s.Position.Z / s.Position.Magnitude)));
            }
            Expect($"Circular: altitude constant over 3 orbits (worst error {worstAltitude:E1} m)", worstAltitude < 1e-3);
            Expect($"Circular: speed equals sqrt(mu/r) (worst error {worstSpeed:E1} m/s)", worstSpeed < 1e-6);
            Expect("Circular: latitude never exceeds inclination (32.5 deg)",
                   worstLatitude <= 32.5 * Math.PI / 180.0 + 1e-9 && worstLatitude > 32.4 * Math.PI / 180.0);

            Expect("Determinism: same time gives identical state regardless of call order",
                   SameState(orbit.Propagate(1234.5), CallOtherTimesThenPropagate(orbit, 1234.5)));
        }

        private static OrbitalState CallOtherTimesThenPropagate(KeplerOrbit orbit, double t)
        {
            orbit.Propagate(0);
            orbit.Propagate(99999);
            return orbit.Propagate(t);
        }

        private static void CheckElliptical(double unused, double semiMajorAxis, double eccentricity, double inclinationDeg)
        {
            var orbit = new KeplerOrbit(semiMajorAxis, eccentricity, inclinationDeg * Math.PI / 180.0, 0.7, 1.1, 0.4);

            double energy0 = 0, h0 = 0, worstEnergy = 0, worstH = 0;
            for (int i = 0; i <= 2000; i++)
            {
                OrbitalState s = orbit.Propagate(orbit.Period * 3 * i / 2000);
                double energy = s.Speed * s.Speed / 2 - KeplerOrbit.MuEarth / s.Position.Magnitude;
                double h = Vector3d.Cross(s.Position, s.Velocity).Magnitude;
                if (i == 0) { energy0 = energy; h0 = h; }
                worstEnergy = Math.Max(worstEnergy, Math.Abs(energy - energy0) / Math.Abs(energy0));
                worstH = Math.Max(worstH, Math.Abs(h - h0) / h0);
            }
            Expect($"Elliptical: specific energy conserved (worst {worstEnergy:E1})", worstEnergy < 1e-9);
            Expect($"Elliptical: angular momentum conserved (worst {worstH:E1})", worstH < 1e-9);

            OrbitalState start = orbit.Propagate(0), afterPeriod = orbit.Propagate(orbit.Period);
            double drift = (start.Position - afterPeriod.Position).Magnitude;
            Expect($"Elliptical: returns to start after one period (error {drift:E1} m)", drift < 1e-3);

            double visViva = Math.Sqrt(KeplerOrbit.MuEarth * (2 / start.Position.Magnitude - 1 / semiMajorAxis));
            Expect("Elliptical: speed satisfies vis-viva", Math.Abs(visViva - start.Speed) < 1e-6);
        }

        private static void CheckEarthRotation()
        {
            // Orbit starts at the ascending node on the +X axis: lat 0, lon 0 at MET 0 (rotation angle at epoch is 0).
            var orbit = KeplerOrbit.Circular(185_000.0, 32.5);
            GeoPoint start = EarthRotation.SubPoint(orbit.Propagate(0).Position, 0);
            Expect("Earth rotation: start point is lat 0, lon 0",
                   Math.Abs(start.LatitudeRad) < 1e-9 && Math.Abs(start.LongitudeRad) < 1e-9);

            // After one orbit the inertial position repeats but the Earth has turned: track shifts west by omega * period.
            GeoPoint afterOrbit = EarthRotation.SubPoint(orbit.Propagate(orbit.Period).Position, orbit.Period);
            double expected = -EarthRotation.AngularVelocity * orbit.Period;
            Expect($"Earth rotation: ground track shifts west {Math.Abs(expected) * 180.0 / Math.PI:F2} deg per orbit",
                   Math.Abs(afterOrbit.LongitudeRad - expected) < 1e-9 && Math.Abs(afterOrbit.LatitudeRad) < 1e-9);

            double siderealDay = 2 * Math.PI / EarthRotation.AngularVelocity;
            Expect($"Earth rotation: one full turn per sidereal day ({siderealDay:F1} s)",
                   Math.Abs(EarthRotation.Angle(siderealDay)) < 1e-9);
        }

        private static void CheckClock()
        {
            var clock = new SimulationClock();
            clock.SetTimeScale(100);
            clock.Advance(10);
            Expect("Clock: time scale multiplies elapsed time", Math.Abs(clock.MissionElapsedTime - 1000.0) < 1e-9);

            clock.SetPaused(true);
            clock.Advance(10);
            Expect("Clock: paused clock does not advance", Math.Abs(clock.MissionElapsedTime - 1000.0) < 1e-9);

            clock.SetPaused(false);
            clock.SetTimeScale(1);
            clock.Advance(0.5);
            Expect("Clock: resumes after unpausing", Math.Abs(clock.MissionElapsedTime - 1000.5) < 1e-9);
        }

        private static void CheckViewMapping()
        {
            var v = new Vector3d(3.0, 4.0, 12.0);
            Vector3 mapped = ViewMapping.ToUnity(v, 1.0);
            Expect("ViewMapping: Unity (x,y,z) = ECI (x,z,y)", mapped == new Vector3(3f, 12f, 4f));
            Expect("ViewMapping: preserves length (scaled)", Mathf.Abs(ViewMapping.ToUnity(v, 2.0).magnitude - 6.5f) < 1e-5f);
        }

        private static bool SameState(OrbitalState a, OrbitalState b)
        {
            return a.Position.X == b.Position.X && a.Position.Y == b.Position.Y && a.Position.Z == b.Position.Z
                && a.Velocity.X == b.Velocity.X && a.Velocity.Y == b.Velocity.Y && a.Velocity.Z == b.Velocity.Z;
        }

        private static void Expect(string name, bool condition)
        {
            if (condition) { passed++; Debug.Log($"PASS  {name}"); }
            else { failed++; Debug.LogError($"FAIL  {name}"); }
        }
    }
}
