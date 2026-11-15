using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Shared
{
    /// <summary>
    /// TEMPORARY test aid for V2 milestone 0 (removed when real propulsion exists).
    /// ] = +10 m/s along the velocity, [ = -10 m/s, R = restart. Also shows the restart notice.
    /// </summary>
    public sealed class DebugImpulseControls : MonoBehaviour
    {
        private const double ImpulseMetersPerSecond = 10.0;

        [SerializeField] private SimulationHost host;

        private void Awake()
        {
            if (host == null) host = GetComponent<SimulationHost>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || host == null) return;

            if (keyboard.rightBracketKey.wasPressedThisFrame) host.ApplyProgradeVelocityChange(ImpulseMetersPerSecond);
            if (keyboard.leftBracketKey.wasPressedThisFrame) host.ApplyProgradeVelocityChange(-ImpulseMetersPerSecond);
            if (keyboard.rKey.wasPressedThisFrame) host.Restart("manual restart");
        }

        private void OnGUI()
        {
            if (host == null) return;

            GUI.Label(new Rect(10, Screen.height - 54, 900, 24), "[ ]: -/+10 m/s along velocity (debug) | R: restart");

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
