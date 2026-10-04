using System;
using MobaTrainer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>
    /// Builds minimal, dark, rounded UGUI elements. Used by the editor scene builder (static screens)
    /// and at runtime (settings rows, scenario cards) so everything shares one look.
    /// </summary>
    public static class UIFactory
    {
        // ------------------------------------------------------------------ layout helpers

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        public static readonly Vector2 TopRight = new Vector2(1f, 1f);
        public static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
        public static readonly Vector2 BottomRight = new Vector2(1f, 0f);
        public static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        public static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        public static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

        public static LayoutElement Layout(Component c, float preferredHeight = -1f, float preferredWidth = -1f, float flexibleWidth = -1f)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0f) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
            if (preferredWidth >= 0f) { le.preferredWidth = preferredWidth; le.minWidth = preferredWidth; }
            if (flexibleWidth >= 0f) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static VerticalLayoutGroup VerticalGroup(Component c, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperLeft)
        {
            var g = c.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup HorizontalGroup(Component c, float spacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var g = c.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            return g;
        }

        // ------------------------------------------------------------------ primitives

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>Rounded rectangle (9-sliced) with the given corner radius.</summary>
        public static Image Rounded(UITheme theme, Transform parent, string name, Color color, float radius, bool raycast = false)
        {
            Image img = Image(parent, name, color, theme != null ? theme.rounded : null, raycast);
            MakeRounded(theme, img, radius);
            return img;
        }

        public static void MakeRounded(UITheme theme, Image img, float radius, bool outline = false)
        {
            if (theme == null) return;
            img.sprite = outline ? theme.roundedOutline : theme.rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UITheme.RadiusMultiplier(radius);
            img.fillCenter = true;
        }

        /// <summary>1.5 px rounded border stretched over <paramref name="parent"/>.</summary>
        public static Image Outline(UITheme theme, Transform parent, Color color, float radius)
        {
            Image o = Image(parent, "Outline", color, theme != null ? theme.roundedOutline : null);
            MakeRounded(theme, o, radius, true);
            Stretch(o.rectTransform);
            return o;
        }

        public static TextMeshProUGUI Text(UITheme theme, Transform parent, string name, string text, float size,
            Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, TextWeight weight = TextWeight.Regular)
        {
            RectTransform rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (theme != null && theme.Font(weight) != null) t.font = theme.Font(weight);
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        // ------------------------------------------------------------------ buttons

        public static Button Button(UITheme theme, Transform parent, string name, string label, ButtonStyle style, Vector2 size, float fontSize = -1f)
        {
            RectTransform rt = Rect(name, parent);
            rt.sizeDelta = size;
            var img = rt.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            img.color = Color.white;
            MakeRounded(theme, img, theme != null ? theme.radiusButton : 10f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            btn.transition = Selectable.Transition.ColorTint;

            Color normal, hover, pressed, labelNormal, labelHover;
            switch (style)
            {
                case ButtonStyle.Primary:
                    normal = Palette.Accent; hover = Palette.AccentHover; pressed = Palette.AccentPressed;
                    labelNormal = labelHover = Color.white;
                    break;
                case ButtonStyle.Secondary:
                    normal = Palette.Surface; hover = Palette.SurfaceHover; pressed = Palette.SurfacePressed;
                    labelNormal = labelHover = Palette.Text;
                    break;
                case ButtonStyle.Danger:
                    normal = Palette.Danger.WithAlpha(0f); hover = Palette.Danger.WithAlpha(0.12f); pressed = Palette.Danger.WithAlpha(0.2f);
                    labelNormal = Palette.Danger; labelHover = Palette.DangerHover;
                    break;
                default: // Ghost
                    normal = Palette.Surface.WithAlpha(0f); hover = Palette.Surface; pressed = Palette.SurfacePressed;
                    labelNormal = Palette.TextMuted; labelHover = Palette.Text;
                    break;
            }

            ColorBlock cb = btn.colors;
            cb.normalColor = normal;
            cb.highlightedColor = hover;
            cb.pressedColor = pressed;
            cb.selectedColor = normal;
            cb.disabledColor = new Color(normal.r, normal.g, normal.b, normal.a * 0.4f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = theme != null ? theme.hoverDuration : 0.12f;
            btn.colors = cb;

            var tint = rt.gameObject.AddComponent<UIHoverTint>();
            tint.duration = theme != null ? theme.hoverDuration : 0.12f;

            if (style == ButtonStyle.Secondary)
            {
                Image outline = Outline(theme, rt, Palette.Border, theme != null ? theme.radiusButton : 10f);
                tint.Add(outline, Palette.Border, Palette.BorderStrong, Palette.BorderStrong);
            }

            if (!string.IsNullOrEmpty(label))
            {
                float fs = fontSize > 0f ? fontSize : (theme != null ? theme.sizeBody : 17f);
                TextWeight w = style == ButtonStyle.Primary ? TextWeight.Semibold : TextWeight.Medium;
                TextMeshProUGUI t = Text(theme, rt, "Label", label, fs, labelNormal, TextAlignmentOptions.Center, w);
                Stretch(t.rectTransform, 12f, 12f, 0f, 0f);
                tint.Add(t, labelNormal, labelHover, labelHover);
            }

            rt.gameObject.AddComponent<UIButtonSounds>();
            return btn;
        }

        public static Image Icon(UITheme theme, Transform parent, string name, UIIcon icon, float size, Color color)
        {
            Image img = Image(parent, name, color, theme != null ? theme.Icon(icon) : null);
            img.preserveAspect = true;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>Square ghost button with a single icon; icon brightens on hover.</summary>
        public static Button IconButton(UITheme theme, Transform parent, string name, UIIcon icon, float size, float iconSize, Color iconNormal, Color iconHover)
        {
            Button b = Button(theme, parent, name, string.Empty, ButtonStyle.Ghost, new Vector2(size, size));
            Image ic = Icon(theme, b.transform, "Icon", icon, iconSize, iconNormal);
            Place(ic.rectTransform, Center, Center, Vector2.zero, new Vector2(iconSize, iconSize));
            b.GetComponent<UIHoverTint>().Add(ic, iconNormal, iconHover, iconHover);
            return b;
        }

        /// <summary>Row button: icon on the left, short label. Used for compact menus (pause).</summary>
        public static Button IconLabelButton(UITheme theme, Transform parent, string name, UIIcon icon, string label, ButtonStyle style, Vector2 size)
        {
            Button b = Button(theme, parent, name, label, style, size);
            TextMeshProUGUI t = Label(b);
            t.alignment = TextAlignmentOptions.Left;
            Stretch(t.rectTransform, 56f, 16f, 0f, 0f);
            Color normal = style == ButtonStyle.Primary ? Color.white : style == ButtonStyle.Danger ? Palette.Danger : Palette.TextMuted;
            Color hover = style == ButtonStyle.Primary ? Color.white : style == ButtonStyle.Danger ? Palette.DangerHover : Palette.Text;
            Image ic = Icon(theme, b.transform, "Icon", icon, 20f, normal);
            Place(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(20f, 20f));
            b.GetComponent<UIHoverTint>().Add(ic, normal, hover, hover);
            return b;
        }

        public static TextMeshProUGUI Label(Button b)
        {
            Transform t = b.transform.Find("Label");
            return t != null ? t.GetComponent<TextMeshProUGUI>() : b.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        /// <summary>Changes a button label's colours (normal/hover) after creation.</summary>
        public static void SetLabelColors(Button b, Color normal, Color hover)
        {
            var tint = b.GetComponent<UIHoverTint>();
            TextMeshProUGUI label = Label(b);
            if (tint == null || label == null) return;
            foreach (UIHoverTint.Target t in tint.targets)
            {
                if (t.graphic != label) continue;
                t.normal = normal;
                t.hover = hover;
                t.pressed = hover;
                label.color = normal;
            }
        }

        /// <summary>Small rounded tag, e.g. a scenario code.</summary>
        public static TextMeshProUGUI Tag(UITheme theme, Transform parent, string name, string text, Color fill, Color textColor, out Image background)
        {
            background = Rounded(theme, parent, name, fill, theme != null ? theme.radiusSmall : 8f);
            TextMeshProUGUI t = Text(theme, background.transform, "Text", text, theme != null ? theme.sizeCaption : 13f, textColor, TextAlignmentOptions.Center, TextWeight.Medium);
            Stretch(t.rectTransform, 8f, 8f, 0f, 0f);
            return t;
        }

        // ------------------------------------------------------------------ inputs

        public static Slider Slider(UITheme theme, Transform parent, string name, float min, float max, float value, bool wholeNumbers = false)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = new Vector2(300f, 28f);
            var slider = root.gameObject.AddComponent<Slider>();

            Image hit = Image(root, "Hit", new Color(0f, 0f, 0f, 0f), null, true);
            Stretch(hit.rectTransform);

            Image track = Rounded(theme, root, "Track", Palette.BorderStrong, 2f);
            Stretch(track.rectTransform, 0f, 0f, 12f, 12f);

            RectTransform fillArea = Stretch(Rect("Fill Area", root), 0f, 0f, 12f, 12f);
            Image fill = Rounded(theme, fillArea, "Fill", Palette.Accent, 2f);
            fill.rectTransform.sizeDelta = Vector2.zero;

            RectTransform handleArea = Stretch(Rect("Handle Slide Area", root), 9f, 9f, 0f, 0f);
            Image handle = Image(handleArea, "Handle", Palette.Text, theme != null ? theme.circle : null, true);
            handle.rectTransform.sizeDelta = new Vector2(18f, 0f);
            handle.preserveAspect = true;

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.SetValueWithoutNotify(value);
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = slider.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.pressedColor = new Color(0.85f, 0.85f, 0.9f, 1f);
            cb.selectedColor = Color.white;
            cb.fadeDuration = 0.1f;
            slider.colors = cb;
            return slider;
        }

        public static UISwitch Switch(UITheme theme, Transform parent, string name, bool on)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = new Vector2(44f, 24f);
            Image track = Rounded(theme, root, "Track", Palette.BorderStrong, 12f, true);
            Stretch(track.rectTransform);
            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = track;
            btn.transition = Selectable.Transition.None;
            btn.navigation = new Navigation { mode = Navigation.Mode.None };
            Image knob = Image(root, "Knob", Color.white, theme != null ? theme.circle : null);
            Place(knob.rectTransform, Center, Center, Vector2.zero, new Vector2(18f, 18f));
            root.gameObject.AddComponent<UIButtonSounds>();
            var sw = root.gameObject.AddComponent<UISwitch>();
            sw.track = track;
            sw.knob = knob.rectTransform;
            sw.travel = 20f;
            sw.Set(on, true);
            return sw;
        }

        public static TMP_InputField InputField(UITheme theme, Transform parent, string name, string placeholder, float fontSize, Vector2 size, string prefix = null)
        {
            RectTransform root = Rect(name, parent);
            root.sizeDelta = size;
            Image bg = root.gameObject.AddComponent<Image>();
            bg.color = Palette.Surface;
            MakeRounded(theme, bg, theme != null ? theme.radiusButton : 10f);
            Image outline = Outline(theme, root, Palette.Border, theme != null ? theme.radiusButton : 10f);
            outline.name = "Outline";

            float left = 18f;
            if (!string.IsNullOrEmpty(prefix))
            {
                TextMeshProUGUI pre = Text(theme, root, "Prefix", prefix, fontSize, Palette.Accent, TextAlignmentOptions.Left, TextWeight.Semibold);
                Stretch(pre.rectTransform, 18f, 0f, 0f, 0f);
                left = 42f;
            }

            RectTransform area = Stretch(Rect("Text Area", root), left, 16f, 2f, 2f);
            area.gameObject.AddComponent<RectMask2D>();

            TextMeshProUGUI ph = Text(theme, area, "Placeholder", placeholder, fontSize, Palette.TextFaint, TextAlignmentOptions.Left);
            Stretch(ph.rectTransform);
            TextMeshProUGUI txt = Text(theme, area, "Text", string.Empty, fontSize, Palette.Text, TextAlignmentOptions.Left, TextWeight.Medium);
            Stretch(txt.rectTransform);

            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = txt;
            field.placeholder = ph;
            if (theme != null && theme.fontMedium != null) field.fontAsset = theme.fontMedium;
            field.pointSize = fontSize;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.characterLimit = 16;
            field.customCaretColor = true;
            field.caretColor = Palette.Accent;
            field.caretWidth = 2;
            field.caretBlinkRate = 1.1f;
            field.selectionColor = Palette.AccentSoft;
            field.navigation = new Navigation { mode = Navigation.Mode.None };
            field.transition = Selectable.Transition.None;
            field.targetGraphic = bg;
            return field;
        }

        // ------------------------------------------------------------------ settings rows

        public const float RowHeight = 56f;

        /// <summary>Label left, control area right, hairline divider below. Returns the control area.</summary>
        public static RectTransform Row(UITheme theme, Transform parent, string label, out TextMeshProUGUI labelText, float controlWidth = 360f)
        {
            RectTransform row = Rect("Row_" + label, parent);
            Layout(row, RowHeight);

            labelText = Text(theme, row, "Label", label, theme != null ? theme.sizeBody : 17f, Palette.Text, TextAlignmentOptions.Left);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(1f, 1f);
            labelText.rectTransform.offsetMin = new Vector2(0f, 0f);
            labelText.rectTransform.offsetMax = new Vector2(-controlWidth - 24f, 0f);

            Image divider = Image(row, "Divider", Palette.Border.WithAlpha(0.7f), theme != null ? theme.white : null);
            divider.rectTransform.anchorMin = new Vector2(0f, 0f);
            divider.rectTransform.anchorMax = new Vector2(1f, 0f);
            divider.rectTransform.pivot = new Vector2(0.5f, 0f);
            divider.rectTransform.sizeDelta = new Vector2(0f, 1f);
            divider.rectTransform.anchoredPosition = Vector2.zero;

            RectTransform content = Rect("Control", row);
            content.anchorMin = new Vector2(1f, 0f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(1f, 0.5f);
            content.sizeDelta = new Vector2(controlWidth, -16f);
            content.anchoredPosition = Vector2.zero;
            return content;
        }

        public static TextMeshProUGUI Header(UITheme theme, Transform parent, string text)
        {
            TextMeshProUGUI t = Text(theme, parent, "Header_" + text, text.ToUpperInvariant(), theme != null ? theme.sizeCaption : 13f,
                Palette.TextFaint, TextAlignmentOptions.BottomLeft, TextWeight.Semibold);
            t.characterSpacing = 8f;
            t.margin = new Vector4(0f, 0f, 0f, 8f);
            Layout(t, 48f);
            return t;
        }

        public static Slider SliderRow(UITheme theme, Transform parent, string label, float min, float max, float value,
            Func<float, string> format, UnityAction<float> onChanged)
        {
            RectTransform content = Row(theme, parent, label, out _);
            TextMeshProUGUI valueText = Text(theme, content, "Value", format(value), theme != null ? theme.sizeSmall : 15f, Palette.TextMuted, TextAlignmentOptions.Right);
            valueText.rectTransform.anchorMin = new Vector2(1f, 0f);
            valueText.rectTransform.anchorMax = new Vector2(1f, 1f);
            valueText.rectTransform.pivot = new Vector2(1f, 0.5f);
            valueText.rectTransform.sizeDelta = new Vector2(64f, 0f);
            valueText.rectTransform.anchoredPosition = Vector2.zero;

            Slider s = Slider(theme, content, "Slider", min, max, value);
            RectTransform srt = (RectTransform)s.transform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = new Vector2(-80f, 0f);
            s.onValueChanged.AddListener(v =>
            {
                valueText.text = format(v);
                onChanged?.Invoke(v);
            });
            return s;
        }

        public static UISwitch SwitchRow(UITheme theme, Transform parent, string label, bool on, Action<bool> onChanged)
        {
            RectTransform content = Row(theme, parent, label, out _);
            UISwitch sw = Switch(theme, content, "Switch", on);
            Place((RectTransform)sw.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(44f, 24f));
            sw.Changed += v => onChanged?.Invoke(v);
            return sw;
        }

        /// <summary>"‹  value  ›" selector. Returns a setter to refresh the displayed value.</summary>
        public static Action<int> CycleRow(UITheme theme, Transform parent, string label, string[] options, int index, Action<int> onChanged)
        {
            RectTransform content = Row(theme, parent, label, out _);
            int current = Mathf.Clamp(index, 0, options.Length - 1);

            Image box = Rounded(theme, content, "Box", Palette.Surface, theme != null ? theme.radiusButton : 10f);
            Stretch(box.rectTransform);
            Outline(theme, box.transform, Palette.Border, theme != null ? theme.radiusButton : 10f);

            TextMeshProUGUI valueText = Text(theme, box.transform, "Value", options[current], theme != null ? theme.sizeSmall : 15f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Medium);
            Stretch(valueText.rectTransform, 44f, 44f, 0f, 0f);

            Button prev = Button(theme, box.transform, "Prev", "‹", ButtonStyle.Ghost, new Vector2(40f, 0f), 22f);
            RectTransform prt = (RectTransform)prev.transform;
            prt.anchorMin = new Vector2(0f, 0f); prt.anchorMax = new Vector2(0f, 1f); prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(40f, -6f); prt.anchoredPosition = new Vector2(3f, 0f);
            Button next = Button(theme, box.transform, "Next", "›", ButtonStyle.Ghost, new Vector2(40f, 0f), 22f);
            RectTransform nrt = (RectTransform)next.transform;
            nrt.anchorMin = new Vector2(1f, 0f); nrt.anchorMax = new Vector2(1f, 1f); nrt.pivot = new Vector2(1f, 0.5f);
            nrt.sizeDelta = new Vector2(40f, -6f); nrt.anchoredPosition = new Vector2(-3f, 0f);

            void Set(int i)
            {
                current = (i % options.Length + options.Length) % options.Length;
                valueText.text = options[current];
            }

            prev.onClick.AddListener(() => { Set(current - 1); onChanged?.Invoke(current); });
            next.onClick.AddListener(() => { Set(current + 1); onChanged?.Invoke(current); });
            return Set;
        }
    }
}
