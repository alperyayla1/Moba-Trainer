using MobaTrainer.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MobaTrainer.Input
{
    /// <summary>
    /// Custom cursor for the whole game (drawn as a top-most UI image, OS cursor hidden).
    /// In menus it follows the OS pointer. In gameplay the OS cursor is locked and a virtual
    /// cursor is driven by raw mouse delta × sensitivity, which gives us the cursor-sensitivity
    /// setting and reliable edge-panning (the cursor can't leave the window).
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public class CursorService : MonoBehaviour
    {
        public static CursorService Instance { get; private set; }

        [Header("Cursor Visual")]
        [Tooltip("RectTransform of the cursor image on the top-most cursor canvas.")]
        public RectTransform cursorRect;
        public RawImage cursorImage;

        [Header("Gauntlet (from Blindfold Chess)")]
        public Texture2D gauntletTexture;
        [Tooltip("Hotspot in normalised texture coords, measured from the TOP-LEFT.")]
        public Vector2 gauntletHotspot = new Vector2(0.05f, 0.1f);
        public Vector2 gauntletSize = new Vector2(30f, 38f);

        [Header("Arrow")]
        public Texture2D arrowTexture;
        public Vector2 arrowHotspot = new Vector2(0.06f, 0.04f);
        public Vector2 arrowSize = new Vector2(30f, 30f);

        public bool GameplayMode { get; private set; }
        public Vector2 ScreenPosition { get; private set; }

        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);
        private CursorStyle appliedStyle = (CursorStyle)(-1);
        private float appliedScale = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (Mouse.current != null) ScreenPosition = Mouse.current.position.ReadValue();
        }

        public void SetGameplayMode(bool gameplay)
        {
            if (GameplayMode == gameplay) return;
            GameplayMode = gameplay;
            Mouse mouse = Mouse.current;
            if (gameplay)
            {
                if (mouse != null) ScreenPosition = mouse.position.ReadValue();
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                if (mouse != null) mouse.WarpCursorPosition(ScreenPosition);
            }
        }

        public void SetTint(Color color)
        {
            if (cursorImage != null) cursorImage.color = color;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (Application.isFocused) Cursor.visible = false;

            if (GameplayMode)
            {
                if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
                Vector2 delta = mouse.delta.ReadValue() * SettingsManager.Current.cursorSensitivity;
                Vector2 p = ScreenPosition + delta;
                p.x = Mathf.Clamp(p.x, 0f, Screen.width - 1f);
                p.y = Mathf.Clamp(p.y, 0f, Screen.height - 1f);
                ScreenPosition = p;
            }
            else
            {
                ScreenPosition = mouse.position.ReadValue();
            }
        }

        private void LateUpdate()
        {
            if (cursorRect == null) return;
            SettingsData s = SettingsManager.Current;
            if (s.cursorStyle != appliedStyle || !Mathf.Approximately(s.cursorScale, appliedScale)) ApplyStyle(s);
            cursorRect.position = ScreenPosition;
        }

        private void ApplyStyle(SettingsData s)
        {
            appliedStyle = s.cursorStyle;
            appliedScale = s.cursorScale;
            bool arrow = s.cursorStyle == CursorStyle.Arrow && arrowTexture != null;
            Texture2D tex = arrow ? arrowTexture : gauntletTexture;
            Vector2 hot = arrow ? arrowHotspot : gauntletHotspot;
            Vector2 size = (arrow ? arrowSize : gauntletSize) * s.cursorScale;
            if (cursorImage != null) cursorImage.texture = tex;
            cursorRect.pivot = new Vector2(hot.x, 1f - hot.y);
            cursorRect.sizeDelta = size;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus && GameplayMode) Cursor.lockState = CursorLockMode.Locked;
        }

        // ------------------------------------------------------------------ world helpers

        public Ray ScreenRay(Camera cam) => cam.ScreenPointToRay(ScreenPosition);

        /// <summary>Cursor position projected on the arena plane (y = 0).</summary>
        public bool TryGetGroundPoint(Camera cam, out Vector3 point)
        {
            Ray ray = ScreenRay(cam);
            if (Ground.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                point.y = 0f;
                return true;
            }
            point = Vector3.zero;
            return false;
        }
    }
}
