using System;
using System.Collections.Generic;
using System.Text;
using MobaTrainer.Core;
using MobaTrainer.Flow;
using MobaTrainer.Gameplay;
using MobaTrainer.Input;
using MobaTrainer.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MobaTrainer.ExtremeSurvivor
{
    /// <summary>
    /// Extreme Survivor's own overlay (built at runtime, only while the mode runs): 4 item slots with
    /// cooldowns, a stats line, next-item timer, boss bar, announcements, and the item offer screen
    /// (3 cards + a Random card) that pauses the game.
    /// </summary>
    public class SurvivorUI : MonoBehaviour
    {
        public struct Card
        {
            public SurvivorItem item;    // null = Random card
        }

        private UITheme theme;
        private Canvas canvas;
        private RectTransform hudRoot;
        private RectTransform offerRoot;
        private RectTransform cardRow;
        private TextMeshProUGUI statsText;
        private TextMeshProUGUI nextText;
        private TextMeshProUGUI announceText;
        private RectTransform bossRoot;
        private Image bossFill;
        private TextMeshProUGUI bossLabel;
        private float announceTimer;

        private class SlotView
        {
            public Image frame, fill, cooldown;
            public TextMeshProUGUI glyph, timer, note, key;
            public Image icon;
            public OwnedItem shown;
            public bool dirty = true;     // redraw even when the slot became empty
        }
        private readonly List<SlotView> slots = new List<SlotView>();
        private Image tooltip, ghost, ghostIcon;
        private TextMeshProUGUI tooltipText;
        private RectTransform itemBar;
        private SurvivorItemSystem current;
        private int hoverSlot = -1;
        private int dragFrom = -1;

        private Action<int> onPick;
        private PlayerController player;
        private GameFlowController flow;
        public bool OfferOpen { get; private set; }

        public static SurvivorUI Create(PlayerController player, int slotCount)
        {
            var go = new GameObject("ExtremeSurvivorUI", typeof(RectTransform));
            var ui = go.AddComponent<SurvivorUI>();
            ui.player = player;
            ui.Build(slotCount);
            return ui;
        }

        private void Build(int slotCount)
        {
            HUDController hud = FindFirstObjectByType<HUDController>(FindObjectsInactive.Include);
            theme = hud != null ? hud.theme : null;
            flow = FindFirstObjectByType<GameFlowController>(FindObjectsInactive.Include);

            gameObject.layer = LayerMask.NameToLayer("UI");
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            hudRoot = UIFactory.Stretch(UIFactory.Rect("HUD", transform));

            // ---- item slots (bottom right)
            RectTransform bar = UIFactory.Rect("Items", hudRoot);
            float size = 64f, gap = 10f;
            UIFactory.Place(bar, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-40f, 40f), new Vector2(slotCount * size + (slotCount - 1) * gap, size));
            for (int i = 0; i < slotCount; i++)
            {
                var v = new SlotView();
                v.frame = UIFactory.Rounded(theme, bar, "Slot" + i, Palette.Surface.WithAlpha(0.85f), 10f, true);
                UIFactory.Place(v.frame.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(i * (size + gap), 0f), new Vector2(size, size));
                UIFactory.Outline(theme, v.frame.transform, Palette.Border, 10f);
                v.fill = UIFactory.Rounded(theme, v.frame.transform, "Fill", Color.clear, 8f);
                UIFactory.Stretch(v.fill.rectTransform, 5f, 5f, 5f, 5f);
                v.icon = UIFactory.Image(v.fill.transform, "Icon", Color.white);
                UIFactory.Stretch(v.icon.rectTransform, 4f, 4f, 4f, 4f);
                v.icon.preserveAspect = true;
                v.icon.enabled = false;
                v.glyph = UIFactory.Text(theme, v.fill.transform, "Glyph", "", 22f, Color.white, TextAlignmentOptions.Center, TextWeight.Display);
                UIFactory.Stretch(v.glyph.rectTransform);
                v.cooldown = UIFactory.Image(v.fill.transform, "Cooldown", new Color(0f, 0f, 0f, 0.7f), theme != null ? theme.white : null);
                UIFactory.Stretch(v.cooldown.rectTransform);
                v.cooldown.type = Image.Type.Filled;
                v.cooldown.fillMethod = Image.FillMethod.Radial360;
                v.cooldown.fillOrigin = (int)Image.Origin360.Top;
                v.cooldown.fillClockwise = false;
                v.cooldown.fillAmount = 0f;
                v.timer = UIFactory.Text(theme, v.fill.transform, "Timer", "", 20f, Color.white, TextAlignmentOptions.Center, TextWeight.Semibold);
                UIFactory.Stretch(v.timer.rectTransform);
                v.note = UIFactory.Text(theme, v.frame.transform, "Note", "", 12f, Palette.Highlight, TextAlignmentOptions.BottomLeft, TextWeight.Semibold);
                UIFactory.Stretch(v.note.rectTransform, 6f, 4f, 0f, 3f);
                // Key label (top right, small), like a MOBA item bar.
                Image keyBg = UIFactory.Rounded(theme, v.frame.transform, "KeyBg", new Color(0f, 0f, 0f, 0.75f), 4f);
                UIFactory.Place(keyBg.rectTransform, UIFactory.TopRight, UIFactory.TopRight, new Vector2(2f, 2f), new Vector2(18f, 16f));
                v.key = UIFactory.Text(theme, keyBg.transform, "Key", (i + 1).ToString(), 11f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Semibold);
                UIFactory.Stretch(v.key.rectTransform);
                var handle = v.frame.gameObject.AddComponent<SurvivorSlotHandle>();
                handle.ui = this;
                handle.index = i;
                slots.Add(v);
            }

            // Hover tooltip + drag ghost. The tooltip lives on the root (not the HUD) so it can also
            // show above the item offer screen; it never takes the pointer itself.
            tooltip = UIFactory.Rounded(theme, transform, "Tooltip", Palette.BgElevated.WithAlpha(0.96f), 10f);
            var tooltipGroup = tooltip.gameObject.AddComponent<CanvasGroup>();
            tooltipGroup.blocksRaycasts = false;
            tooltipGroup.interactable = false;
            UIFactory.Outline(theme, tooltip.transform, Palette.BorderStrong, 10f);
            tooltip.rectTransform.pivot = new Vector2(1f, 0f);
            tooltip.rectTransform.anchorMin = tooltip.rectTransform.anchorMax = new Vector2(1f, 0f);
            tooltipText = UIFactory.Text(theme, tooltip.transform, "Text", "", 15f, Palette.TextMuted, TextAlignmentOptions.TopLeft, TextWeight.Medium);
            tooltipText.textWrappingMode = TextWrappingModes.Normal;
            tooltipText.richText = true;
            UIFactory.Stretch(tooltipText.rectTransform, 16f, 16f, 14f, 14f);
            tooltip.gameObject.SetActive(false);
            ghost = UIFactory.Rounded(theme, transform, "DragGhost", Color.white, 10f);
            ghost.rectTransform.sizeDelta = new Vector2(64f, 64f);
            ghostIcon = UIFactory.Image(ghost.transform, "Icon", Color.white);
            UIFactory.Stretch(ghostIcon.rectTransform, 5f, 5f, 5f, 5f);
            var ghostGroup = ghost.gameObject.AddComponent<CanvasGroup>();
            ghostGroup.blocksRaycasts = false;
            ghostGroup.alpha = 0.85f;
            ghost.gameObject.SetActive(false);
            itemBar = bar;

            statsText = UIFactory.Text(theme, hudRoot, "Stats", "", 15f, Palette.TextMuted, TextAlignmentOptions.BottomRight, TextWeight.Medium);
            UIFactory.Place(statsText.rectTransform, UIFactory.BottomRight, UIFactory.BottomRight, new Vector2(-40f, 116f), new Vector2(700f, 24f));

            nextText = UIFactory.Text(theme, hudRoot, "Next", "", 15f, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Semibold);
            nextText.characterSpacing = 4f;
            UIFactory.Place(nextText.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -112f), new Vector2(600f, 24f));

            announceText = UIFactory.Text(theme, hudRoot, "Announce", "", 40f, Palette.Highlight, TextAlignmentOptions.Center, TextWeight.Display);
            UIFactory.Place(announceText.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 220f), new Vector2(1400f, 60f));

            // ---- boss bar (top centre)
            bossRoot = UIFactory.Rect("Boss", hudRoot);
            UIFactory.Place(bossRoot, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -150f), new Vector2(720f, 44f));
            bossLabel = UIFactory.Text(theme, bossRoot, "Label", "", 16f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Heading);
            UIFactory.Place(bossLabel.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, Vector2.zero, new Vector2(720f, 20f));
            Image track = UIFactory.Rounded(theme, bossRoot, "Track", Palette.Surface.WithAlpha(0.9f), 6f);
            UIFactory.Place(track.rectTransform, UIFactory.BottomCenter, UIFactory.BottomCenter, Vector2.zero, new Vector2(720f, 16f));
            bossFill = UIFactory.Image(track.transform, "Fill", Palette.Accent, theme != null ? theme.white : null);
            UIFactory.Stretch(bossFill.rectTransform, 2f, 2f, 2f, 2f);
            bossFill.type = Image.Type.Filled;
            bossFill.fillMethod = Image.FillMethod.Horizontal;
            bossRoot.gameObject.SetActive(false);

            // ---- item offer
            offerRoot = UIFactory.Stretch(UIFactory.Rect("Offer", transform));
            Image dim = UIFactory.Image(offerRoot, "Dim", Palette.Overlay, theme != null ? theme.white : null, true);
            UIFactory.Stretch(dim.rectTransform);
            TextMeshProUGUI title = UIFactory.Text(theme, offerRoot, "Title", "CHOOSE AN ITEM", 44f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Display);
            UIFactory.Place(title.rectTransform, UIFactory.Center, UIFactory.Center, new Vector2(0f, 330f), new Vector2(1200f, 60f));
            cardRow = UIFactory.Rect("Cards", offerRoot);
            UIFactory.Place(cardRow, UIFactory.Center, UIFactory.Center, new Vector2(0f, -20f), new Vector2(1320f, 470f));
            offerRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ HUD

        public void Announce(string text, float seconds = 2.5f)
        {
            announceText.text = text;
            announceTimer = seconds;
        }

        public void SetNext(string text) => nextText.text = text;

        public void SetBoss(bool show, float health01, string label)
        {
            if (bossRoot.gameObject.activeSelf != show) bossRoot.gameObject.SetActive(show);
            if (!show) return;
            bossFill.fillAmount = Mathf.Clamp01(health01);
            bossLabel.text = label;
        }

        public void Refresh(SurvivorItemSystem items)
        {
            current = items;
            InputManager input = InputManager.Instance;
            for (int i = 0; i < slots.Count; i++)
            {
                SlotView v = slots[i];
                OwnedItem o = i < items.slots.Length ? items.slots[i] : null;
                if (input != null && i < 5) v.key.text = input.BindingShort(GameAction.Item1 + i);
                if (o != v.shown || v.dirty)
                {
                    v.shown = o;
                    v.dirty = false;
                    bool has = o != null;
                    v.fill.color = has ? (o.item.icon != null ? Color.white : o.item.color.WithAlpha(0.85f)) : Color.clear;
                    v.icon.enabled = false;
                    if (has && o.item.icon != null) v.fill.sprite = o.item.icon; else if (theme != null) v.fill.sprite = theme.rounded;
                    v.fill.type = has && o.item.icon != null ? Image.Type.Simple : Image.Type.Sliced;
                    v.glyph.text = has && o.item.icon == null ? o.item.glyph : "";
                }
                if (o == null)
                {
                    v.cooldown.fillAmount = 0f;
                    v.timer.text = "";
                    v.note.text = "";
                    continue;
                }
                float left = items.SlotCooldownLeft(o.item, out float total);
                v.cooldown.fillAmount = left > 0f ? left / total : 0f;
                v.timer.text = left > 0f ? (left < 10f ? left.ToString("0.0") : Mathf.CeilToInt(left).ToString()) : "";
                v.note.text = o.item.HasActive && left <= 0f ? "USE" : items.SlotNote(o.item);
            }
            statsText.text = $"AD {Mathf.RoundToInt(items.AD)}   AP {Mathf.RoundToInt(items.AP)}   HP {Mathf.RoundToInt(items.MaxHealth)}   " +
                             $"MS {items.MoveSpeed * 100f:0}   AS {items.AttackSpeed:0.00}   CDR {Mathf.RoundToInt(items.CDR * 100f)}%";
            if (hoverSlot >= 0 && dragFrom < 0 && !OfferOpen) ShowTooltip(hoverSlot);
        }

        // ------------------------------------------------------------------ slot hover / drag

        internal void HoverSlot(int index)
        {
            hoverSlot = index;
            if (OfferOpen) return;   // the offer screen owns the tooltip (card hover)
            if (index < 0 || dragFrom >= 0) { tooltip.gameObject.SetActive(false); return; }
            ShowTooltip(index);
        }

        private void ShowTooltip(int index)
        {
            OwnedItem o = current != null && index < current.slots.Length ? current.slots[index] : null;
            if (o == null) { tooltip.gameObject.SetActive(false); return; }
            tooltipText.text = Describe(o.item, o.statMult, current);
            var slotRt = slots[index].frame.rectTransform;
            float x = itemBar.anchoredPosition.x - (itemBar.rect.width - slotRt.anchoredPosition.x - slotRt.rect.width);
            RectTransform tr = tooltip.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(1f, 0f);
            tr.pivot = new Vector2(1f, 0f);
            tr.anchoredPosition = new Vector2(Mathf.Min(-40f, x), 40f + 64f + 46f);
            SizeTooltip();
            tooltip.transform.SetAsLastSibling();
            tooltip.gameObject.SetActive(true);
        }

        private const float TooltipWidth = 380f;

        private float SizeTooltip()
        {
            Vector2 pref = tooltipText.GetPreferredValues(tooltipText.text, TooltipWidth - 32f, 0f);
            float h = pref.y + 30f;
            tooltip.rectTransform.sizeDelta = new Vector2(TooltipWidth, h);
            return h;
        }

        /// <summary>Hover on an offer card: full item details beside the card (null hides it).</summary>
        internal void HoverCard(SurvivorItem item, RectTransform card)
        {
            if (item == null || card == null || !OfferOpen) { tooltip.gameObject.SetActive(false); return; }
            tooltipText.text = Describe(item, 1f, null);
            float h = SizeTooltip();

            var root = (RectTransform)transform;
            Vector3[] c = new Vector3[4];
            card.GetWorldCorners(c);    // 0 = bottom left, 2 = top right (screen space overlay: world = screen)
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, c[0], null, out Vector2 bl);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, c[2], null, out Vector2 trc);
            Rect area = root.rect;
            const float gap = 14f;

            // Right of the card if it fits, otherwise left of it.
            bool right = trc.x + gap + TooltipWidth <= area.xMax - 10f;
            float px = right ? trc.x + gap : bl.x - gap;
            float py = Mathf.Clamp(trc.y, area.yMin + h + 10f, area.yMax - 10f);

            RectTransform t = tooltip.rectTransform;
            t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
            t.pivot = new Vector2(right ? 0f : 1f, 1f);
            t.anchoredPosition = new Vector2(px, py);
            tooltip.transform.SetAsLastSibling();
            tooltip.gameObject.SetActive(true);
        }

        /// <summary>Name, group, stats, passive, active and cooldowns of an item (cards and tooltips).</summary>
        private static string Describe(SurvivorItem it, float mult, SurvivorItemSystem items)
        {
            var sb = new StringBuilder();
            sb.Append("<size=20><color=#F2F2F4><b>").Append(it.displayName).Append("</b></color></size>");
            if (it.group != ItemGroup.None) sb.Append("   <size=12><color=#F5C451>").Append(it.GroupLabel).Append("</color></size>");
            if (mult > 1.001f) sb.Append("   <size=12><color=#F5C451>+").Append(Mathf.RoundToInt((mult - 1f) * 100f)).Append("% (Random)</color></size>");
            sb.Append('\n');
            foreach (string line in it.StatLines(mult)) sb.Append("<color=#F2F2F4>").Append(line).Append("</color>\n");
            if (!string.IsNullOrEmpty(it.passiveText)) sb.Append('\n').Append(it.passiveText).Append('\n');
            if (it.HasActive) sb.Append('\n').Append("<color=#F5C451>").Append(it.activeText).Append("</color>\n");
            if (it.cooldown > 0f)
            {
                sb.Append("\n<color=#55555C>Passive cooldown ").Append(it.cooldown.ToString("0.#")).Append(" s");
                if (it.group != ItemGroup.None) sb.Append(" (shared by group)");
                sb.Append("</color>");
            }
            if (items != null)
            {
                float left = items.SlotCooldownLeft(it, out _);
                if (left > 0f) sb.Append("\n<color=#FF4D5A>Ready in ").Append(left.ToString("0.0")).Append(" s</color>");
            }
            return sb.ToString().TrimEnd('\n');
        }

        internal void BeginDrag(int index)
        {
            if (current == null || index >= current.slots.Length || current.slots[index] == null) return;
            dragFrom = index;
            SurvivorItem it = current.slots[index].item;
            ghost.color = it.icon != null ? Color.white : it.color;
            ghostIcon.sprite = it.icon;
            ghostIcon.enabled = it.icon != null;
            if (it.icon != null) ghost.color = new Color(0f, 0f, 0f, 0f);
            ghost.gameObject.SetActive(true);
            tooltip.gameObject.SetActive(false);
        }

        internal void Drag(Vector2 screen)
        {
            if (dragFrom < 0) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out Vector2 local);
            ghost.rectTransform.anchorMin = ghost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            ghost.rectTransform.anchoredPosition = local;
        }

        internal void Drop(int index)
        {
            if (dragFrom < 0 || current == null) return;
            current.Swap(dragFrom, index);
            foreach (SlotView v in slots) v.dirty = true;   // force a redraw (the old slot may now be empty)
        }

        internal void EndDrag()
        {
            dragFrom = -1;
            ghost.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ offer

        public void ShowOffer(List<Card> cards, float randomBonus, Action<int> pick)
        {
            onPick = pick;
            hoverSlot = -1;
            tooltip.gameObject.SetActive(false);
            for (int i = cardRow.childCount - 1; i >= 0; i--) Destroy(cardRow.GetChild(i).gameObject);
            float w = 300f, gap = 24f;
            float total = cards.Count * w + (cards.Count - 1) * gap;
            for (int i = 0; i < cards.Count; i++)
            {
                int index = i;
                Button b = BuildCard(cards[i], randomBonus);
                var rt = (RectTransform)b.transform;
                UIFactory.Place(rt, UIFactory.Center, UIFactory.Center, new Vector2(-total * 0.5f + w * 0.5f + i * (w + gap), 0f), new Vector2(w, 460f));
                b.onClick.AddListener(() => Pick(index));
                if (cards[i].item != null)
                {
                    var hover = b.gameObject.AddComponent<SurvivorCardHandle>();
                    hover.ui = this;
                    hover.item = cards[i].item;
                }
            }
            offerRoot.gameObject.SetActive(true);
            OfferOpen = true;
            Hold(true);
        }

        private void Pick(int index)
        {
            if (!OfferOpen) return;
            OfferOpen = false;
            offerRoot.gameObject.SetActive(false);
            tooltip.gameObject.SetActive(false);
            Hold(false);
            onPick?.Invoke(index);
        }

        /// <summary>Closes the offer without picking (run ended / quit).</summary>
        public void CloseOffer()
        {
            if (!OfferOpen) return;
            OfferOpen = false;
            offerRoot.gameObject.SetActive(false);
            tooltip.gameObject.SetActive(false);
            Hold(false);
        }

        private void Hold(bool on)
        {
            Time.timeScale = on ? 0f : 1f;
            if (player != null)
            {
                player.InputEnabled = !on;
                if (on) { player.Abilities.CancelAiming(); player.Abilities.CancelCharge(); }
            }
        }

        private Button BuildCard(Card card, float randomBonus)
        {
            SurvivorItem it = card.item;
            bool random = it == null;
            Button b = UIFactory.Button(theme, cardRow, random ? "Card_Random" : "Card_" + it.id, "", ButtonStyle.Secondary, new Vector2(300f, 460f));
            Transform t = b.transform;
            Color accent = random ? Palette.Highlight : it.color;

            Image icon = UIFactory.Rounded(theme, t, "Icon", accent.WithAlpha(0.9f), 14f);
            UIFactory.Place(icon.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -24f), new Vector2(88f, 88f));
            if (!random && it.icon != null)
            {
                icon.color = Color.clear;   // the icon art has its own tile
                Image spr = UIFactory.Image(icon.transform, "Sprite", Color.white, it.icon);
                UIFactory.Stretch(spr.rectTransform);
                spr.preserveAspect = true;
            }
            else
            {
                TextMeshProUGUI g = UIFactory.Text(theme, icon.transform, "Glyph", random ? "?" : it.glyph, random ? 48f : 30f, Color.white, TextAlignmentOptions.Center, TextWeight.Display);
                UIFactory.Stretch(g.rectTransform);
            }

            TextMeshProUGUI name = UIFactory.Text(theme, t, "Name", random ? "Random" : it.displayName, 24f, Palette.Text, TextAlignmentOptions.Center, TextWeight.Heading);
            UIFactory.Place(name.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -124f), new Vector2(276f, 32f));
            UIFactory.FitText(name, 16f);

            string groupLine = random ? "GAMBLE" : (it.group == ItemGroup.None ? "" : it.GroupLabel);
            TextMeshProUGUI grp = UIFactory.Text(theme, t, "Group", groupLine, 13f, accent, TextAlignmentOptions.Center, TextWeight.Semibold);
            grp.characterSpacing = 6f;
            UIFactory.Place(grp.rectTransform, UIFactory.TopCenter, UIFactory.TopCenter, new Vector2(0f, -158f), new Vector2(276f, 20f));

            var sb = new StringBuilder();
            if (random)
            {
                sb.Append("<color=#F2F2F4>A random item you don't own.</color>\n\n");
                sb.Append($"<color=#F5C451>Its stats are +{Mathf.RoundToInt((randomBonus - 1f) * 100f)}% higher.</color>");
            }
            else
            {
                foreach (string line in it.StatLines()) sb.Append("<color=#F2F2F4>").Append(line).Append("</color>\n");
                if (!string.IsNullOrEmpty(it.passiveText)) sb.Append('\n').Append(it.passiveText);
                if (it.HasActive) sb.Append("\n\n<color=#F5C451>").Append(it.activeText).Append("</color>");
                if (it.cooldown > 0f)
                {
                    sb.Append("\n\n<color=#55555C>Cooldown ").Append(it.cooldown.ToString("0.#")).Append(" s");
                    if (it.group != ItemGroup.None) sb.Append(" (shared by group)");
                    sb.Append("</color>");
                }
            }
            TextMeshProUGUI body = UIFactory.Text(theme, t, "Body", sb.ToString(), 15f, Palette.TextMuted, TextAlignmentOptions.Top, TextWeight.Medium);
            body.textWrappingMode = TextWrappingModes.Normal;
            UIFactory.Stretch(body.rectTransform, 20f, 20f, 190f, 18f);
            UIFactory.FitText(body, 11f);
            return b;
        }

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            if (announceTimer > 0f)
            {
                announceTimer -= Time.unscaledDeltaTime;
                Color c = announceText.color;
                c.a = Mathf.Clamp01(announceTimer / 0.5f);
                announceText.color = c;
                if (announceTimer <= 0f) announceText.text = "";
            }

            // The pause menu / results sit under this canvas: hide everything while they are open.
            // Esc: the item bar stays up (hover for details, drag to rearrange); only results hide it.
            bool paused = flow != null && flow.CurrentState == GameFlowController.State.Paused;
            bool results = flow != null && flow.CurrentState == GameFlowController.State.Results;
            if (hudRoot.gameObject.activeSelf == results) hudRoot.gameObject.SetActive(!results);
            if (results && tooltip.gameObject.activeSelf) tooltip.gameObject.SetActive(false);
            if (!OfferOpen) return;
            if (offerRoot.gameObject.activeSelf == paused)
            {
                offerRoot.gameObject.SetActive(!paused);
                if (paused) tooltip.gameObject.SetActive(false);
            }
            if (!paused)
            {
                if (Time.timeScale != 0f) Time.timeScale = 0f;
                if (player != null && player.InputEnabled) player.InputEnabled = false;
            }
        }

        private void OnDestroy()
        {
            if (OfferOpen)
            {
                Time.timeScale = 1f;
                if (player != null) player.InputEnabled = true;
            }
        }
    }
}
