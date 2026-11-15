using Apollo.OrbitalSim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Shared
{
    /// <summary>
    /// TEMPORARY test keys for V2 step 3 (replaced by the control station and the real WASD/QE scheme in steps 5-7).
    /// Up/Down arrows: pitch, Left/Right arrows: yaw, Comma/Period: roll, X (hold): stop rotation.
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

            host.SetControls(new ControlInput
            {
                Pitch = Axis(k.upArrowKey.isPressed, k.downArrowKey.isPressed),     // Up = nose up
                Yaw = Axis(k.rightArrowKey.isPressed, k.leftArrowKey.isPressed),    // Right = nose right
                Roll = Axis(k.periodKey.isPressed, k.commaKey.isPressed),           // Period = roll right
                StopRotation = k.xKey.isPressed
            });
        }

        private static double Axis(bool positive, bool negative) => (positive ? 1.0 : 0.0) - (negative ? 1.0 : 0.0);

        private void OnGUI()
        {
            if (host == null) return;
            GUI.Label(new Rect(10, Screen.height - 78, 900, 24), "TEST attitude: arrows = pitch/yaw | , . = roll | X (hold) = stop rotation");
            var r = host.Attitude.Rate;
            GUI.Label(new Rect(10, Screen.height - 102, 900, 24),
                $"rates deg/s  roll {r.X * 57.29578:F2}  pitch {r.Y * 57.29578:F2}  yaw {r.Z * 57.29578:F2}   (body Y = left)");
        }
    }
}
