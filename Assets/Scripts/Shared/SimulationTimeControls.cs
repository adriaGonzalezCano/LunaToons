using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Shared
{
    /// <summary>Time controls: keys 1-4 set time scale to x1, x10, x100, x1000; P pauses. Works in both environments. Also draws a small controls hint (placeholder UI).</summary>
    public sealed class SimulationTimeControls : MonoBehaviour
    {
        [SerializeField] private SimulationHost host;

        private void Awake()
        {
            if (host == null) host = GetComponent<SimulationHost>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || host == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) host.SetTimeScale(1);
            if (keyboard.digit2Key.wasPressedThisFrame) host.SetTimeScale(10);
            if (keyboard.digit3Key.wasPressedThisFrame) host.SetTimeScale(100);
            if (keyboard.digit4Key.wasPressedThisFrame) host.SetTimeScale(1000);
            if (keyboard.pKey.wasPressedThisFrame) host.SetPaused(!host.Clock.IsPaused);
        }

        private void OnGUI()
        {
            if (host == null) return;
            GUI.Label(new Rect(10, Screen.height - 30, 900, 24),
                $"Tab: spacecraft / Mission Control | 1-4: time x1/10/100/1000 | P: pause | Esc: cursor   [time x{host.Clock.TimeScale}{(host.Clock.IsPaused ? ", PAUSED" : "")}]");
        }
    }
}
