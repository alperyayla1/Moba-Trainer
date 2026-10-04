# DESIGN_NOTES — Reference: Blindfold Chess (`denizcos/chess-master-1`)

Analyzed commit `7885cb7` ("keyboard satisfaction mode", 2026-09-18). The reference clone is read-only and nothing in it was modified.
Every value below comes from the project files (scene YAML, prefabs, `.meta`, ProjectSettings, scripts). Sprite colors were sampled from the PNGs, using the median of the opaque pixels in the button face.

---

## 1. Engine & packages

| Item | Reference (Blindfold Chess) | New project (as created) |
|---|---|---|
| Unity | **6000.0.58f1** (Unity 6.0 LTS) | **6000.5.4f1** ⚠ mismatch, see Open Questions |
| Render pipeline | URP 17.0.4, **2D Renderer** (`Renderer2D.asset`, Global Light 2D) | URP 17.5.0, Universal (3D) renderer, PC + Mobile assets |
| Camera | Orthographic, size 5, Solid Color clear `#000000` (α 0) | n/a |
| Input handling | `activeInputHandler: 2` (**Both**). Input System 1.14.2 is installed but **unused**. All code calls legacy `Input.GetKeyDown(KeyCode…)` | Input System 1.19.0 |
| UI | UGUI 2.0 + **TextMeshPro** (no UI Toolkit anywhere) | UGUI 2.5.0 |
| Other packages | Netcode for GameObjects 2.4.4, Lobby 1.2.2, Relay 1.0.5, Timeline, Visual Scripting, 2D feature set, Test Framework | AI Navigation 2.0.13, Timeline, Test Framework |
| Post-processing | No Volume in the scene (DefaultVolumeProfile is the untouched URP default) | — |
| Player settings | Company **Marmotsoft**, 1920×1080, Fullscreen Window, not resizable, vSync on in the higher quality tiers | — |

## 2. UI approach

- **One Screen-Space Overlay Canvas** with a **CanvasScaler** set to Scale With Screen Size at **1920×1080**, match 0.5.
- Every screen is a **sibling panel under `Canvas`**. Screens are switched with `SetActive`: enable the mode's objects plus its panel, and disable the menu panel.
- The UI is **sprite-driven, not color-driven**. Almost every `Image` has color `#FFFFFF`, and the look comes from the sprites.
- Button `Selectable` transition: **Sprite Swap**, fade duration **0.1 s**, with Normal, Hover and Pressed sprites.

### Fonts
| Font | Use | Count |
|---|---|---|
| **Vinque Rg SDF** (Vinque, by Typodermic) | Primary font for everything: titles, buttons, scores, inputs, logs | 79 texts |
| LiberationSans SDF (TMP default) | Only leftover default input fields (password/code) and the big flash text | 10 texts |

All text is **Regular**, color **white**. No bold or italic, except italic placeholders in the leftover default fields.

**Type scale (pt at 1080p):** 300 (full-screen flash overlay, 49% α) · **100** (big score / target) · 60 (prompt) · **50** (text inputs) · 42 (menu tiles) · **36** (body, status, logs, headers) · **24** (button labels, settings) · 20 / 14 (small inputs).

### Color palette (exact hex)
**Backgrounds and overlays**
| Role | Hex | Alpha |
|---|---|---|
| Camera / base background | `#000000` | 1 |
| Title backdrop (radial vignette) | `#000000` edges to `#313131` center | 1 |
| Mode panel dim | `#000000` | 0.39 |
| Board panel wash | `#FFFFFF` | 0.39 |
| Settings panel (dark maroon glass) | `#432222` | 0.39 |
| Log / scroll view | `#151212` | 0.39 |
| Alt dark glass | `#2F2A2A` | 0.39 |
| Neutral greys used on solids | `#4D4D4D`, `#505050`, `#414141`, `#323232` | 1 |

**Accents** (from the button sprites and solid images)
| Role | Normal | Hover | Pressed |
|---|---|---|---|
| **Violet** (primary action: New Game, dropdowns) | `#6C1B71` | `#9D26A4` | `#48114C` |
| **Crimson** (destructive or secondary: Resign, Reveal, Close, Quit) | `#701B1B` | `#A52626` | `#4B1212` |
| Disabled | `#5D5D5D` | — | — |
| Dropdown list bg (solid) | `#601666` | | |
| Solid crimson accents | `#631414`, `#681718` | | |
| Unused pack colors (Blue/Green/Yellow) | `#1B3070` / `#467319` / `#716E1B` | | |

