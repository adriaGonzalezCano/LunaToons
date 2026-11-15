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
            CheckPoweredFlight();
            CheckHighEccentricity();
            CheckAttitude();
            CheckAttitudeInSimulation();
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

        private static void CheckPoweredFlight()
        {
            // 20 s prograde burn at 3 m/s^2 (default orbit). Python port (tools/validate_burn.py): delta-v 60 m/s, apoapsis 390.74 km
            // (impulsive 60 m/s: 390.75 km), frame-rate differences about 1e-8 m.
            SpacecraftSimulation Burn(double frameSeconds)
            {
                var sim = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
                sim.SetThrust(true);
                int frames = (int)Math.Round(20.0 / frameSeconds);
                for (int i = 0; i < frames; i++) sim.Advance(frameSeconds);
                sim.SetThrust(false);
                return sim;
            }

            SpacecraftSimulation a = Burn(1.0 / 60.0);
            SpacecraftSimulation b = Burn(1.0 / 144.0);
            SpacecraftSimulation c = Burn(1.0 / 30.0);
            Expect($"Burn: delta-v spent {a.DeltaVUsed:F6} m/s equals thrust x time (60 m/s)", Math.Abs(a.DeltaVUsed - 60.0) < 1e-6);
            Expect($"Burn: apoapsis {a.Orbit.ApoapsisAltitude / 1000.0:F2} km matches the impulsive prediction (390.75 km), periapsis {a.Orbit.PeriapsisAltitude / 1000.0:F2} km stays 185 km",
                   Math.Abs(a.Orbit.ApoapsisAltitude - 390_750.0) < 1_000.0 && Math.Abs(a.Orbit.PeriapsisAltitude - 185_000.0) < 500.0);
            Expect($"Burn: result independent of frame rate (60 vs 144 fps: {(a.State.Position - b.State.Position).Magnitude:E1} m; 60 vs 30 fps: {(a.State.Position - c.State.Position).Magnitude:E1} m)",
                   (a.State.Position - b.State.Position).Magnitude < 1e-3 && (a.State.Position - c.State.Position).Magnitude < 1e-3
                   && (a.State.Velocity - b.State.Velocity).Magnitude < 1e-6);
            Expect("Burn: engine reported off after release", !a.EngineOn);

            // After release the coast continues from the burn-end state without a jump.
            OrbitalState end = a.State;
            double t = a.Clock.MissionElapsedTime;
            a.Advance(1.0);
            Expect("Burn: coast after release is continuous with the burn-end state",
                   (a.State.Position - a.Orbit.Propagate(t + 1.0).Position).Magnitude < 1e-6 && (a.State.Position - end.Position).Magnitude < 8_100.0);

            // Retrograde: 10 s lowers the periapsis (about 85 km), apoapsis unchanged.
            var retro = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            retro.SetThrust(true, true);
            for (int i = 0; i < 600; i++) retro.Advance(1.0 / 60.0);
            retro.SetThrust(false);
            Expect($"Burn: 10 s retrograde gives periapsis {retro.Orbit.PeriapsisAltitude / 1000.0:F2} km (85.07) and apoapsis unchanged",
                   Math.Abs(retro.Orbit.PeriapsisAltitude - 85_070.0) < 500.0 && Math.Abs(retro.Orbit.ApoapsisAltitude - 185_000.0) < 5_000.0);

            // Holding the burn: the engine must cut itself off before the orbit becomes unbound (Python: after about 1114 s).
            var hold = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            hold.SetThrust(true);
            for (int i = 0; i < 100_000 && hold.EngineOn; i++) hold.Advance(0.1);
            Expect($"Burn: escape cutoff after {hold.Clock.MissionElapsedTime:F0} s, orbit still bound (e = {hold.Orbit.Eccentricity:F5})",
                   !hold.EngineOn && hold.EngineCutoffReason == "escape trajectory" && hold.Orbit.Eccentricity < 1.0
                   && hold.Clock.MissionElapsedTime > 900.0 && hold.Clock.MissionElapsedTime < 1300.0);

            // Restart clears the engine state.
            hold.Reset();
            Expect("Burn: restart clears engine state and delta-v", !hold.EngineOn && hold.DeltaVUsed == 0.0 && hold.EngineCutoffReason == "");
        }

        private static void CheckAttitude()
        {
            const double step = 0.05;
            double Angle(Quaterniond q) => 2 * Math.Acos(Math.Min(1.0, Math.Abs(q.W)));

            // Torque-free, equal inertia: constant rate, exact rotation about the rate axis (Python: drift 0).
            var w0 = new Vector3d(0.1, -0.05, 0.2);
            var free = new RigidBodyAttitude(new Vector3d(1, 1, 1), Quaterniond.Identity, w0);
            for (int i = 0; i < 2400; i++) free.Step(step);
            Quaterniond expected = Quaterniond.FromAxisAngle(w0, w0.Magnitude * 120.0);
            double dot = Math.Abs(free.Attitude.W * expected.W + free.Attitude.X * expected.X + free.Attitude.Y * expected.Y + free.Attitude.Z * expected.Z);
            Expect($"Attitude: torque-free rotation persists unchanged for 120 s (rate drift {(free.Rate - w0).Magnitude:E1}, quaternion error {1 - dot:E1})",
                   (free.Rate - w0).Magnitude < 1e-10 && 1 - dot < 1e-10);

            // Unequal inertia (tumbling): angular momentum and energy conserved, quaternion stays unit length.
            var tumble = new RigidBodyAttitude(new Vector3d(1, 2, 3), new Quaterniond(0.8, 0.2, -0.4, 0.4), new Vector3d(0.3, 0.02, -0.15));
            Vector3d l0 = tumble.AngularMomentumInertial;
            double e0 = tumble.RotationalEnergy;
            for (int i = 0; i < 6000; i++) tumble.Step(step);
            double dl = (tumble.AngularMomentumInertial - l0).Magnitude / l0.Magnitude;
            double de = Math.Abs(tumble.RotationalEnergy - e0) / e0;
            Expect($"Attitude: tumbling body conserves angular momentum ({dl:E1}) and energy ({de:E1}), |q| = 1",
                   dl < 1e-9 && de < 1e-9 && Math.Abs(tumble.Attitude.Norm - 1.0) < 1e-12);

            // Torque pulse: 2 deg/s^2 about X for 5 s gives 10 deg/s and 25 deg; the rate persists after release.
            double alpha = 2.0 * Math.PI / 180.0;
            var pulse = new RigidBodyAttitude(new Vector3d(1, 1, 1), Quaterniond.Identity, new Vector3d(0, 0, 0));
            pulse.SetTorque(new Vector3d(alpha, 0, 0));
            for (int i = 0; i < 100; i++) pulse.Step(step);
            pulse.SetTorque(new Vector3d(0, 0, 0));
            double angleDeg = Angle(pulse.Attitude) * 180.0 / Math.PI;
            Expect($"Attitude: torque pulse gives {pulse.Rate.X * 180.0 / Math.PI:F6} deg/s and {angleDeg:F6} deg (10 and 25)",
                   Math.Abs(pulse.Rate.X * 180.0 / Math.PI - 10.0) < 1e-6 && Math.Abs(angleDeg - 25.0) < 1e-4);
            for (int i = 0; i < 200; i++) pulse.Step(step);
            Expect("Attitude: rate persists after the torque is released", Math.Abs(pulse.Rate.X * 180.0 / Math.PI - 10.0) < 1e-6);

            // Stop-rotation law: smooth, limited to 2 deg/s^2, no overshoot, at rest within 20 s (Python: 9.8 s).
            var stop = new RigidBodyAttitude(new Vector3d(1, 1, 1), Quaterniond.Identity,
                                             new Vector3d(10.0 * Math.PI / 180.0, -6.0 * Math.PI / 180.0, 3.0 * Math.PI / 180.0));
            double peak = 0, time = 0;
            bool overshoot = false;
            double[] sign = { 1, -1, 1 };
            while (time < 120.0 && !stop.SnapToRest())
            {
                Vector3d tq = stop.StopRotationTorque(alpha);
                peak = Math.Max(peak, Math.Max(Math.Abs(tq.X), Math.Max(Math.Abs(tq.Y), Math.Abs(tq.Z))));
                stop.SetTorque(tq);
                stop.Step(step);
                time += step;
                if (stop.Rate.X * sign[0] < -1e-9 || stop.Rate.Y * sign[1] < -1e-9 || stop.Rate.Z * sign[2] < -1e-9) overshoot = true;
            }
            Expect($"Attitude: stop-rotation reaches rest in {time:F1} s, peak {peak * 180.0 / Math.PI:F3} deg/s^2, overshoot: {overshoot}",
                   time < 20.0 && !overshoot && peak <= alpha * 1.000001 && stop.Rate.Magnitude == 0.0);
        }

        private static void CheckAttitudeInSimulation()
        {
            var sim = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            Vector3d x = sim.Attitude.Attitude.Rotate(new Vector3d(1, 0, 0));
            Vector3d y = sim.Attitude.Attitude.Rotate(new Vector3d(0, 1, 0));
            Vector3d z = sim.Attitude.Attitude.Rotate(new Vector3d(0, 0, 1));
            Vector3d vHat = sim.State.Velocity * (1.0 / sim.State.Speed);
            Vector3d rHat = sim.State.Position * (1.0 / sim.State.Position.Magnitude);
            Expect("Attitude: starts with +X along the velocity, +Z radial, right-handed (X x Y = Z)",
                   (x - vHat).Magnitude < 1e-9 && (z - rHat).Magnitude < 1e-9 && (Vector3d.Cross(x, y) - z).Magnitude < 1e-9);
            Expect("Attitude: no controls, no rotation, no warp lockout", !sim.ControlsActive && sim.Attitude.Rate.Magnitude == 0.0);

            // Sign conventions: pitch +1 = nose up (rate about -Y), yaw +1 = nose right (+Z), roll +1 = roll right (-X).
            double alpha = SpacecraftSimulation.MaxAngularAcceleration;
            ControlInput Cmd(double pitch, double yaw, double roll) => new ControlInput { Pitch = pitch, Yaw = yaw, Roll = roll };
            sim.SetControls(Cmd(1, 0, 0)); sim.Advance(1.0);
            Expect("Attitude: controls active flag raised while commanding", sim.ControlsActive);
            Expect($"Attitude: pitch +1 gives nose-up rate ({sim.Attitude.Rate.Y * 180.0 / Math.PI:F3} deg/s about Y)", sim.Attitude.Rate.Y < 0 && Math.Abs(sim.Attitude.Rate.Y + alpha) < 1e-9);
            sim.Reset();
            sim.SetControls(Cmd(0, 1, 0)); sim.Advance(1.0);
            Expect("Attitude: yaw +1 gives rate about +Z", sim.Attitude.Rate.Z > 0 && Math.Abs(sim.Attitude.Rate.Z - alpha) < 1e-9);
            sim.Reset();
            sim.SetControls(Cmd(0, 0, 1)); sim.Advance(1.0);
            Expect("Attitude: roll +1 gives rate about -X", sim.Attitude.Rate.X < 0 && Math.Abs(sim.Attitude.Rate.X + alpha) < 1e-9);

            // Frame-rate independence of a 5 s command followed by 100 s of coasting.
            RigidBodyAttitude Run(double frame)
            {
                var s = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
                s.SetControls(Cmd(0.5, 1, -1));
                for (int i = 0; i < (int)Math.Round(5.0 / frame); i++) s.Advance(frame);
                s.SetControls(ControlInput.None);
                for (int i = 0; i < (int)Math.Round(100.0 / frame); i++) s.Advance(frame);
                return s.Attitude;
            }
            RigidBodyAttitude a = Run(1.0 / 30.0), b = Run(1.0 / 144.0);
            double qd = 1 - Math.Abs(a.Attitude.W * b.Attitude.W + a.Attitude.X * b.Attitude.X + a.Attitude.Y * b.Attitude.Y + a.Attitude.Z * b.Attitude.Z);
            Expect($"Attitude: identical at 30 and 144 fps (quaternion {qd:E1}, rate {(a.Rate - b.Rate).Magnitude:E1}), rate persists after release",
                   qd < 1e-9 && (a.Rate - b.Rate).Magnitude < 1e-9 && a.Rate.Magnitude > 0.05);

            // Angular momentum conserved while coasting in orbit after release; stop-rotation brings it to rest.
            var spin = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            spin.SetControls(Cmd(0.5, 1, -1));
            for (int i = 0; i < 300; i++) spin.Advance(1.0 / 60.0);
            spin.SetControls(ControlInput.None);
            Vector3d lSpin = spin.Attitude.AngularMomentumInertial;
            spin.Advance(600.0);
            Expect("Attitude: angular momentum conserved over 600 s of coasting", (spin.Attitude.AngularMomentumInertial - lSpin).Magnitude / lSpin.Magnitude < 1e-9);

            spin.SetControls(new ControlInput { StopRotation = true });
            double tStop = 0;
            while (spin.ControlsActive && tStop < 60) { spin.Advance(1.0 / 60.0); tStop += 1.0 / 60.0; }
            Expect($"Attitude: stop-rotation brings the vehicle to rest in {tStop:F1} s and releases the warp lockout",
                   spin.Attitude.Rate.Magnitude == 0.0 && !spin.ControlsActive && tStop < 30);

            // Warp: a coasting, torque-free rotation survives x1000 time-warp.
            var warp = new SpacecraftSimulation(KeplerOrbit.Circular(185_000.0, 32.5), 100_000.0);
            warp.SetControls(Cmd(0, 1, 0)); warp.Advance(2.0); warp.SetControls(ControlInput.None);
            Vector3d lw = warp.Attitude.AngularMomentumInertial;
            warp.Clock.SetTimeScale(1000.0);
            for (int i = 0; i < 300; i++) warp.Advance(1.0 / 60.0);
            Expect("Attitude: rotation conserved under x1000 time-warp", (warp.Attitude.AngularMomentumInertial - lw).Magnitude / lw.Magnitude < 1e-9);

            sim.SetControls(Cmd(1, 1, 1));
            sim.Reset();
            Expect("Attitude: restart clears controls and rotation", !sim.Controls.AnyAttitudeAxis && sim.Attitude.Rate.Magnitude == 0.0);
        }

        private static void CheckHighEccentricity()
        {
            // Periapsis 185 km, e = 0.97 and 0.995: the solver must converge and conserve energy, angular momentum, and close after one period.
            foreach (double e in new[] { 0.9, 0.97, 0.995 })
            {
                double rp = KeplerOrbit.EarthRadius + 185_000.0;
                double sma = rp / (1 - e);
                double speed = Math.Sqrt(KeplerOrbit.MuEarth * (2.0 / rp - 1.0 / sma));
                CoastOrbit.TryCreate(new Vector3d(rp, 0, 0), new Vector3d(0, speed, 0), 0.0, out CoastOrbit orbit);
                double energy0 = speed * speed / 2 - KeplerOrbit.MuEarth / rp;
                double h0 = rp * speed;
                bool conserved = true;
                for (int k = -40; k <= 40; k++)
                {
                    OrbitalState s = orbit.Propagate(orbit.Period * k / 37.0);
                    double energy = s.Speed * s.Speed / 2 - KeplerOrbit.MuEarth / s.Position.Magnitude;
                    double h = Vector3d.Cross(s.Position, s.Velocity).Magnitude;
                    if (Math.Abs(energy - energy0) / Math.Abs(energy0) > 1e-9 || Math.Abs(h - h0) / h0 > 1e-9) conserved = false;
                }
                OrbitalState back = orbit.Propagate(orbit.Period);
                Expect($"Coast e = {e}: energy and angular momentum conserved, back at periapsis after one period ({(back.Position - new Vector3d(rp, 0, 0)).Magnitude:E1} m)",
                       conserved && (back.Position - new Vector3d(rp, 0, 0)).Magnitude < 1e-2);
            }
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
