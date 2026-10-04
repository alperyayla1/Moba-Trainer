using System;
using System.Collections.Generic;
using MobaTrainer.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.UI
{
    /// <summary>
    /// Title screen over the live arena background: big title, and a short vertical list of menu
    /// items. Hovering an item slides it right and draws a red marker next to it.
    /// </summary>
    public class MainMenuController : UIPanel
    {
        private class Item
        {
            public Button button;
            public RectTransform label;
            public TextMeshProUGUI text;
            public Image marker;
            public bool hovered;
            public float t;
        }

        [Header("Theme")]
        public UITheme theme;

        [Header("References")]
        public RectTransform itemList;

        [Header("Items")]
        public float itemHeight = 64f;
        public float itemFontSize = 34f;
        public float hoverShift = 26f;

        private readonly List<Item> items = new List<Item>();
        private readonly List<(string label, Action action)> pending = new List<(string, Action)>();
        private bool built;

        /// <summary>Registers a menu item (top to bottom). Call before the menu is first shown.</summary>
        public void AddItem(string label, Action onClick)
        {
            pending.Add((label, onClick));
            built = false;
        }

        protected override void OnShown()
        {
            if (!built) Build();
            foreach (Item it in items)
            {
                it.hovered = false;
                it.t = 0f;
                Apply(it);
            }
        }

        private void Build()
        {
            built = true;
            items.Clear();
            for (int i = itemList.childCount - 1; i >= 0; i--) Destroy(itemList.GetChild(i).gameObject);

            foreach ((string label, Action action) in pending)
            {
                RectTransform row = UIFactory.Rect("Item_" + label, itemList);
                UIFactory.Layout(row, itemHeight, 520f);

                Image hit = UIFactory.Image(row, "Hit", new Color(0f, 0f, 0f, 0f), theme.white, true);
                UIFactory.Stretch(hit.rectTransform);
                var btn = row.gameObject.AddComponent<Button>();
                btn.targetGraphic = hit;
                btn.transition = Selectable.Transition.None;
                btn.navigation = new Navigation { mode = Navigation.Mode.None };
                row.gameObject.AddComponent<UIButtonSounds>();

                Image marker = UIFactory.Image(row, "Marker", Palette.Accent, theme.white);
                UIFactory.Place(marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(4f, 30f));

                TextMeshProUGUI text = UIFactory.Text(theme, row, "Label", label.ToUpperInvariant(), itemFontSize, Palette.TextMuted, TextAlignmentOptions.Left, TextWeight.Heading);
                text.characterSpacing = 12f;
                UIFactory.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(500f, itemHeight));

                var item = new Item { button = btn, label = text.rectTransform, text = text, marker = marker };
                var notify = row.gameObject.AddComponent<UIHoverNotify>();
                notify.Entered += () => item.hovered = true;
                notify.Exited += () => item.hovered = false;
                Action captured = action;
                btn.onClick.AddListener(() => captured?.Invoke());
                items.Add(item);
                Apply(item);
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                float target = it.hovered ? 1f : 0f;
                if (Mathf.Approximately(it.t, target)) continue;
                it.t = Mathf.MoveTowards(it.t, target, dt / 0.16f);
                Apply(it);
            }
        }

        private void Apply(Item it)
        {
            float e = it.t * it.t * (3f - 2f * it.t);
            it.label.anchoredPosition = new Vector2(e * hoverShift, 0f);
            it.text.color = Color.Lerp(Palette.TextMuted, Palette.Text, e);
            it.marker.color = Palette.Accent.WithAlpha(e);
            it.marker.rectTransform.sizeDelta = new Vector2(4f, Mathf.Lerp(8f, 30f, e));
        }
    }
}
