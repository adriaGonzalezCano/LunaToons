using System.Collections.Generic;

namespace Apollo.OrbitalSim
{
    public readonly struct TrackSample
    {
        public readonly double MissionElapsedTime;
        public readonly GeoPoint Point;

        public TrackSample(double missionElapsedTime, GeoPoint point)
        {
            MissionElapsedTime = missionElapsedTime;
            Point = point;
        }
    }

    /// <summary>
    /// Recorded sub-spacecraft points. The past ground track must be recorded, not recomputed, because the orbit
    /// can change (a burn re-seeds it) and the old path would no longer follow from the current elements.
    /// </summary>
    public sealed class GroundTrackHistory
    {
        private readonly List<TrackSample> samples = new List<TrackSample>();
        private readonly double minSpacing; // s
        private readonly double window;     // s kept

        public GroundTrackHistory(double minSpacingSeconds = 10.0, double windowSeconds = 7200.0)
        {
            minSpacing = minSpacingSeconds;
            window = windowSeconds;
        }

        public IReadOnlyList<TrackSample> Samples => samples;

        public void Record(double missionElapsedTime, GeoPoint point)
        {
            if (samples.Count > 0 && missionElapsedTime - samples[samples.Count - 1].MissionElapsedTime < minSpacing) return;
            samples.Add(new TrackSample(missionElapsedTime, point));

            int expired = 0;
            while (expired < samples.Count && samples[expired].MissionElapsedTime < missionElapsedTime - window) expired++;
            if (expired > 0) samples.RemoveRange(0, expired);
        }

        public void Clear() => samples.Clear();
    }
}
