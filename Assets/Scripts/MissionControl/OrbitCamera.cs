using UnityEngine;
using UnityEngine.InputSystem;

namespace Apollo.MissionControl
{
    /// <summary>
    /// Mission Control orbit camera: hold left or right mouse button and drag to rotate the view around Earth,
    /// mouse wheel to zoom. The pivot is the parent transform (the Mission Control rig, which holds Earth at its origin).
    /// </summary>
    public sealed class OrbitCamera : MonoBehaviour
    {
        [SerializeField] private float distance = 190f;   // scene units from Earth center
        [SerializeField] private float yaw;               // degrees
        [SerializeField] private float pitch = 28f;       // degrees above the equatorial plane
        [SerializeField] private float rotateSpeed = 0.25f;   // degrees per mouse pixel
        [SerializeField] private float zoomSpeed = 0.0008f;   // fraction of distance per wheel unit
        [SerializeField] private float minDistance = 80f;
        [SerializeField] private float maxDistance = 600f;

        private void OnEnable() => Apply();

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * rotateSpeed;
                pitch = Mathf.Clamp(pitch - delta.y * rotateSpeed, -89f, 89f);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                distance = Mathf.Clamp(distance * (1f - scroll * zoomSpeed), minDistance, maxDistance);

            Apply();
        }

        private void Apply()
        {
            transform.localPosition = Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -distance);
            transform.LookAt(transform.parent.position);
        }
    }
}