**Text**
| Role | Hex | Alpha |
|---|---|---|
| Primary | `#FFFFFF` | 1.0 |
| Secondary (menu mode list) | `#FFFFFF` | 0.65 |
| Hint / placeholder | `#FFFFFF` | 0.35–0.49 |
| Off-white | `#E9E4E4` | 1.0 |

**Feedback** (in code): `Color.green` for correct and `Color.red` for wrong (raw Unity colors), and a yellow highlight `new Color(1, .85, .2)` = `#FFD933`.

Overall identity: **black, vignetted, gothic serif, grungy beveled violet and crimson bars, translucent maroon glass panels, white text.**

### Button and panel style
- Buttons use the **"RPG Button 7"** Asset Store pack. Sources are 660×140 textured, beveled "grunge" bars in 5 colors plus Disabled plus a Border element. The pack's readme recommends Vinque.
- Typical in-game size **~251×85**, label 24 pt Vinque white, centered. The legacy menu tiles used 42 pt labels.
- Menu or exit button: a white outline "exit door" icon (`Icon_Exit.png`, 250 px) in the top-left of every mode panel.
- Panels: flat, sprite-less `Image` with a dark translucent tint (39% α). There are no rounded corners, borders, shadows or layout groups on the menus. Elements are placed with absolute anchors, usually top-left.
- The Settings panel has a transparent drag header (`DraggablePanel`), so the window can be moved.

### Animations and transitions
- **No tweening library** (no DOTween or LeanTween) and **no Animators** on UI. Screen changes are **instant** `SetActive` swaps.
- Feel comes from **timed coroutines** rather than motion:
  - flash text (300 pt, 49% α) that shows for 1.2 s,
  - a blinking target square (`blinkInterval`),
  - correct/wrong square color that resets after a delay,
  - a board reveal for N seconds.
- `MusicManager.fadeDuration = 2` is declared but **never used**, so there are no music fades.

### Audio feedback
- **`UIButtonHoverSound`** is a singleton on the `UIAudio` prefab with DontDestroyOnLoad. It is the central SFX player and uses `PlayOneShot` on one AudioSource.
  - Hover (via `EventTrigger` or `HoverPlaySound`) plays a **mechanical-keyboard clack** sample (`hoverClip1`).
  - Click (`Button.onClick`, wired in the inspector) plays `click.wav`.
  - Switch / toggle plays `Clack_Minimal UI Sounds.wav`.
  - Typing plays one of 15 random clack samples at volume **0.45**, with separate delete and enter sounds (`KeyboardClackSoundRandom`).
  - The notification sound has its own AudioSource that ignores listener pause.
  - `AudioListener.pause` is set when the window loses focus.
- **`MusicManager`**: one title track, then a **shuffled dark-ambient playlist** (7 tracks in `MUSICAUDIO/`, Pixabay IDs), reshuffled after each loop.
- Settings: SFX toggle, Music toggle, Music volume (default **0.45**), Quit. Stored in **PlayerPrefs** (`GeneralSound`, `Music`, `MasterVolume`). **Esc** toggles the settings panel.
- Custom cursor: a pixel-art gauntlet (`Cursors v0.02/2665113.6_image.png`), applied with `CursorMode.ForceSoftware`. It is re-applied on focus and on scene change.

## 3. Menu flow and scene structure

```
SampleScene (the only scene in the build)
└─ Canvas (Overlay, 1920×1080)
   ├─ MainMenuPanelGOD      ← ACTIVE main menu
   │    title image (BLINDFOLD CHESS, glowing serif on a black vignette)
   │    mode list text (36pt, 65% white):  "a1: Coordinate Training / b2: … / e5: AI Battle / f6: Online"
   │    always-focused TMP input (50pt) → type the code + Enter → mode starts
   ├─ MainMenuPanelOld      ← legacy image-tile button menu (kept, inactive)
   ├─ CoordinateTrainPanel / CoordinateTraining2Panel / ColorModePanel / KnightModePanel
   ├─ BlindfoldModePanel (vs AI) / MultiplayerModePanel (lobby → room → battle)
   └─ Settings Panel (Esc, draggable, maroon glass)
Scene roots outside the Canvas: Main Camera, Global Light 2D, EventSystem, mode manager GameObjects
```

