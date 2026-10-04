using System;
using System.Collections.Generic;
using MobaTrainer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>Drill selection: a 3x3 grid of icon cards with short names. Locked drills are dimmed.</summary>
    public class DrillsPanel : UIPanel
    {
        private class Drill
        {
            public string name;
            public UIIcon icon;
            public bool available;
            public Action onSelect;
        }

        [Header("Theme")]
        public UITheme theme;

        [Header("References")]
        public RectTransform grid;
        public Button backButton;

        [Header("Cards")]
        public Vector2 cardSize = new Vector2(250f, 168f);

        public event Action BackRequested;

        private readonly List<Drill> drills = new List<Drill>();
        private bool built;

        public void AddDrill(string name, UIIcon icon, bool available, Action onSelect)
        {
            drills.Add(new Drill { name = name, icon = icon, available = available, onSelect = onSelect });
            built = false;
        }

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(() => BackRequested?.Invoke());
        }

        protected override void OnShown()
        {
            if (built) return;
            built = true;
            for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
            foreach (Drill d in drills) BuildCard(d);
        }

        private void BuildCard(Drill d)
        {
            const float radius = 18f;
            Button b = UIFactory.Button(theme, grid, "Drill_" + d.name, string.Empty, ButtonStyle.Secondary, cardSize);
            UIFactory.MakeRounded(theme, (Image)b.targetGraphic, radius);
            ColorBlock cb = b.colors;
            cb.normalColor = Palette.Surface.WithAlpha(0.72f);
            cb.highlightedColor = Palette.SurfaceHover.WithAlpha(0.9f);
            cb.pressedColor = Palette.SurfacePressed;
            cb.selectedColor = cb.normalColor;
            cb.disabledColor = Palette.Surface.WithAlpha(0.35f);
            b.colors = cb;

            var tint = b.GetComponent<UIHoverTint>();
            Transform outlineT = b.transform.Find("Outline");
            if (outlineT != null)
            {
                var o = outlineT.GetComponent<Image>();
                UIFactory.MakeRounded(theme, o, radius, true);
                foreach (UIHoverTint.Target t in tint.targets)
                {
                    if (t.graphic != o) continue;
                    t.hover = Palette.Accent.WithAlpha(0.75f);
                    t.pressed = Palette.Accent;
                }
            }

            Color iconColor = d.available ? Palette.Text : Palette.TextFaint;
            Image icon = UIFactory.Icon(theme, b.transform, "Icon", d.icon, 46f, iconColor);
            UIFactory.Place(icon.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 18f), new Vector2(46f, 46f));
            tint.Add(icon, iconColor, Palette.Accent, Palette.AccentPressed);

            TextMeshProUGUI name = UIFactory.Text(theme, b.transform, "Name", d.name.ToUpperInvariant(), theme.sizeSmall, d.available ? Palette.TextMuted : Palette.TextFaint,
                TextAlignmentOptions.Center, TextWeight.Heading);
            name.characterSpacing = 8f;
            UIFactory.Place(name.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, new Vector2(0f, 26f), new Vector2(cardSize.x - 20f, 26f));
            tint.Add(name, name.color, Palette.Text, Palette.Text);

            if (!d.available)
            {
                b.interactable = false;
                Image lockIcon = UIFactory.Icon(theme, b.transform, "Lock", UIIcon.Lock, 14f, Palette.TextFaint);
                UIFactory.Place(lockIcon.rectTransform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(-16f, -16f), new Vector2(14f, 14f));
            }

            b.gameObject.AddComponent<UIHoverScale>().hoverScale = 1.04f;
            Drill captured = d;
            b.onClick.AddListener(() => { if (captured.available) captured.onSelect?.Invoke(); });
        }
    }
}
