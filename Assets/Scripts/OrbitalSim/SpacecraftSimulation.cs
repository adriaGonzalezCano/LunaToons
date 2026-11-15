using System;

namespace Apollo.OrbitalSim
{
    public enum AdvanceResult
    {
        Nominal,
        /// <summary>The spacecraft dropped below the safe altitude; the owner should restart the simulation.</summary>
        BelowSafeAltitude
    }

    /// <summary>
    /// The spacecraft simulation (pure C#, no Unity). Single source of truth for the spacecraft state.
    /// Coasting is analytic (KeplerOrbit). A velocity change recomputes the orbit from the current state vector
    /// (approach A), so the position and velocity are continuous across the change.
    /// </summary>
    public sealed class SpacecraftSimulation
    {
        /// <summary>Longest span of mission time evaluated in one go, so a fast time-warp cannot skip past a low point.</summary>
        public const double MaxChunkSeconds = 1.0;

        private readonly KeplerOrbit initialOrbit;

        public SpacecraftSimulation(KeplerOrbit orbit, double restartAltitudeMeters)
        {
            initialOrbit = orbit ?? throw new ArgumentNullException(nameof(orbit));
            RestartAltitude = restartAltitudeMeters;
            TrackHistory = new GroundTrackHistory();
            Reset();
        }

        public SimulationClock Clock { get; } = new SimulationClock();
        public GroundTrackHistory TrackHistory { get; }

        /// <summary>Current coasting orbit. Replaced when a velocity change is applied.</summary>
        public KeplerOrbit Orbit { get; private set; }

        /// <summary>Incremented every time the orbit changes (start, velocity change, restart); views rebuild when it moves.</summary>
        public int OrbitVersion { get; private set; }

        public OrbitalState State { get; private set; }

        /// <summary>Revolutions completed so far (accumulated, so it stays meaningful when the period changes).</summary>
        public double Revolutions { get; private set; }

        /// <summary>Altitude (m) below which the simulation must be restarted. Also the threshold for the low-periapsis warning.</summary>
        public double RestartAltitude { get; }

        public bool PeriapsisIsLow => Orbit.PeriapsisAltitude < RestartAltitude;

        public AdvanceResult Advance(double realDeltaSeconds)
        {
            double before = Clock.MissionElapsedTime;
            Clock.Advance(realDeltaSeconds);
            double target = Clock.MissionElapsedTime;
            if (target <= before) return AdvanceResult.Nominal;

            double t = before;
            while (t < target)
            {
                double next = Math.Min(t + MaxChunkSeconds, target);
                Revolutions += (next - t) / Orbit.Period;
                t = next;

                State = Orbit.Propagate(t);
                TrackHistory.Record(t, EarthRotation.SubPoint(State.Position, t));
                if (State.Altitude < RestartAltitude) return AdvanceResult.BelowSafeAltitude;
            }
            return AdvanceResult.Nominal;
        }

        /// <summary>
        /// Instantaneous velocity change (m/s, inertial frame). Recomputes the orbit from the current state.
        /// Returns false, changing nothing, if the result would not be a bound elliptical orbit.
        /// </summary>
        public bool ApplyVelocityChange(Vector3d deltaVelocity)
        {
            OrbitalState now = Orbit.Propagate(Clock.MissionElapsedTime);
            if (!KeplerOrbit.TryFromState(now.Position, now.Velocity + deltaVelocity, Clock.MissionElapsedTime, out KeplerOrbit changed))
                return false;

            Orbit = changed;
            OrbitVersion++;
            State = Orbit.Propagate(Clock.MissionElapsedTime);
            return true;
        }

        /// <summary>Velocity change along (positive) or against (negative) the current velocity.</summary>
        public bool ApplyProgradeVelocityChange(double deltaSpeed)
        {
            Vector3d velocity = Orbit.Propagate(Clock.MissionElapsedTime).Velocity;
            return ApplyVelocityChange(velocity * (deltaSpeed / velocity.Magnitude));
        }

        /// <summary>Back to the initial orbit at mission time 0. Time scale and pause are kept.</summary>
        public void Reset()
        {
            Orbit = initialOrbit;
            OrbitVersion++;
            Clock.ResetTime();
            Revolutions = 0.0;
            TrackHistory.Clear();
            State = Orbit.Propagate(0.0);
        }
    }
}