- **Signature interaction:** the main menu is **keyboard-first**. You type a short code instead of clicking, with an input field that is always focused (`AlwaysShowPlaceholder`) and keyboard clack SFX on every keystroke.
- Returning to the menu: the exit icon calls `CanvasUIManager.ReturnToMainMenu()`, which disables every Canvas child and non-essential scene root, then re-enables the menu objects.

## 4. Folders and code conventions

- **Flat** `Assets/Scripts/` holds all 31 scripts. There are **no namespaces and no asmdefs**. Editor tools live in `Assets/Editor/`.
- Asset folders are loosely named: `buttonss/`, `MUSICAUDIO/`, `Audio/`, `ChessAssets/`, and the vendor folders `RPG Button 7/` and `Cursors v0.02/`.
- File name ≠ class name in several places:

  | File | Class |
  |---|---|
  | `MainMenu.cs` | `MainMenuController` |
  | `MenuButton.cs` | `CanvasUIManager` |
  | `Hover.cs` | `HoverPlaySound` |
  | `Clacking.cs` | `KeyboardClackSoundRandom` |
  | `leScript.cs` | `LockBoardRect` |
  | `AutoFit.cs` | `ResponsiveGrid8x8` |

- **Naming patterns:** `XxxManager` for systems, `XxxMode` / `XxxModeManager` for game modes, `XxxUI` for mode views. PascalCase methods, camelCase fields.
- **Style:**
  - public serialized fields grouped with `[Header]` and `[Tooltip]`,
  - `[System.Serializable] ModeConfig { modeName, objectsToEnable[], modePanel }`,
  - singletons written as `public static X Instance { get; private set; }` plus DontDestroyOnLoad,
  - null-safe calls (`UIButtonHoverSound.Instance?.PlayClick()`),
  - C# events for cross-component callbacks (`OnAIMoveReady`),
  - coroutines for all timing,
  - `[DefaultExecutionOrder(-100)]` on a mode bootstrapper that wires sibling components.
- **Persistence:** PlayerPrefs only for settings. Game logs go to `persistentDataPath` as `.txt`. There is **no JSON save system**.

## 5. Reusable pieces

| Reuse | What / how |
|---|---|
| ✅ Copy as-is | **Vinque Rg SDF** font asset + `.otf` · **RPG Button 7** sprites (Violet/Red/Disabled + Border) · `Icon_Exit.png` · gauntlet cursor · `click.wav`, `Clack_Minimal UI Sounds.wav`, keyboard clack sets (`keyboard/`, `NoDelayKeyboard/`), `switch-on/off.mp3` · `MUSICAUDIO/` ambient playlist |
| ♻ Port and upgrade | `UIButtonHoverSound`, `SettingsManager` (extend: SFX/music volume, graphics, FPS cap, keybinds, cast modes; JSON instead of PlayerPrefs), `MusicManager` (add the unused fade), `CursorManager`, `DraggablePanel`, `HoverPlaySound` / `DropdownHoverSound`, `KeyboardClackSoundRandom`, the `ModeConfig` panel-switching pattern, and the typed-code menu idea |
| ❌ Do not reuse | `Audio/chess.com audio/*`: these files appear to come from a commercial chess site and carry a licensing risk, so they should not ship in a Steam game. Also out: Stockfish, Netcode/Lobby/Relay, chess logic, `ChessAssets/`, and the legacy `Input.GetKeyDown` calls (rebinding needs the Input System) |

## 6. How the MOBA trainer will inherit this (proposal)

Where the new game has to diverge (3D arena, Input System), the change stays inside gameplay code. The visible layer (menus, fonts, colors, SFX) keeps the Blindfold Chess identity.

- **UI:** UGUI + TMP, one overlay canvas at 1920×1080, Vinque everywhere, RPG Button 7 Violet for primary and Crimson for secondary/destructive, 39% maroon/black glass panels, exit icon top-left, Esc opens settings, hover clack and click SFX, dark-ambient music.
- **Arena palette (derived, for placeholder 3D art):**

  | Element | Hex / treatment |
  |---|---|
  | Floor | near-black `#0E0B0B` with a faint `#151212` grid |
  | Arena rim / walls | `#2F2A2A` |
  | Player | violet `#9D26A4` with a `#6C1B71` ring |
  | Enemy telegraphs | crimson `#A52626` fill at about 25% α, `#701B1B` edge |
  | Hit flash | `#FFFFFF` |
  | Neutral units / targets | greys `#4D4D4D` / `#505050` |
  | Gold / CS feedback | `#FFD933` |

  This stays inside the reference palette apart from the reused yellow highlight.
