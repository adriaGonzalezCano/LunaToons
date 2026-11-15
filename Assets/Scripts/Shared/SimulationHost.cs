using Apollo.OrbitalSim;
using UnityEngine;

namespace Apollo.Shared
{
    /// <summary>
    /// Unity wrapper around the one SpacecraftSimulation. Ticks it every frame and exposes the state to the views.
    /// Neither the spacecraft nor Mission Control owns orbital data; they all read from here.
    /// </summary>
    public sealed class SimulationHost : MonoBehaviour
    {
        [SerializeField] private double altitudeKm = 185.0;
        [SerializeField] private double inclinationDegrees = 32.5;
        [Tooltip("Below this altitude the simulation restarts. Also the threshold of the low-periapsis warning.")]
        [SerializeField] private double restartAltitudeKm = 100.0;

        private SpacecraftSimulation simulation;

        public CoastOrbit Orbit => simulation?.Orbit;
        public int OrbitVersion => simulation != null ? simulation.OrbitVersion : 0;
        public SimulationClock Clock => simulation.Clock;
        public OrbitalState CurrentState => simulation.State;
        public GroundTrackHistory TrackHistory => simulation.TrackHistory;
        public double Revolutions => simulation.Revolutions;
        public double RestartAltitudeMeters => simulation.RestartAltitude;
        public bool PeriapsisIsLow => simulation.PeriapsisIsLow;
        public bool EngineOn => simulation.EngineOn;
        public double DeltaVUsed => simulation.DeltaVUsed;
        public double ThrustAcceleration => simulation.ThrustAcceleration;
        public string EngineCutoffReason => simulation.EngineCutoffReason;

        /// <summary>Why the simulation last restarted, and when (real time), for on-screen notices.</summary>
        public string LastRestartReason { get; private set; }
        public float LastRestartRealTime { get; private set; } = float.NegativeInfinity;

        private void Awake()
        {
            simulation = new SpacecraftSimulation(KeplerOrbit.Circular(altitudeKm * 1000.0, inclinationDegrees), restartAltitudeKm * 1000.0);
        }

        private void Update()
        {
            // Time-warp is not allowed during a burn (decision V2 #6): drop to x1 while the engine runs.
            if (simulation.EngineOn && simulation.Clock.TimeScale > 1.0) simulation.Clock.SetTimeScale(1.0);

            if (simulation.Advance(Time.deltaTime) == AdvanceResult.BelowSafeAltitude)
                Restart($"altitude fell below {restartAltitudeKm:F0} km");
        }

        public void SetTimeScale(double scale) => simulation.Clock.SetTimeScale(scale);
        public void SetPaused(bool paused) => simulation.Clock.SetPaused(paused);

        /// <summary>Engine on/off (thrust 1 or 0). Starting a burn drops time-warp to x1.</summary>
        public void SetThrust(bool on, bool retrograde = false)
        {
            if (on && simulation.Clock.TimeScale > 1.0) simulation.Clock.SetTimeScale(1.0);
            simulation.SetThrust(on, retrograde);
        }

        public bool ApplyProgradeVelocityChange(double deltaSpeed) => simulation.ApplyProgradeVelocityChange(deltaSpeed);

        public void Restart(string reason)
        {
            simulation.Reset();
            LastRestartReason = reason;
            LastRestartRealTime = Time.realtimeSinceStartup;
        }
    }
}
