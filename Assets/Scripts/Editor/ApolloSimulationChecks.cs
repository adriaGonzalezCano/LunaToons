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
            CheckCoastPropagator();
            CheckVelocityChange();
            CheckRestartLogic();
            CheckTrackHistory();
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

        // --- V2 milestone 0: coasting from a state vector (approach B, universal variables) --------------------------------------

        private static void CheckCoastPropagator()
        {
            double deg = Math.PI / 180.0;
            double r = KeplerOrbit.EarthRadius;
            var cases = new (string name, KeplerOrbit orbit)[]
            {
                ("circular equatorial", new KeplerOrbit(r + 400e3, 0.0, 0.0, 0.0, 0.0, 0.0)),
                ("circular inclined 32.5", new KeplerOrbit(r + 185e3, 0.0, 32.5 * deg, 0.7, 0.0, 0.4)),
                ("elliptical inclined 51.6", new KeplerOrbit(12.0e6, 0.3, 51.6 * deg, 0.7, 1.1, 0.4)),
                ("elliptical equatorial", new KeplerOrbit(9.0e6, 0.1, 0.0, 0.0, 2.0, 1.0)),
                ("elliptical retrograde equatorial", new KeplerOrbit(9.0e6, 0.1, Math.PI, 0.0, 2.0, 1.0)),
                ("near-circular e=1e-4", new KeplerOrbit(r + 300e3, 1e-4, 28.5 * deg, 1.2, 0.5, 2.0)),
                ("highly elliptical e=0.7", new KeplerOrbit(24.0e6, 0.7, 63.4 * deg, 0.5, 4.5, 2.5)),
                ("polar", new KeplerOrbit(r + 600e3, 0.02, 90.0 * deg, 2.0, 1.0, 3.0)),
            };

            foreach (var (name, orbit) in cases)
            {
                double worstPosition = 0, worstVelocity = 0;
                bool built = true;
                foreach (double t0 in new[] { 0.0, 1234.5, 5000.0, 40000.0 })
                {
                    OrbitalState s0 = orbit.Propagate(t0);
                    if (!CoastOrbit.TryCreate(s0.Position, s0.Velocity, t0, out CoastOrbit coast)) { built = false; break; }
                    foreach (double dt in new[] { 0.0, 1.0, 777.0, 5000.0, 20000.0, 86164.0, 1.0e6, -500.0, -20000.0 })
                    {
                        OrbitalState a = orbit.Propagate(t0 + dt), b = coast.Propagate(t0 + dt);
                        worstPosition = Math.Max(worstPosition, (a.Position - b.Position).Magnitude);
                        worstVelocity = Math.Max(worstVelocity, (a.Velocity - b.Velocity).Magnitude);
                    }
                }
                Expect($"Coast from state vector matches element propagation, {name} (position {worstPosition:E1} m, velocity {worstVelocity:E1} m/s)",
                       built && worstPosition < 1e-3 && worstVelocity < 1e-6);
            }

            // Energy and angular momentum are conserved along a coast, and the default circular orbit needs no special case.
            CoastOrbit eccentric = CoastOrbit.FromKepler(cases[2].orbit, 0.0);
            OrbitalState first = eccentric.Propagate(0.0);
            double energy0 = first.Speed * first.Speed / 2 - KeplerOrbit.MuEarth / first.Position.Magnitude;
            double h0 = Vector3d.Cross(first.Position, first.Velocity).Magnitude;
            double worstEnergy = 0, worstH = 0;
            for (int i = 0; i <= 2000; i++)
            {
                OrbitalState s = eccentric.Propagate(eccentric.Period * 3 * i / 2000);
                double energy = s.Speed * s.Speed / 2 - KeplerOrbit.MuEarth / s.Position.Magnitude;
                worstEnergy = Math.Max(worstEnergy, Math.Abs(energy - energy0) / Math.Abs(energy0));
                worstH = Math.Max(worstH, Math.Abs(Vector3d.Cross(s.Position, s.Velocity).Magnitude - h0) / h0);
            }
            Expect($"Coast: energy and angular momentum conserved (worst {worstEnergy:E1}, {worstH:E1})", worstEnergy < 1e-9 && worstH < 1e-9);

            CoastOrbit start = CoastOrbit.FromKepler(KeplerOrbit.Circular(185_000.0, 32.5), 0.0);
            Expect($"Coast: default circular orbit has eccentricity {start.Eccentricity:E1} and period {start.Period / 60.0:F2} min",
                   start.Eccentricity < 1e-9 && Math.Abs(start.Period - 5282.8636) < 1e-3);
        }

        private static void CheckVelocityChange()
        {
            // Default orbit, +100 m/s along the velocity at MET 1000 s. Python cross-check: apoapsis 532.44 km, periapsis 185.00 km.
            var sim = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            sim.Clock.Advance(1000.0);
            OrbitalState before = sim.Orbit.Propagate(1000.0);
            int versionBefore = sim.OrbitVersion;

            bool applied = sim.ApplyProgradeVelocityChange(100.0);
            OrbitalState after = sim.State;
            Expect("Velocity change: applied and orbit version incremented", applied && sim.OrbitVersion == versionBefore + 1);
            Expect($"Velocity change: position unchanged, speed up by exactly 100 m/s ({(after.Speed - before.Speed - 100.0):E1})",
                   (after.Position - before.Position).Magnitude < 1e-3 && Math.Abs(after.Speed - before.Speed - 100.0) < 1e-6);
            Expect($"Velocity change: apoapsis {sim.Orbit.ApoapsisAltitude / 1000.0:F2} km matches vis-viva (532.44 km)",
                   Math.Abs(sim.Orbit.ApoapsisAltitude - 532_440.0) < 200.0);
            Expect("Velocity change: periapsis stays at the burn altitude (185 km)", Math.Abs(sim.Orbit.PeriapsisAltitude - 185_000.0) < 10.0);

            // An escape velocity change is refused and changes nothing.
            int version = sim.OrbitVersion;
            bool refused = !sim.ApplyProgradeVelocityChange(4000.0);
            Expect("Velocity change: escape trajectory refused, orbit untouched", refused && sim.OrbitVersion == version);
        }

        private static void CheckRestartLogic()
        {
            // 30 m/s retrograde at MET 0 gives a periapsis of about 85 km, below the 100 km limit.
            var sim = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            Expect("Restart: no low-periapsis warning on the default orbit", !sim.PeriapsisIsLow);
            sim.ApplyProgradeVelocityChange(-30.0);
            Expect($"Restart: low-periapsis warning raised (periapsis {sim.Orbit.PeriapsisAltitude / 1000.0:F1} km)", sim.PeriapsisIsLow);

            double period = sim.Orbit.Period;
            AdvanceResult result = AdvanceResult.Nominal;
            for (int i = 0; i < 100 && result == AdvanceResult.Nominal; i++) result = sim.Advance(60.0);
            Expect($"Restart: detected within one period (MET {sim.Clock.MissionElapsedTime:F0} s of {period:F0} s)",
                   result == AdvanceResult.BelowSafeAltitude && sim.Clock.MissionElapsedTime < period);
            Expect("Restart: altitude is below the limit at detection", sim.State.Altitude < 100_000.0);

            sim.Reset();
            Expect("Restart: reset returns to MET 0 and the initial orbit",
                   sim.Clock.MissionElapsedTime == 0.0 && Math.Abs(sim.Orbit.PeriapsisAltitude - 185_000.0) < 1e-6 && !sim.PeriapsisIsLow);

            // Detection must also work at x1000 time-warp (about 16 s of mission time per frame).
            sim.ApplyProgradeVelocityChange(-30.0);
            sim.Clock.SetTimeScale(1000.0);
            result = AdvanceResult.Nominal;
            for (int i = 0; i < 1000 && result == AdvanceResult.Nominal; i++) result = sim.Advance(1.0 / 60.0);
            Expect("Restart: detected under x1000 time-warp", result == AdvanceResult.BelowSafeAltitude);

            // No false restarts over three orbits of a healthy, slightly raised orbit.
            var healthy = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            healthy.ApplyProgradeVelocityChange(10.0);
            bool nominal = true;
            for (int i = 0; i < 270 && nominal; i++) nominal = healthy.Advance(60.0) == AdvanceResult.Nominal;
            Expect("Restart: no false restart over three orbits", nominal);
        }

        private static void CheckTrackHistory()
        {
            var sim = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            for (int i = 0; i < 50; i++) sim.Advance(60.0); // 3000 s
            int count = sim.TrackHistory.Samples.Count;
            Expect($"Track history: one sample per 10 s ({count} samples over 3000 s)", count >= 295 && count <= 305);

            for (int i = 0; i < 200; i++) sim.Advance(60.0); // 15000 s total
            Expect($"Track history: old samples pruned ({sim.TrackHistory.Samples.Count} kept)", sim.TrackHistory.Samples.Count <= 722);
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
