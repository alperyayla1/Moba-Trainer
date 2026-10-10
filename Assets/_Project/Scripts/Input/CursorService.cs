using System;
using System.Runtime.InteropServices;
using MobaTrainer.Persistence;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MobaTrainer.Input
{
    public enum CursorState { Normal, Attack, Cast }

    /// <summary>
    /// Game cursor, two modes (Settings > Controls):
    /// <list type="bullet">
    /// <item><b>System (default)</b>: a hardware cursor (Cursor.SetCursor, CursorMode.Auto) with our own art.
    /// It moves exactly like the Windows pointer, with zero extra latency. Like typical MOBA clients, the
    /// in-game sensitivity is applied by temporarily setting the Windows pointer speed while the game is
    /// focused (restored on focus loss and on quit). Optionally confined to the window.</item>
    /// <item><b>Raw input</b>: OS cursor locked; a virtual cursor (UI image) driven by raw mouse delta ×
    /// sensitivity.</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public class CursorService : MonoBehaviour
    {
        public static CursorService Instance { get; private set; }

        [Header("Raw-input cursor (UI image)")]
        public RectTransform cursorRect;
        public RawImage cursorImage;

        [Header("Cursor art (sizes 32 / 48 / 64)")]
        public Texture2D[] normalTextures = new Texture2D[3];
        public Texture2D[] attackTextures = new Texture2D[3];
        public Texture2D[] castTextures = new Texture2D[3];
        [Tooltip("Tip of the arrow in normalised texture coords, from the TOP-LEFT.")]
        public Vector2 hotspot = new Vector2(0.094f, 0.063f);

        public bool GameplayMode { get; private set; }
        public Vector2 ScreenPosition { get; private set; }
        public bool Hardware => SettingsManager.Current.cursorMode == CursorModeSetting.System;

        private static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);
        private CursorState state = CursorState.Normal;
        private Texture2D appliedTexture;
        private CursorModeSetting appliedMode = (CursorModeSetting)(-1);

        // Windows pointer speed (1..20) as found when we took it over; 0 = not overridden.
        private int originalPointerSpeed;

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
            if (Hardware) return;   // lock state handled every frame
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

        /// <summary>Normal / attack-move armed / aiming an ability.</summary>
        public void SetState(CursorState s) => state = s;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            SettingsData s = SettingsManager.Current;

            if (s.cursorMode != appliedMode) SwitchMode(s.cursorMode);

            if (Hardware)
            {
                ScreenPosition = mouse.position.ReadValue();
                if (Application.isFocused)
                {
                    Cursor.visible = true;
                    CursorLockMode want = s.confineCursor ? CursorLockMode.Confined : CursorLockMode.None;
                    if (Cursor.lockState != want) Cursor.lockState = want;
                    ApplyPointerSpeed(true);
                }
                return;
            }

            if (Application.isFocused) Cursor.visible = false;
            if (GameplayMode)
            {
                if (Application.isFocused && Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
                Vector2 delta = mouse.delta.ReadValue() * s.cursorSensitivity;
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
            SettingsData s = SettingsManager.Current;
            Texture2D tex = Pick(s);
            if (Hardware)
            {
                if (tex != appliedTexture && tex != null)
                {
                    appliedTexture = tex;
                    Cursor.SetCursor(tex, new Vector2(hotspot.x * tex.width, hotspot.y * tex.height), CursorMode.Auto);
                }
                return;
            }

            if (cursorRect == null) return;
            if (cursorImage != null && cursorImage.texture != tex)
            {
                cursorImage.texture = tex;
                cursorImage.color = Color.white;
            }
            float size = 30f * s.cursorScale;
            cursorRect.pivot = new Vector2(hotspot.x, 1f - hotspot.y);
            cursorRect.sizeDelta = new Vector2(size, size);
            cursorRect.position = ScreenPosition;
        }

        private Texture2D Pick(SettingsData s)
        {
            Texture2D[] set = state == CursorState.Attack ? attackTextures : state == CursorState.Cast ? castTextures : normalTextures;
            if (set == null || set.Length == 0) return null;
            int i = s.cursorScale < 0.85f ? 0 : (s.cursorScale < 1.3f ? 1 : 2);
            i = Mathf.Clamp(i, 0, set.Length - 1);
            return set[i] != null ? set[i] : set[0];
        }

        private void SwitchMode(CursorModeSetting mode)
        {
            appliedMode = mode;
            appliedTexture = null;
            bool hw = mode == CursorModeSetting.System;
            if (cursorRect != null) cursorRect.gameObject.SetActive(!hw);
            if (hw)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                ApplyPointerSpeed(false);
                Cursor.lockState = GameplayMode ? CursorLockMode.Locked : CursorLockMode.None;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ApplyPointerSpeed(false);   // never leave the user's desktop with our speed
                return;
            }
            if (!Hardware && GameplayMode) Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnApplicationQuit() => ApplyPointerSpeed(false);
        private void OnDisable() => ApplyPointerSpeed(false);

        // ------------------------------------------------------------------ Windows pointer speed

        /// <summary>
        /// Hardware mode: sets the Windows pointer speed to the notch matching the sensitivity setting
        /// while the game has focus (setting not written to the user profile), restores it otherwise.
        /// </summary>
        private void ApplyPointerSpeed(bool apply)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                bool want = apply && Hardware && SettingsManager.Current.applySensitivityToSystem;
                if (want)
                {
                    int notch = NotchFor(SettingsManager.Current.cursorSensitivity);
                    if (originalPointerSpeed == 0)
                    {
                        int current = 0;
                        if (!SystemParametersInfoGet(SPI_GETMOUSESPEED, 0, ref current, 0) || current < 1) return;
                        originalPointerSpeed = current;
                    }
                    if (notch != lastSetNotch)
                    {
                        SystemParametersInfoSet(SPI_SETMOUSESPEED, 0, new IntPtr(notch), 0);
                        lastSetNotch = notch;
                    }
                }
                else if (originalPointerSpeed != 0)
                {
                    SystemParametersInfoSet(SPI_SETMOUSESPEED, 0, new IntPtr(originalPointerSpeed), 0);
                    originalPointerSpeed = 0;
                    lastSetNotch = 0;
                }
            }
            catch (Exception)
            {
                // Not on Windows / API unavailable: the cursor simply uses the system speed.
            }
#endif
        }

        private int lastSetNotch;

        /// <summary>Windows pointer-speed notch (1..20) whose multiplier is closest to the given one.</summary>
        public static int NotchFor(float multiplier)
        {
            int best = 10;
            float bestD = float.MaxValue;
            for (int n = 1; n <= 20; n++)
            {
                float d = Mathf.Abs(Core.SensitivityConverter.FromOtherGame(n * 5f) - multiplier);
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private const uint SPI_GETMOUSESPEED = 0x0070;
        private const uint SPI_SETMOUSESPEED = 0x0071;

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        private static extern bool SystemParametersInfoGet(uint action, uint param, ref int value, uint winIni);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
        private static extern bool SystemParametersInfoSet(uint action, uint param, IntPtr value, uint winIni);
#endif

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