- **Transitions:** the reference has none. I propose one small, shared, optional `UIFade` (CanvasGroup, 0.15 s) so the new game can feel a little smoother without breaking the family look. This is an open question below.
- **Code:** keep the reference conventions (Manager/Mode/UI suffixes, `[Header]` fields, `Instance` singletons for global services, C# events). Fix what doesn't scale: file name = class name, one namespace (`MobaTrainer.*`), per-area asmdefs, and a pooled, allocation-free gameplay loop.

## 7. Decisions (2026-09-28)
- Build on **Unity 6000.5.4f1** (the existing project).
- Main menu: **hybrid**. RPG-button scenario list plus typed codes as the keyboard-first signature.
- Active Input Handling: **Input System only**. Ported scripts will be migrated.
- Transitions: **subtle 0.15 s CanvasGroup fade** on panel changes.
- Asset licensing (Q4 below): still open.

## 8. Original open questions

1. **Unity version:** the reference is 6000.0.58f1, but the new project was created in **6000.5.4f1**. Should I build on 6.5 as it is, or should you recreate the project in 6000.0.58f1?
2. **Main menu style:** the live Blindfold Chess menu is the **typed-code menu** (`a1` + Enter). Should I keep that as the signature (for example type `d1` for Dodge), use RPG-button tiles, or build a hybrid?
3. **Input System only:** can I switch Active Input Handling to *Input System Package* only? The reference uses legacy input, but rebinding needs the new system.
4. **Asset licensing:** can you confirm you own or are licensed for RPG Button 7, the cursor pack, the Vinque font and the Pixabay music for a commercial Steam release? `chess.com audio` stays excluded either way.
5. **Fade transitions:** should I add the 0.15 s fade, or keep the reference's instant swaps?


## 9. Design direction v2 (2026-09-30) - replaces sections 2 and 6 for the new game
After seeing the first build, Alper rejected the Blindfold Chess look for this game as too old-fashioned. The new direction is **modern, fully minimal and dark-only, with smooth colours**.

| Token | Hex |
|---|---|
| Bg / BgElevated | `#0B0B0E` / `#111116` |
| Surface / hover / pressed | `#16161C` / `#1D1D25` / `#121217` |
| Border / strong | `#25252E` / `#34343F` |
| Text / muted / faint | `#EDEDF2` / `#9A9AA8` / `#5E5E6B` |
| Accent (soft violet) / hover / pressed | `#8B7CF6` / `#9D90F8` / `#7666E8` |
| Danger / Success / Highlight | `#EF5B6B` / `#3DD68C` / `#F5C451` |

**Type:** Inter (SIL OFL) in Regular, Medium and SemiBold, as dynamic TMP assets. Sizes 60 / 28 / 22 / 17 / 15 / 13.

**Shapes:** generated rounded rects (16 px panels, 10 px buttons, 8 px tags), 1.5 px hairline borders, no textures. Button styles are Primary (accent fill), Secondary (surface + border), Ghost and Danger.

**Motion:** 0.12 s colour transitions on hover, 0.15 s panel fades, a sliding tab underline and animated switches.

**Audio:** click sound only. There are no hover sounds and no typing clacks.

**Cursor:** a clean arrow by default; the gauntlet cursor stays available as an option.

**Kept from the reference:** single scene with panel switching, keyboard-first command bar (type a code), code conventions, ambient music.


## 10. Design direction v3 (2026-09-30) - current, supersedes v2 colours
Feedback on v2: too much text; wants a more graphical, modern menu with a **red-black atmosphere** and **no music for now**.

**Palette:**

| Token | Hex |
|---|---|
| Bg | `#060607` |
| Surface | `#131316` |
| Border | `#241C1E` |
| Accent red / hover / pressed | `#E23A45` / `#F0515B` / `#C42D38` |
| Text | `#F2F2F4` |
| Player | off-white `#E6E6EA` |
| Enemies | red |

**Icon-first UI:** 20 generated line icons (`Art/UI/Icons`, 128 px white PNGs, tinted at runtime).
- **Main menu:** a 3x3 grid of icon-only drill tiles. A single caption line names the hovered tile, and hovered tiles scale up and turn red. Gear and power icon buttons, a small `>` code bar, and a wordmark.
- **Settings:** icon tabs (speaker / monitor / mouse / keyboard), with the tab name shown as the only title.
- **Pause:** icon + one-word rows.
- **HUD:** the camera-lock state is shown as an icon.

**Atmosphere:** a breathing red glow, slowly counter-rotating dashed rings, and a dark-red vignette in the arena.

**Audio:** click only. MusicManager is not placed in the scene.


## 11. Design direction v4 (2026-09-30) - current
Feedback: Inter looked robotic; wants a real menu (no command line), nothing useless on screen, a modern atmospheric look.

**Type:**
- **Cinzel** (Bold / SemiBold) for titles and menu items: uppercase, wide letterspacing.
- **Montserrat** (Regular / Medium / SemiBold) for body text.
- Both fonts are SIL OFL.

**Title screen:** the live 3D arena is the background, with a slow cinematic orbit camera, red ember and dust particles, dark red fog and a vignette.
- On the left: "MOBA / TRAINER" (the second word in red) above a red rule.
- The menu is PRACTICE / DRILLS / SETTINGS / QUIT. The hovered item slides right, with a red marker.
- There is no command bar, version number or brand text.

**Drills screen:** a 3x3 grid of icon cards with short Cinzel names. Locked drills are dimmed.

**Pause:** icon + Cinzel uppercase labels. The HUD title uses Cinzel.


## 12. Feel & kit v5 (2026-09-30) - current
**Scale:** 1 Unity unit = 100 game units of a typical MOBA.

**Player:**
- Move speed 3.25, gameplay radius 0.65 (dummies are the same).
- Attack range 5.5 (edge to edge), 0.625 attacks/s, windup 18.8 %, attack missile speed 20.

**World:**
- Arena radius 16.
- Camera pitch 56°, FOV 40, zoom 15–22.

**Kit** (generic names; the numbers follow a classic ranged marksman). Keys: Q, W, E + Blink.

| Key | Shape | Cast time | Range | Width | Speed | Cooldown |
|---|---|---|---|---|---|---|
| Q (Bolt) | line | 0.25 s | 12 | 1.2 | 20 | 4.5 s |
| W (Wave) | line | 0.25 s | 12 | 1.6 | 17 | 8 s |
| E (Shift) | blink 4.75, then a homing bolt at the nearest enemy within 7.5 | 0.25 s | — | — | 20 | 14 s |
| Blink (D or F) | blink | instant | 4 | — | — | 15 s |

**Casting rules:**
- During a cast time the character stops. Move orders are remembered and resume afterwards, and no auto-attack starts.
- Blink can be used during a cast time.
- The builder re-applies these numbers to the ability assets on every build.

**Cast mode:**
- Quick cast is the default for every key (settings v3 migration). Normal (left-click confirm) is opt-in per key.

**HUD:**
- Slots show only the bound key letter. There are no ability glyphs.
- The Blink slot is red-tinted and set apart by a small gap.

**Sensitivity converter:**
- Settings > Controls has a "Sensitivity from other MOBA" slider (0–100) and a fine-tune multiplier.
- Conversion: value/5 gives a Windows pointer notch, and the Windows notch multiplier is interpolated. Estimate only (assumes Enhance Pointer Precision is off).
- Examples: 35 → 0.625×, 50 → 1.0×.

**Naming:** the summoner-style reposition is called **Blink** everywhere in the UI.

**Audio:**
- The click plays on pointer-down.
- The DSP buffer is set to 256 (Best latency).

**Build:** don't output the Windows build into OneDrive-synced folders. Its `level0` was reported as corrupted there. Use a local folder such as `C:\Builds\MobaTrainer`.


## 13. Map & HUD v6 (2026-09-30) - current
**Arena:**
- Stylized stone plaza with a radius of 20.
- The floor uses the procedural `StylizedGround` shader: flagstones, moss, dirt, and main-light shadows. It fades to dark beyond the edge.

**Map layout (seeded, so it's identical on every build):**
- Two curved stone walls, NW and SE, point-mirrored. Each one has a grass pocket behind it.
- Two short walls, NE and SW.
- Pillars with brazier flames (8 point lights).
- About 70 low-poly pines ring the arena outside the play area.

**Collision:**
- Walls, pillars and pocket trees block movement. They are stored as circles in `ObstacleField`, and units slide along them.
- Skillshots fly over walls.

**Lighting:**
- Natural trilight ambient, a warm sun and a greenish-dark fog.
- Neutral vignette.
- The red-black palette is now only for menus and UI.

**Units:**
- Player HP bar, selection ring and move-click marker are green.
- Dummies are clay-red with red HP bars.

**HUD (minimal):**
- Only the key slots, plus hits / misses / timer pills above them.
- No title, no stats block.

**Settings:**
- Autosaved about 0.35 s after any change.
- Also saved on panel close, on quit, on focus loss, when leaving Play mode, and after migrations.
- Stored in the local JSON file `persistentDataPath/Save/settings.json`.


## 14. v7 (2026-10-04): intense tuning, cliff walls, hero rig
**Intense training defaults:**
- Cooldowns: Q 1.5 s, W 3 s, E 5 s, Blink 6 s.
- Attack speed: 1.25 attacks per second.

**Walls:**
- Chunky rock outcrops built on a smoothed spine, with a bulging width.
- Three-tier flat-shaded cliff: a wide foot, a rough ledge, and a mossy mound top.
- Small pines and grass grow on top. Braziers stand at both tips.
- Collision circles follow the foot of the cliff.

**Characters:**
- An original low-poly "Wayfarer": travel coat, scarf, satchel, and a glowing focus stone in the right hand.
- Player look: blue and gold. Sparring partners: crimson.
- Animation is fully procedural (`CharacterAnimator`):
  - run cycle driven by real ground speed;
  - idle breathing;
  - attack arm raise during windup, plus recoil;
  - cast thrust;
  - blink squash;
  - hit flinch.
- Enemies face their movement direction, or the player when they are idle.

**Not done:** no character from an existing game is reproduced.


## 15. v8 (2026-10-04): marksman kit rules, menu stroll, layout
**Q (Bolt):**
- Damage 80. Cooldown 2.5 s.
- On hit, all ability cooldowns (not Blink) drop by 1.5 s.
- Detonates a mark.

**W (Wave):**
- Deals no damage. A hit marks the enemy for 4 s, shown as a golden pulsing ring.
- Q or E detonates the mark for +100 damage. Auto-attacks do not.

**E (Shift):**
- Blink 4.75. Then a bolt hits one enemy within 7.5 of the landing point.
- A marked enemy is targeted first.
- Detonates a mark.

**Blink:** instant, 6 s cooldown.

**Cast times:** Q, W and E 0.25 s.

**Projectiles:**
- HDR white-gold core, stretched along the flight path, with an additive tapered trail (`AdditiveTrail` shader).
- W uses a bigger, more golden projectile.
- Hits flash gold.

**Title screen:** clicking the arena, not the menu, walks the hero there.

**Characters:**
- Models scaled ×1.3; the hitbox is still 0.65.
- HP bars sit at 2.95.

**Layout:**
- Long cliffs moved into the middle ring.
- Short outcrops at the corners.
- Two small juke rocks near the centre, without braziers.
- Dummies are clamped out of the walls.


## 16. v9 (2026-10-04): drills, results, stats
**Mark detonation:** auto-attacks now detonate W marks too, as do Q and E.

**Framework:**
- `Scenario` base class: Begin → 3-2-1 countdown → StartRunning → Completed(RunRecord) → Results panel.
- Enemies are pooled `TargetDummy` instances (hero rig) driven by `EnemyBrain`. The brain makes them juke around a range band and throw line or circle skillshots, standing still during the cast time and leading the target some of the time.
- `EnemySkillSystem` handles the enemy shots:
  - telegraphed lanes that fill during the windup, then a magenta-red bolt;
  - circles that fill, then hit.

**Skillshot (60 s):**
- 3 juking enemies at 6.5–10.5 range; they respawn elsewhere.
- Score = hits ×100 + mark detonations ×150 + kills ×200.

**Dodge (60 s):**
- 4 invulnerable casters at radius 10. The player cannot die.
- Shots ramp: interval 2.6→1.1 s, speed 16→22, windup 0.4→0.25. Circles start after 15 s.
- Score = dodged ×10 − hits taken ×40.

**Duel:**
- 900 HP each. The enemy jukes, throws Q-like lines (90 dmg) and circles (70 dmg). 2-minute cap.
- Win score: 1000 + HP left % ×10 + speed bonus. Loss score: damage dealt % ×5.

**Stats:**
- `StatsStore` keeps every run in `Save/stats.json` (cap 1000).
- Stats page per drill: runs, best, average of the last 10, key rate (accuracy / dodge rate / win rate), a chart of the last 20 scores with the best highlighted, the latest 6 runs, and total minutes trained.

**Menu:** Practice / Drills / Stats / Settings / Quit.
