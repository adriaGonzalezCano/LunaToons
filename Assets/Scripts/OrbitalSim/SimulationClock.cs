using System;

namespace Apollo.OrbitalSim
{
    /// <summary>Mission elapsed time with pause and time-warp. Pure C#, driven by whoever calls Advance.</summary>
    public sealed class SimulationClock
    {
        public double MissionElapsedTime { get; private set; } // s
        public double TimeScale { get; private set; } = 1.0;
        public bool IsPaused { get; private set; }

        public void Advance(double realDeltaSeconds)
        {
            if (IsPaused || realDeltaSeconds <= 0) return;
            MissionElapsedTime += realDeltaSeconds * TimeScale;
        }

        public void SetTimeScale(double scale)
        {
            if (scale < 0) throw new ArgumentOutOfRangeException(nameof(scale));
            TimeScale = scale;
        }

        public void SetPaused(bool paused) => IsPaused = paused;

        /// <summary>Sets mission elapsed time back to 0. Time scale and pause state are kept.</summary>
        public void ResetTime() => MissionElapsedTime = 0.0;
    }
}
