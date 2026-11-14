using Apollo.OrbitalSim;
using UnityEngine;

namespace Apollo.Shared
{
    /// <summary>
    /// The one simulation instance. Ticks the clock and publishes the current orbital state.
    /// Both the spacecraft and Mission Control read CurrentState; neither owns orbital data.
    /// </summary>
    public sealed class SimulationHost : MonoBehaviour
    {
        [SerializeField] private double altitudeKm = 185.0;
        [SerializeField] private double inclinationDegrees = 32.5;

        public KeplerOrbit Orbit { get; private set; }
        public SimulationClock Clock { get; } = new SimulationClock();
        public OrbitalState CurrentState { get; private set; }

        private void Awake()
        {
            Orbit = KeplerOrbit.Circular(altitudeKm * 1000.0, inclinationDegrees);
            CurrentState = Orbit.Propagate(0.0);
        }

        private void Update()
        {
            Clock.Advance(Time.deltaTime);
            CurrentState = Orbit.Propagate(Clock.MissionElapsedTime);
        }

        public void SetTimeScale(double scale) => Clock.SetTimeScale(scale);
        public void SetPaused(bool paused) => Clock.SetPaused(paused);
    }
}
