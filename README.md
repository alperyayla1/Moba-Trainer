# MOBA Mechanics Trainer

A mechanics trainer for MOBA players: short, scored drills (in the spirit of aim trainers) for
skillshots, dodging, kiting, last hitting, blink reactions, combos, awareness and 1v1 duels,
with a stats page that tracks every run.

Made with **Unity 6000.5.4f1** (URP, Input System). All characters, abilities, art and names are original.

## Getting started

1. Open the project folder in Unity Hub (Unity 6000.5.4f1).
2. Run **Tools ▸ MOBA Trainer ▸ Build Main Scene** (it also runs automatically after a code update).
3. Open `Assets/_Project/Scenes/Main.unity` and press **Play**.

## Controls (default, rebindable in Settings)

| Key | Action |
|---|---|
| Right click | Move / attack |
| A + Left click | Attack-move |
| Q / W / E | Bolt / Wave (mark) / Shift |
| D or F | Blink (choose in Settings) |
| S / H | Stop / Hold |
| Y / Space | Camera lock / centre |
| F5 | Restart drill |
| Esc | Pause |

## Drills

Free Practice · Skillshot · Dodge · Duel · Kiting · Last Hit · Blink · Combo · Awareness

Settings, keybinds and run history are saved as JSON in Unity's `persistentDataPath/Save`.

## Notes

Some third-party art/audio used locally (button sprites, cursor pack, display font, UI sounds) is
not included in this repository for licensing reasons. The game runs without them.

Docs: `DESIGN_NOTES.md`, `ARCHITECTURE.md`.
