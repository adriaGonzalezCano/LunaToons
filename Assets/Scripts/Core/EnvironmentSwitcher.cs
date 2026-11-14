using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Core
{
    /// <summary>Tab toggles between the spacecraft rig and the Mission Control rig. The simulation (SimulationHost) is never touched.</summary>
    public sealed class EnvironmentSwitcher : MonoBehaviour
    {
        [SerializeField] private GameObject spacecraftRig;
        [SerializeField] private GameObject missionControlRig;

        private bool inMissionControl;

        private void Start() => Apply(false);

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                inMissionControl = !inMissionControl;
                Apply(inMissionControl);
            }
        }

        private void Apply(bool missionControl)
        {
            spacecraftRig.SetActive(!missionControl);
            missionControlRig.SetActive(missionControl);
        }
    }
}
