using MobaTrainer.Input;
using MobaTrainer.Persistence;
using UnityEngine;
using Mouse = UnityEngine.InputSystem.Mouse;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Angled top-down MOBA camera: locked (follows the player) or unlocked (edge-pan with the
    /// virtual cursor), hold Center to snap back, mouse wheel zoom. Toggle lock with CameraLock.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class MobaCameraController : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Framing")]
        [Range(35f, 85f)] public float pitch = 56f;
        public float yaw = 0f;
        public float fieldOfView = 40f;
        public float distance = 22f;
        public float minDistance = 15f;
        public float maxDistance = 22f;
        public float zoomStep = 1.5f;
        [Tooltip("Focus offset so the player sits slightly below screen centre.")]
        public Vector3 focusOffset = new Vector3(0f, 0f, 0.5f);

        [Header("Edge Pan")]
        [Tooltip("Pixels from the screen edge that start panning.")]
        public float edgeThreshold = 6f;
        [Tooltip("How far the unlocked camera may wander outside the arena radius.")]
        public float boundsPadding = 3f;

        [Header("Menu Orbit (title screen background)")]
        public float orbitPitch = 26f;
        public float orbitDistance = 34f;
        public float orbitDegreesPerSecond = 3.5f;
        public float orbitFov = 34f;
        [Tooltip("Shifts the camera sideways so the arena sits right of the menu text.")]
        public float orbitSideOffset = 9f;
        public Vector3 orbitFocus = new Vector3(0f, 1f, 0f);

        /// <summary>Slow cinematic orbit around the arena (menus). Gameplay camera logic is skipped.</summary>
        public bool OrbitMode { get; set; }

        private float orbitYaw = 30f;

        public bool Locked { get; private set; } = true;
        public bool GameplayActive { get; set; }

        private Camera cam;
        private Vector3 focus;
        private float targetDistance;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.fieldOfView = fieldOfView;
            targetDistance = distance;
        }

        public void ResetCamera()
        {
            Locked = SettingsManager.Current.cameraLockedByDefault;
            focus = target != null ? target.position + focusOffset : Vector3.zero;
            distance = targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
            Apply();
        }

        public void SetLocked(bool locked) => Locked = locked;

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (OrbitMode)
            {
                orbitYaw += orbitDegreesPerSecond * dt;
                Quaternion orot = Quaternion.Euler(orbitPitch, orbitYaw, 0f);
                Vector3 opos = orbitFocus - orot * Vector3.forward * orbitDistance + orot * Vector3.left * orbitSideOffset;
                transform.SetPositionAndRotation(opos, orot);
                cam.fieldOfView = orbitFov;
                return;
            }
            if (!Mathf.Approximately(cam.fieldOfView, fieldOfView)) cam.fieldOfView = fieldOfView;
            InputManager input = InputManager.Instance;

            if (GameplayActive && input != null && !input.SuppressShortcuts && Time.timeScale > 0f)
            {
                if (input.Pressed(GameAction.CameraLock)) Locked = !Locked;

                Mouse mouse = Mouse.current;
                if (mouse != null)
                {
                    float scroll = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scroll) > 0.01f) targetDistance = Mathf.Clamp(targetDistance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
                }

                bool centerHeld = input.Held(GameAction.CameraCenter);
                if (!Locked && !centerHeld) EdgePan(dt);
                if ((Locked || centerHeld) && target != null)
                {
                    // Locked follow is rigid (no smoothing) so aiming never drifts relative to the player.
                    focus = target.position + focusOffset;
                }
            }
            else if (target != null && Locked)
            {
                focus = target.position + focusOffset;
            }

            distance = Mathf.Lerp(distance, targetDistance, 1f - Mathf.Exp(-12f * dt));
            Apply();
        }

        private void EdgePan(float dt)
        {
            CursorService cursor = CursorService.Instance;
            if (cursor == null || !Application.isFocused) return;
            Vector2 p = cursor.ScreenPosition;
            Vector3 move = Vector3.zero;
            if (p.x <= edgeThreshold) move.x -= 1f;
            else if (p.x >= Screen.width - 1 - edgeThreshold) move.x += 1f;
            if (p.y <= edgeThreshold) move.z -= 1f;
            else if (p.y >= Screen.height - 1 - edgeThreshold) move.z += 1f;
            if (move.sqrMagnitude < 0.01f) return;

            Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);
            focus += yawRot * move.normalized * (SettingsManager.Current.edgePanSpeed * dt);
            float limit = (Arena.Instance != null ? Arena.Instance.radius : 25f) + boundsPadding;
            Vector2 flat = new Vector2(focus.x, focus.z);
            if (flat.sqrMagnitude > limit * limit)
            {
                flat = flat.normalized * limit;
                focus.x = flat.x;
                focus.z = flat.y;
            }
        }

        private void Apply()
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        }
    }
}
