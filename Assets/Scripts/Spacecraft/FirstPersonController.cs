using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.Spacecraft
{
    /// <summary>
    /// Basic first-person walking: WASD + mouse look, CharacterController, simple gravity.
    /// V1 uses artificial gravity so the interior can be walked; microgravity is out of scope here.
    /// Escape toggles the mouse cursor lock. Reads devices directly via the Input System package.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3f;       // m/s
        [SerializeField] private float lookSensitivity = 0.1f; // degrees per mouse pixel
        [SerializeField] private float gravity = -9.81f;     // m/s^2, local to the interior only

        private CharacterController controller;
        private Transform cameraTransform;
        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            cameraTransform = GetComponentInChildren<Camera>().transform;
        }

        private void OnEnable() => SetCursorLocked(true);
        private void OnDisable() => SetCursorLocked(false);

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 look = mouse.delta.ReadValue() * lookSensitivity;
                transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -85f, 85f);
                cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);
            }

            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            Vector3 horizontal = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1f) * moveSpeed;

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -1f;
            verticalVelocity += gravity * Time.deltaTime;

            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
