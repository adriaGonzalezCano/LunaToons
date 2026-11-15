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
        private Vector3d thrustAcceleration;   // inertial frame, constant during one integration step
        private bool engineInhibited;

        /// <summary>Largest angular acceleration the attitude controls can produce (rad/s^2).</summary>
        public const double MaxAngularAcceleration = 2.0 * Math.PI / 180.0;

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

        /// <summary>Attitude and angular rate of the spacecraft (body-to-inertial quaternion, body rates).</summary>
        public RigidBodyAttitude Attitude { get; private set; }

        /// <summary>Current pilot commands.</summary>
        public ControlInput Controls { get; private set; }

        /// <summary>True while anything needs fine time steps and no time-warp: engine on, attitude commanded, or rotation being stopped.</summary>
        public bool ControlsActive =>
            EngineOn || Controls.AnyAttitudeAxis || Controls.AnyTranslationAxis ||
            (Controls.StopRotation && Attitude.Rate.Magnitude > 0.0);

        public void SetControls(ControlInput controls)
        {
            // A new translation command re-arms the engine after an automatic cutoff.
            if (!controls.AnyTranslationAxis) engineInhibited = false;
            else if (!Controls.AnyTranslationAxis)
            {
                engineInhibited = false;
                EngineCutoffReason = "";
            }
            Controls = controls;
        }

        public bool PeriapsisIsLow => Orbit.PeriapsisAltitude < RestartAltitude;

        /// <summary>True while a translation thruster is firing (thrust 1 on each commanded axis, 0 otherwise).</summary>
        public bool EngineOn => Controls.AnyTranslationAxis && !engineInhibited;

        /// <summary>
        /// Thrust acceleration of each translation axis at thrust 1, m/s^2 (placeholder, the same for every axis).
        /// Thrust acts along the body axes, so it follows the attitude of the spacecraft.
        /// </summary>
        public double ThrustAcceleration { get; set; } = DefaultThrustAcceleration;

        /// <summary>Total delta-v spent by the engines since the last restart (m/s).</summary>
        public double DeltaVUsed { get; private set; }

        /// <summary>Why the engines last stopped by themselves (empty if they never did). Cleared by the next translation command.</summary>
        public string EngineCutoffReason { get; private set; } = "";

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
                double step = Math.Min(t + (ControlsActive ? PoweredStepSeconds : MaxChunkSeconds), target) - t;
                if (EngineOn)
                {
                    // Thrust along the body axes, using the attitude at the middle of the step (the ship may be rotating).
                    Vector3d bodyDirection = new Vector3d(Controls.TranslateX, -Controls.TranslateY, Controls.TranslateZ); // body +Y is left
                    Vector3d bodyThrust = bodyDirection * ThrustAcceleration;
                    thrustAcceleration = Attitude.AttitudeAfter(0.5 * step).Rotate(bodyThrust);

                    OrbitalState now = State;
                    y[0] = now.Position.X; y[1] = now.Position.Y; y[2] = now.Position.Z;
                    y[3] = now.Velocity.X; y[4] = now.Velocity.Y; y[5] = now.Velocity.Z;
                    integrator.Step(derivatives, t, y, step);

                    var position = new Vector3d(y[0], y[1], y[2]);
                    var velocity = new Vector3d(y[3], y[4], y[5]);
                    if (!CoastOrbit.TryCreate(position, velocity, t + step, out CoastOrbit poweredOrbit))
                    {
                        // The burn would make the orbit unbound: cut the engine and redo this span as a coast.
                        engineInhibited = true;
                        EngineCutoffReason = "escape trajectory";
                        continue;
                    }

                    Revolutions += step / Orbit.Period;
                    t += step;
                    Orbit = poweredOrbit;
                    orbitChanged = true;
                    DeltaVUsed += bodyThrust.Magnitude * step;
                    State = new OrbitalState(t, position, velocity, position.Magnitude - KeplerOrbit.EarthRadius, velocity.Magnitude, Orbit.Period);
                }
                else
                {
                    Revolutions += step / Orbit.Period;
                    t += step;
                    State = Orbit.Propagate(t);
                }

                AdvanceAttitude(step);

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

        private void AdvanceAttitude(double step)
        {
            double a = MaxAngularAcceleration;
            Vector3d inertia = Attitude.Inertia;
            Vector3d torque;
            if (Controls.StopRotation)
                torque = Attitude.StopRotationTorque(a);
            else
                torque = new Vector3d(Controls.Roll * a * inertia.X, -Controls.Pitch * a * inertia.Y, -Controls.Yaw * a * inertia.Z);

            Attitude.SetTorque(torque);
            Attitude.Step(step);
            if (Controls.StopRotation) Attitude.SnapToRest();
        }

        /// <summary>
        /// Starting attitude: +X (forward, the way the couches face) along the velocity, +Y (left side of the cabin) away from
        /// Earth, so the window on the right-hand wall looks down at Earth; +Z (up) = X x Y completes the right-handed set.
        /// </summary>
        private static Quaterniond InitialAttitude(OrbitalState state)
        {
            Vector3d x = state.Velocity * (1.0 / state.Velocity.Magnitude);
            Vector3d radial = state.Position * (1.0 / state.Position.Magnitude);
            Vector3d y = radial - x * Vector3d.Dot(radial, x);
            y = y * (1.0 / y.Magnitude);
            Vector3d z = Vector3d.Cross(x, y);
            return Quaterniond.FromBasis(x, y, z);
        }

        // y = [x y z vx vy vz]; gravity plus the thrust acceleration of the current step.
        private void Derivatives(double t, double[] state, double[] derivative)
        {
            double r2 = state[0] * state[0] + state[1] * state[1] + state[2] * state[2];
            double r = Math.Sqrt(r2);
            double k = -KeplerOrbit.MuEarth / (r2 * r);

            derivative[0] = state[3];
            derivative[1] = state[4];
            derivative[2] = state[5];
            derivative[3] = k * state[0] + thrustAcceleration.X;
            derivative[4] = k * state[1] + thrustAcceleration.Y;
            derivative[5] = k * state[2] + thrustAcceleration.Z;
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
            engineInhibited = false;
            EngineCutoffReason = "";
            DeltaVUsed = 0.0;
            TrackHistory.Clear();
            State = Orbit.Propagate(0.0);
            Controls = ControlInput.None;
            Attitude = new RigidBodyAttitude(new Vector3d(1, 1, 1), InitialAttitude(State), new Vector3d(0, 0, 0));
        }
    }
}
