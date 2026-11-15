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
    /// Coasting is analytic (CoastOrbit, exact two-body motion from a state vector). A velocity change starts a new
    /// CoastOrbit from the new state, so position and velocity are continuous across the change.
    /// While the engine is on (a continuous burn) the state is integrated with fixed-step RK8 under gravity plus thrust; after
    /// every step the osculating Kepler orbit of the new state is solved and published. When the engine stops, coasting
    /// continues analytically from the last state.
    /// </summary>
    public sealed class SpacecraftSimulation
    {
        /// <summary>Longest span of mission time evaluated in one go, so a fast time-warp cannot skip past a low point.</summary>
        public const double MaxChunkSeconds = 1.0;

        /// <summary>Integration step during a burn (s). Fixed, so the result does not depend on the frame rate.</summary>
        public const double PoweredStepSeconds = 0.05;

        /// <summary>Default thrust acceleration at full throttle (m/s^2). Placeholder until the propellant model exists.</summary>
        public const double DefaultThrustAcceleration = 3.0;

        private readonly CoastOrbit initialOrbit;
        private readonly Rk8Integrator integrator = new Rk8Integrator(6);
        private readonly double[] y = new double[6];
        private readonly OdeFunction derivatives;
        private double thrustSign = 1.0;

        public SpacecraftSimulation(KeplerOrbit startOrbit, double restartAltitudeMeters)
        {
            if (startOrbit == null) throw new ArgumentNullException(nameof(startOrbit));
            initialOrbit = CoastOrbit.FromKepler(startOrbit, 0.0);
            derivatives = Derivatives;
            RestartAltitude = restartAltitudeMeters;
            TrackHistory = new GroundTrackHistory();
            Reset();
        }

        public SimulationClock Clock { get; } = new SimulationClock();
        public GroundTrackHistory TrackHistory { get; }

        /// <summary>Current coasting orbit. Replaced when a velocity change is applied.</summary>
        public CoastOrbit Orbit { get; private set; }

        /// <summary>Incremented every time the orbit changes (start, velocity change, restart); views rebuild when it moves.</summary>
        public int OrbitVersion { get; private set; }

        public OrbitalState State { get; private set; }

        /// <summary>Revolutions completed so far (accumulated, so it stays meaningful when the period changes).</summary>
        public double Revolutions { get; private set; }

        /// <summary>Altitude (m) below which the simulation must be restarted. Also the threshold for the low-periapsis warning.</summary>
        public double RestartAltitude { get; }

        public bool PeriapsisIsLow => Orbit.PeriapsisAltitude < RestartAltitude;

        /// <summary>True while a burn is in progress (thrust 1). Thrust is binary for now: 1 or 0.</summary>
        public bool EngineOn { get; private set; }

        /// <summary>Thrust acceleration at full throttle, m/s^2. Direction is along (or against) the velocity until attitude exists.</summary>
        public double ThrustAcceleration { get; set; } = DefaultThrustAcceleration;

        /// <summary>Total delta-v spent by the engine since the last restart (m/s).</summary>
        public double DeltaVUsed { get; private set; }

        /// <summary>Why the engine last stopped by itself (empty if it never did).</summary>
        public string EngineCutoffReason { get; private set; } = "";

        /// <summary>Starts (on = true) or stops (on = false) a burn. Retrograde thrusts against the velocity.</summary>
        public void SetThrust(bool on, bool retrograde = false)
        {
            if (on)
            {
                EngineCutoffReason = "";
                thrustSign = retrograde ? -1.0 : 1.0;
            }
            EngineOn = on;
        }

        public AdvanceResult Advance(double realDeltaSeconds)
        {
            double before = Clock.MissionElapsedTime;
            Clock.Advance(realDeltaSeconds);
            double target = Clock.MissionElapsedTime;
            if (target <= before) return AdvanceResult.Nominal;

            double t = before;
            bool orbitChanged = false;
            AdvanceResult result = AdvanceResult.Nominal;
            while (t < target)
            {
                if (EngineOn)
                {
                    double step = Math.Min(t + PoweredStepSeconds, target) - t;
                    OrbitalState now = State;
                    y[0] = now.Position.X; y[1] = now.Position.Y; y[2] = now.Position.Z;
                    y[3] = now.Velocity.X; y[4] = now.Velocity.Y; y[5] = now.Velocity.Z;
                    integrator.Step(derivatives, t, y, step);

                    var position = new Vector3d(y[0], y[1], y[2]);
                    var velocity = new Vector3d(y[3], y[4], y[5]);
                    if (!CoastOrbit.TryCreate(position, velocity, t + step, out CoastOrbit poweredOrbit))
                    {
                        // The burn would make the orbit unbound: cut the engine and redo this span as a coast.
                        EngineOn = false;
                        EngineCutoffReason = "escape trajectory";
                        continue;
                    }

                    Revolutions += step / Orbit.Period;
                    t += step;
                    Orbit = poweredOrbit;
                    orbitChanged = true;
                    DeltaVUsed += ThrustAcceleration * step;
                    State = new OrbitalState(t, position, velocity, position.Magnitude - KeplerOrbit.EarthRadius, velocity.Magnitude, Orbit.Period);
                }
                else
                {
                    double next = Math.Min(t + MaxChunkSeconds, target);
                    Revolutions += (next - t) / Orbit.Period;
                    t = next;
                    State = Orbit.Propagate(t);
                }

                TrackHistory.Record(t, EarthRotation.SubPoint(State.Position, t));
                if (State.Altitude < RestartAltitude)
                {
                    result = AdvanceResult.BelowSafeAltitude;
                    break;
                }
            }

            if (orbitChanged) OrbitVersion++; // once per frame, not per integration step
            return result;
        }

        // y = [x y z vx vy vz]; gravity plus thrust along the velocity direction.
        private void Derivatives(double t, double[] state, double[] derivative)
        {
            double r2 = state[0] * state[0] + state[1] * state[1] + state[2] * state[2];
            double r = Math.Sqrt(r2);
            double k = -KeplerOrbit.MuEarth / (r2 * r);
            double speed = Math.Sqrt(state[3] * state[3] + state[4] * state[4] + state[5] * state[5]);
            double thrust = speed > 0 ? thrustSign * ThrustAcceleration / speed : 0.0;

            derivative[0] = state[3];
            derivative[1] = state[4];
            derivative[2] = state[5];
            derivative[3] = k * state[0] + thrust * state[3];
            derivative[4] = k * state[1] + thrust * state[4];
            derivative[5] = k * state[2] + thrust * state[5];
        }

        /// <summary>
        /// Instantaneous velocity change (m/s, inertial frame). Starts a new coasting orbit from the changed state.
        /// Returns false, changing nothing, if the result would not be a bound elliptical orbit.
        /// </summary>
        public bool ApplyVelocityChange(Vector3d deltaVelocity)
        {
            OrbitalState now = Orbit.Propagate(Clock.MissionElapsedTime);
            if (!CoastOrbit.TryCreate(now.Position, now.Velocity + deltaVelocity, Clock.MissionElapsedTime, out CoastOrbit changed))
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
            EngineOn = false;
            EngineCutoffReason = "";
            DeltaVUsed = 0.0;
            TrackHistory.Clear();
            State = Orbit.Propagate(0.0);
        }
    }
}
