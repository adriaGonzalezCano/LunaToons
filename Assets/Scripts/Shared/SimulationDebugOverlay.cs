using UnityEngine;

namespace Apollo.Shared
{
    /// <summary>TEMPORARY placeholder so Step 2 can be verified in play mode. Replaced by Mission Control telemetry in Step 5.</summary>
    public sealed class SimulationDebugOverlay : MonoBehaviour
    {
        [SerializeField] private SimulationHost host;

        private void OnGUI()
        {
            if (host == null || host.Orbit == null) return;
            var s = host.CurrentState;
            GUI.Label(new Rect(10, 10, 420, 100),
                $"MET {s.MissionElapsedTime:F1} s (x{host.Clock.TimeScale})\n" +
                $"Altitude {s.Altitude / 1000.0:F3} km\n" +
                $"Speed {s.Speed:F2} m/s\n" +
                $"Period {s.Period / 60.0:F2} min");
        }
    }
}
