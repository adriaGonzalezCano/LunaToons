using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Shared
{
    /// <summary>
    /// TEMPORARY keyboard control for V2 (replaced by the control station later).
    /// Hold T: engine on (thrust 1) along the velocity. Release T: engine off, coasting. Hold Shift with T: thrust against the
    /// velocity (placeholder until attitude exists). R: restart. Also shows the restart notice.
    /// </summary>
    public sealed class ThrustControls : MonoBehaviour
    {
        [SerializeField] private SimulationHost host;

        private bool engineRequested;

        private void Awake()
        {
            if (host == null) host = GetComponent<SimulationHost>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || host == null) return;

            bool retrograde = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            // The engine follows the T key. If the simulation cuts the engine itself (escape trajectory), it stays off until
            // T is released and pressed again.
            if (keyboard.tKey.wasPressedThisFrame) engineRequested = true;
            if (!keyboard.tKey.isPressed) engineRequested = false;
            if (engineRequested && !keyboard.tKey.wasPressedThisFrame && !host.EngineOn && !string.IsNullOrEmpty(host.EngineCutoffReason)) engineRequested = false;
            host.SetThrust(engineRequested, retrograde);

            if (keyboard.rKey.wasPressedThisFrame) host.Restart("manual restart");
        }

        private void OnGUI()
        {
            if (host == null) return;

            GUI.Label(new Rect(10, Screen.height - 54, 900, 24), "T (hold): burn prograde | Shift+T: burn retrograde | R: restart");

            if (host.EngineOn)
            {
                Color previous = GUI.color;
                GUI.color = Color.green;
                GUI.Label(new Rect(10, 34, 900, 24), $"ENGINE ON   thrust 1   a = {host.ThrustAcceleration:F1} m/s^2   dV used {host.DeltaVUsed:F1} m/s");
                GUI.color = previous;
            }
            else if (!string.IsNullOrEmpty(host.EngineCutoffReason))
            {
                Color previous = GUI.color;
                GUI.color = Color.yellow;
                GUI.Label(new Rect(10, 34, 900, 24), $"ENGINE CUT OFF: {host.EngineCutoffReason} (release and press T again)");
                GUI.color = previous;
            }

            if (Time.realtimeSinceStartup - host.LastRestartRealTime < 6f)
            {
                Color previous = GUI.color;
                GUI.color = Color.red;
                GUI.Label(new Rect(10, 10, 900, 24), $"SIMULATION RESTARTED: {host.LastRestartReason}");
                GUI.color = previous;
            }
        }
    }
}
