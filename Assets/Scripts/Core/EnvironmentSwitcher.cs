using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Core
{
    /// <summary>
    /// Switches between environments: Tab cycles, F1/F2/F3 jump directly (spacecraft, Control Center 1 orbit view,
    /// Control Center 2 ground track). Only toggles which rig is active; the simulation (SimulationHost) is never touched.
    /// </summary>
    public sealed class EnvironmentSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject[] rigs;

        private int current;

        private void Start() => Apply(0);

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || rigs == null || rigs.Length == 0) return;

            if (keyboard.tabKey.wasPressedThisFrame) Apply((current + 1) % rigs.Length);
            if (keyboard.f1Key.wasPressedThisFrame && rigs.Length > 0) Apply(0);
            if (keyboard.f2Key.wasPressedThisFrame && rigs.Length > 1) Apply(1);
            if (keyboard.f3Key.wasPressedThisFrame && rigs.Length > 2) Apply(2);
        }

        private void Apply(int index)
        {
            current = index;
            for (int i = 0; i < rigs.Length; i++)
                rigs[i].SetActive(i == index);
        }
    }
}
