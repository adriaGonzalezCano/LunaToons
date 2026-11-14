using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Shared
{
    /// <summary>Testing aid: keys 1-4 set time scale to x1, x10, x100, x1000; P pauses. Works in both environments.</summary>
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
    }
}
