using System.Collections.Generic;
using MobaTrainer.Core;
using MobaTrainer.Input;
using MobaTrainer.Persistence;
using MobaTrainer.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MobaTrainer.Gameplay
{
    /// <summary>
    /// Minimap (60 s): split attention on a plain dark screen.
    /// <list type="bullet">
    /// <item>Prompts pop up anywhere on screen for 2 s: your Q / W / E keys (as bound in Settings) or a
    /// right-click target. Press the key / right-click the circle before it fades.</item>
    /// <item>At the same time, on the minimap (bottom right) red dots fall down two lanes. Your dot sits
    /// at the bottom: D moves it to the left lane, F to the right lane. Don't let a red dot touch it.</item>
    /// </list>
    /// Score = 100 per prompt hit − 40 per expired prompt − 20 per wrong press + 25 per dot dodged − 60 per dot hit.
    /// </summary>
    public class MinimapScenario : Scenario
    {
        private enum Kind { Key, RightClick }

        private class Prompt
        {
            public Kind kind;
            public GameAction action;
            public RectTransform rect;
            public Image body;
            public float age;
        }

        private class Dot
        {
            public RectTransform rect;
            public int lane;
            public float y;
        }

        [Header("UI")]
        public HUDController hud;

        [Header("Prompts")]
        public float promptLife = 2.5f;
        public Vector2 promptGap = new Vector2(1.1f, 0.65f);    // start → end of run
        public int maxPrompts = 3;
        public float promptSize = 76f;
        public float edgeMargin = 240f;

        [Header("Minimap lanes")]
        public float minimapSize = 300f;
        public Vector2 dotGap = new Vector2(2.6f, 1.6f);
        public Vector2 dotSpeed = new Vector2(55f, 90f);          // px per second

        public override string Id => "minimap";
        public override string Title => "Minimap";
        public override bool UsesPlayerControl => false;
        public override int Hits => record.hits;
        public override int Misses => record.missed;

        private static readonly GameAction[] KeyActions = { GameAction.AbilityQ, GameAction.AbilityW, GameAction.AbilityE };

        private readonly List<Prompt> prompts = new List<Prompt>();
        private readonly List<Dot> dots = new List<Dot>();
        private RectTransform root;
        private RectTransform map;
        private RectTransform me;
        private int myLane;
        private float promptTimer;
        private float dotTimer;
        private float reactionSum;
        private UITheme Theme => hud != null ? hud.theme : null;

        // Values for this run after the difficulty is applied (the serialized fields stay untouched).
        private float lifeNow;
        private Vector2 promptGapNow, dotGapNow, dotSpeedNow;
        private int maxNow = 3;

        private float LaneX(int lane) => lane == 0 ? -minimapSize * 0.22f : minimapSize * 0.22f;
        private float MyY => -minimapSize * 0.5f + 34f;

        protected override void OnBegin()
        {
            HideEnemiesFrom(0);
            // Difficulty: more prompts and dots (their life time and fall speed stay the same).
            lifeNow = promptLife;
            promptGapNow = promptGap * Sooner;
            dotGapNow = dotGap * Sooner;
            dotSpeedNow = dotSpeed;
            maxNow = Mathf.Clamp(maxPrompts + ExtraCount, 2, 5);
            BuildUi();
            promptTimer = 0.5f;
            dotTimer = 1.2f;
            reactionSum = 0f;
            myLane = 0;
        }

        protected override void OnTick(float dt)
        {
            float t = Time01;
            TickPrompts(dt, t);
            TickLanes(dt, t);
            HandleInput();
        }

        // ------------------------------------------------------------------ prompts

        private void TickPrompts(float dt, float t)
        {
            for (int i = prompts.Count - 1; i >= 0; i--)
            {
                Prompt p = prompts[i];
                p.age += dt;
                float k = Mathf.Clamp01(p.age / lifeNow);
                p.rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, k);
                Color c = p.body.color;
                c.a = Mathf.Lerp(1f, 0.4f, k);
                p.body.color = c;
                if (p.age >= lifeNow)
                {
                    record.missed++;
                    RemovePrompt(i);
                }
            }

            promptTimer -= dt;
            if (promptTimer <= 0f && prompts.Count < maxNow)
            {
                promptTimer = Mathf.Lerp(promptGapNow.x, promptGapNow.y, t) * Random.Range(0.8f, 1.2f);
                SpawnPrompt();
            }
        }

        private void SpawnPrompt()
        {
            bool key = Random.value < 0.6f;
            var p = new Prompt { kind = key ? Kind.Key : Kind.RightClick };
            p.body = UIFactory.Image(root, key ? "KeyPrompt" : "ClickPrompt", key ? Palette.Hex(0xD9A21B) : Palette.Hex(0x7B4FD6), Theme != null ? Theme.circle : null);
            p.rect = p.body.rectTransform;
            p.rect.anchorMin = p.rect.anchorMax = Vector2.zero;
            p.rect.sizeDelta = new Vector2(promptSize, promptSize);
            p.rect.anchoredPosition = RandomSpot();

            if (key)
            {
                p.action = KeyActions[Random.Range(0, KeyActions.Length)];
                string label = InputManager.Instance != null ? InputManager.Instance.BindingShort(p.action) : p.action.ToString();
                TextMeshProUGUI txt = UIFactory.Text(Theme, p.rect, "Key", label.ToUpperInvariant(), 34f, Color.white, TextAlignmentOptions.Center, TextWeight.Display);
                UIFactory.Stretch(txt.rectTransform, 6f, 6f, 6f, 6f);
                UIFactory.FitText(txt, 14f);
            }
            else
            {
                Image icon = UIFactory.Icon(Theme, p.rect, "Mouse", UIIcon.Mouse, 34f, Color.white);
                icon.rectTransform.anchoredPosition = new Vector2(0f, 8f);
                TextMeshProUGUI txt = UIFactory.Text(Theme, p.rect, "Right", "RIGHT", 11f, Color.white, TextAlignmentOptions.Center, TextWeight.Semibold);
                txt.characterSpacing = 4f;
                txt.rectTransform.sizeDelta = new Vector2(promptSize, 16f);
                txt.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            }
            prompts.Add(p);
        }

        /// <summary>Random point well inside the screen, away from the minimap and other prompts.</summary>
        private Vector2 RandomSpot()
        {
            Vector2 size = root.rect.size;
            if (size.x < 10f) size = new Vector2(1920f, 1080f);
            for (int i = 0; i < 30; i++)
            {
                var p = new Vector2(Random.Range(edgeMargin, size.x - edgeMargin), Random.Range(edgeMargin * 0.75f, size.y - edgeMargin * 0.75f));
                bool overMap = p.x > size.x - minimapSize - 140f && p.y < minimapSize + 160f;
                bool overlaps = false;
                foreach (Prompt o in prompts)
                {
                    if ((o.rect.anchoredPosition - p).sqrMagnitude < (promptSize * 1.6f) * (promptSize * 1.6f)) { overlaps = true; break; }
                }
                if (!overMap && !overlaps) return p;
            }
            return new Vector2(size.x * 0.5f, size.y * 0.55f);
        }

        private void RemovePrompt(int index)
        {
            if (prompts[index].rect != null) Destroy(prompts[index].rect.gameObject);
            prompts.RemoveAt(index);
        }

        // ------------------------------------------------------------------ minimap lanes

        private void TickLanes(float dt, float t)
        {
            float speed = Mathf.Lerp(dotSpeedNow.x, dotSpeedNow.y, t);
            for (int i = dots.Count - 1; i >= 0; i--)
            {
                Dot d = dots[i];
                d.y -= speed * dt;
                d.rect.anchoredPosition = new Vector2(LaneX(d.lane), d.y);
                if (d.lane == myLane && Mathf.Abs(d.y - MyY) < 24f)
                {
                    record.hitsTaken++;
                    FlashMe();
                    RemoveDot(i);
                }
                else if (d.y < MyY - 30f)
                {
                    record.dodged++;
                    RemoveDot(i);
                }
            }

            dotTimer -= dt;
            if (dotTimer <= 0f)
            {
                dotTimer = Mathf.Lerp(dotGapNow.x, dotGapNow.y, t) * Random.Range(0.85f, 1.15f);
                Image img = UIFactory.Image(map, "Enemy", Palette.Hex(0xE23A45), Theme != null ? Theme.circle : null);
                var d = new Dot { rect = img.rectTransform, lane = Random.value < 0.5f ? 0 : 1, y = minimapSize * 0.5f - 20f };
                d.rect.sizeDelta = new Vector2(26f, 26f);
                d.rect.anchoredPosition = new Vector2(LaneX(d.lane), d.y);
                dots.Add(d);
            }

            me.anchoredPosition = Vector2.Lerp(me.anchoredPosition, new Vector2(LaneX(myLane), MyY), 1f - Mathf.Exp(-25f * dt));
        }

        private float meFlash;

        private void FlashMe()
        {
            meFlash = 0.35f;
        }

        private void RemoveDot(int index)
        {
            if (dots[index].rect != null) Destroy(dots[index].rect.gameObject);
            dots.RemoveAt(index);
        }

        // ------------------------------------------------------------------ input

        private void HandleInput()
        {
            if (meFlash > 0f)
            {
                meFlash -= Time.deltaTime;
                me.GetComponent<Image>().color = meFlash > 0f ? Color.white : Palette.Hex(0x5BD14E);
            }

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.dKey.wasPressedThisFrame) myLane = 0;
                if (kb.fKey.wasPressedThisFrame) myLane = 1;
            }

            InputManager input = InputManager.Instance;
            if (input != null)
            {
                foreach (GameAction a in KeyActions)
                {
                    if (!input.Pressed(a)) continue;
                    int hit = -1;
                    for (int i = 0; i < prompts.Count; i++)
                    {
                        if (prompts[i].kind == Kind.Key && prompts[i].action == a) { hit = i; break; }
                    }
                    if (hit >= 0) Score(hit);
                    else record.casts++;   // wrong key
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                Vector2 pos = CursorService.Instance != null ? CursorService.Instance.ScreenPosition : mouse.position.ReadValue();
                int hit = -1;
                for (int i = prompts.Count - 1; i >= 0; i--)
                {
                    if (prompts[i].kind != Kind.RightClick) continue;
                    if (RectTransformUtility.RectangleContainsScreenPoint(prompts[i].rect, pos, null)) { hit = i; break; }
                }
                if (hit >= 0) Score(hit);
                else record.casts++;   // wrong click
            }
        }

        private void Score(int index)
        {
            record.hits++;
            reactionSum += prompts[index].age;
            RemovePrompt(index);
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            DestroyUi();
            Transform parent = hud != null ? hud.transform : transform;
            root = UIFactory.Stretch(UIFactory.Rect("MinimapDrill", parent));
            root.SetAsFirstSibling();   // under the HUD's counters

            // Plain dark backdrop: nothing else on screen.
            UIFactory.Stretch(UIFactory.Image(root, "Backdrop", Color.black, Theme != null ? Theme.white : null).rectTransform);

            Image frame = UIFactory.Rounded(Theme, root, "Minimap", Palette.BgElevated, 12f);
            RectTransform fr = frame.rectTransform;
            fr.anchorMin = fr.anchorMax = fr.pivot = new Vector2(1f, 0f);
            fr.sizeDelta = new Vector2(minimapSize + 20f, minimapSize + 20f);
            fr.anchoredPosition = new Vector2(-28f, 28f);
            UIFactory.Outline(Theme, fr, Palette.Accent.WithAlpha(0.45f), 12f);

            map = UIFactory.Rect("Map", fr);
            map.sizeDelta = new Vector2(minimapSize, minimapSize);
            map.anchoredPosition = Vector2.zero;

            for (int lane = 0; lane < 2; lane++)
            {
                Image strip = UIFactory.Rounded(Theme, map, lane == 0 ? "LaneD" : "LaneF", Palette.Hex(0x2A2A30), 10f);
                strip.rectTransform.sizeDelta = new Vector2(64f, minimapSize - 16f);
                strip.rectTransform.anchoredPosition = new Vector2(LaneX(lane), 0f);
                TextMeshProUGUI key = UIFactory.Text(Theme, map, "Key", lane == 0 ? "D" : "F", 18f, Palette.TextMuted, TextAlignmentOptions.Center, TextWeight.Display);
                key.rectTransform.sizeDelta = new Vector2(40f, 24f);
                key.rectTransform.anchoredPosition = new Vector2(LaneX(lane) + (lane == 0 ? -54f : 54f), MyY);
            }

            Image dot = UIFactory.Image(map, "You", Palette.Hex(0x5BD14E), Theme != null ? Theme.circle : null);
            me = dot.rectTransform;
            me.sizeDelta = new Vector2(30f, 30f);
            me.anchoredPosition = new Vector2(LaneX(0), MyY);

            TextMeshProUGUI note = UIFactory.Text(Theme, root, "Note", "MINIMAP  ·  D = LEFT LANE  ·  F = RIGHT LANE  ·  DODGE THE RED DOTS",
                Theme != null ? Theme.sizeCaption : 13f, Palette.TextMuted, TextAlignmentOptions.Right, TextWeight.Semibold);
            note.characterSpacing = 3f;
            RectTransform nr = note.rectTransform;
            nr.anchorMin = nr.anchorMax = nr.pivot = new Vector2(1f, 0f);
            nr.sizeDelta = new Vector2(minimapSize + 220f, 22f);
            nr.anchoredPosition = new Vector2(-28f, minimapSize + 58f);
            UIFactory.FitText(note, 9f);

            TextMeshProUGUI help = UIFactory.Text(Theme, root, "Help", "PRESS THE KEY  ·  RIGHT-CLICK THE PURPLE CIRCLES",
                Theme != null ? Theme.sizeCaption : 13f, Palette.TextFaint, TextAlignmentOptions.Center, TextWeight.Semibold);
            help.characterSpacing = 4f;
            RectTransform hr = help.rectTransform;
            hr.anchorMin = hr.anchorMax = hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(900f, 22f);
            hr.anchoredPosition = new Vector2(0f, -90f);
        }

        private void DestroyUi()
        {
            prompts.Clear();
            dots.Clear();
            if (root != null) Destroy(root.gameObject);
            root = null;
        }

        protected override void FillRecord(RunRecord r)
        {
            r.reaction = r.hits > 0 ? reactionSum / r.hits * 1000f : 0f;
            r.score = Mathf.Max(0, r.hits * 100 - r.missed * 40 - r.casts * 20 + r.dodged * 25 - r.hitsTaken * 60);
            r.won = true;
            foreach (Prompt p in prompts) if (p.rect != null) Destroy(p.rect.gameObject);
            prompts.Clear();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            DestroyUi();
        }
    }
}
