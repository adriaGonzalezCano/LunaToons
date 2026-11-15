using Apollo.OrbitalSim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Shared
{
    /// <summary>
    /// TEMPORARY test keys (replaced by the control station and the real WASD/QE scheme in steps 5-7).
    /// Attitude: arrows = pitch/yaw, Comma/Period = roll, X (hold) = stop rotation.
    /// Thrusters (along the ship's body axes): T = forward, Shift+T = backward, J/L = left/right, U/N = up/down. R = restart.
    /// </summary>
    public sealed class AttitudeTestControls : MonoBehaviour
    {
        [SerializeField] private SimulationHost host;

        private void Awake()
        {
            if (host == null) host = GetComponent<SimulationHost>();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null || host == null) return;

            bool shift = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
            host.SetControls(new ControlInput
            {
                Pitch = Axis(k.upArrowKey.isPressed, k.downArrowKey.isPressed),     // Up = nose up
                Yaw = Axis(k.rightArrowKey.isPressed, k.leftArrowKey.isPressed),    // Right = nose right
                Roll = Axis(k.periodKey.isPressed, k.commaKey.isPressed),           // Period = roll right
                StopRotation = k.xKey.isPressed,
                TranslateX = k.tKey.isPressed ? (shift ? -1.0 : 1.0) : 0.0,
                TranslateY = Axis(k.lKey.isPressed, k.jKey.isPressed),              // L = right
                TranslateZ = Axis(k.uKey.isPressed, k.nKey.isPressed)               // U = up
            });

            if (k.rKey.wasPressedThisFrame) host.Restart("manual restart");
        }

        private static double Axis(bool positive, bool negative) => (positive ? 1.0 : 0.0) - (negative ? 1.0 : 0.0);

        private void OnGUI()
        {
            if (host == null) return;

            GUI.Label(new Rect(10, Screen.height - 78, 1100, 24), "TEST: arrows = pitch/yaw | , . = roll | X = stop rotation | T / Shift+T = thrust fwd/back | J L = left/right | U N = up/down | R = restart");
            Vector3dRate r = new Vector3dRate(host.Attitude.Rate);
            GUI.Label(new Rect(10, Screen.height - 102, 900, 24), $"rates deg/s  roll {r.X:F2}  pitch {r.Y:F2}  yaw {r.Z:F2}   (body Y = left)");

            if (host.EngineOn)
            {
                Color previous = GUI.color;
                GUI.color = Color.green;
                GUI.Label(new Rect(10, 34, 900, 24), $"THRUSTERS ON   a = {host.ThrustAcceleration:F1} m/s^2 per axis   dV used {host.DeltaVUsed:F1} m/s");
                GUI.color = previous;
            }
            else if (!string.IsNullOrEmpty(host.EngineCutoffReason))
            {
                Color previous = GUI.color;
                GUI.color = Color.yellow;
                GUI.Label(new Rect(10, 34, 900, 24), $"THRUSTERS CUT OFF: {host.EngineCutoffReason} (release the keys and press again)");
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

        private readonly struct Vector3dRate
        {
            public readonly double X, Y, Z;
            public Vector3dRate(Vector3d rate) { X = rate.X * 57.29578; Y = rate.Y * 57.29578; Z = rate.Z * 57.29578; }
        }
    }
}
