using UnityEngine;

namespace Apollo.Shared
{
    /// <summary>TEMPORARY placeholder to verify the simulation in play mode. Replaced by Mission Control telemetry in Step 5.</summary>
    public sealed class SimulationDebugOverlay : MonoBehaviour
    {
        [SerializeField] private SimulationHost host;

        private void Awake()
        {
            // Fallback so an unassigned reference still works.
            if (host == null) host = GetComponent<SimulationHost>();
        }

        private void OnGUI()
        {
            if (host == null || host.Orbit == null) return;
            var s = host.CurrentState;
            GUI.Label(new Rect(10, 10, 460, 130),
                $"MET {s.MissionElapsedTime:F1} s (x{host.Clock.TimeScale}{(host.Clock.IsPaused ? ", PAUSED" : "")})\n" +
                $"Altitude {s.Altitude / 1000.0:F3} km\n" +
                $"Speed {s.Speed:F2} m/s\n" +
                $"Period {s.Period / 60.0:F2} min\n" +
                "Tab: spacecraft / Mission Control | 1-4: time x1/10/100/1000 | P: pause");
        }
    }
}
