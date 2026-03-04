# Gamma Sutra: Definitely Not Cheating

A Windows display calibration tool for people who are tired of their monitor looking like it was configured by a sad accountant.

![Gamma Sutra icon](Resources/icon.ico)

---

## What Is This

You know how every monitor looks slightly different, and every game has different gamma/brightness settings hidden in the options menu that you adjust once and never touch again because who has time for that? This fixes that. Globally. For real.

Gamma Sutra lets you dial in your display's output curve at the OS level using `SetDeviceGammaRamp` — the same Win32 API that GPU drivers and professional calibration tools use. Your adjustments apply to everything on screen, not just one game.

---

## Features

- **Per-monitor control** — got two monitors that don't match? Now they do
- **Parametric controls** — Gamma, Brightness, Contrast, S-Curve, Highlights, Shadows
- **Node mode** — click to place control points, drag to shape a smooth curve (monotone cubic spline — no overshoot)
- **RGB channel isolation** — adjust All channels together, or tweak R, G, B independently
- **Posterize** — quantize output levels with configurable steps, feather, and range
- **Resizable window** — drag to resize, with graph zoom (scroll wheel) and pan (middle-click drag)
- **Named profiles** — save your "dark room at 2am" and "midday gaming session" setups
- **Global hotkeys** — switch profiles without alt-tabbing (Ctrl+Alt+anything you want)
- **System tray** — lives quietly in your tray, out of your way; shows per-monitor profile status
- **Start with Windows** — set it and forget it

---

## Install

1. Download `GammaSutra.exe` from [Releases](../../releases)
2. Put it somewhere. Desktop, games folder, a folder named "definitely not cheating", whatever
3. Run it
4. That's it

Settings save to `%AppData%\GammaSutra\`. No registry garbage (well, only if you enable Start with Windows — that's just one key).

> **Warning: HDR Note:** Windows disables gamma ramps when HDR is on. Turn off HDR for this to work. Yes, that's Microsoft's fault, not mine.

---

## The Controls

### Parametric Mode (default)

| Slider | What it does |
|--------|-------------|
| Gamma | Power curve. Above 1.0 = darker midtones, below 1.0 = brighter. The classic. |
| Brightness | Additive offset. Lifts or crushes the whole image. |
| Contrast | Scales around the midpoint. Punchy vs. flat. |
| S-Curve | Sigmoid contrast — positive crushes shadows AND highlights for that HDR-ish pop, negative flattens everything out |
| Highlights | Quadratic adjustment to bright values only. Roll off the whites without touching shadows. |
| Shadows | Same but for dark values. Lift your blacks like a cinematographer. |

### Node Mode

Click **Node** to switch to spline-based curve editing. The sliders collapse and the graph becomes interactive.

- **Left-click** on empty space to add a node
- **Left-click and drag** a node to reposition it
- **Right-click** a node to delete it (endpoints can't be deleted)
- Interpolation uses Fritsch-Carlson monotone cubic spline — guaranteed smooth, no overshoot between nodes
- Works per-channel: select R, G, or B to edit that channel's curve independently

### Posterize

Expand the **Posterize** section to quantize output into discrete levels. Works in both Sliders and Node modes.

| Control | What it does |
|---------|-------------|
| Steps | Number of output levels (0 = off, 2–32) |
| Feather | Blend zone width at each step edge — smooths the staircase |
| Range Min/Max | Limit posterization to a portion of the curve |

### Profiles

**Profile row** at the bottom shows your current profile name. The **Save** button appears automatically when you make changes. **Revert** brings you back to however it was when you last loaded/saved. **Profiles...** opens the full manager where you can create, delete, and set hotkeys.

---

## Building From Source

```
dotnet build
dotnet run
```

Requires .NET 9 SDK. That's it.

To build a distributable single-file exe:
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish-single
```

---

## Version History

- **v1.0** — Initial release: parametric sliders, profiles, hotkeys, tray icon
- **v1.1** — Unified mode switching, collapsible posterize, dynamic window height
- **v1.2** — Node mode (monotone spline), RGB channel isolation, per-monitor profile display, resizable window with zoom/pan, built-in Default profile, removed Bezier mode

---

## Why Does This Exist

Because f.lux is for color temperature, monitor OSD is a pain in the ass, and NVIDIA's color settings reset themselves every driver update. Sometimes you just want a curve editor that sits in your tray and does exactly what you tell it to do.

---

## Support the Project

If Gamma Sutra saved you from one more trip into your monitor's cursed OSD menu, or you just appreciate free software that doesn't try to upsell you on a "Pro" tier — consider tossing a few bucks my way. It helps keep the lights on (at the correct gamma level, obviously).

**[Donate here](https://streamlabs.com/captaindapper/tip)** — any amount is genuinely appreciated.

---

## License

MIT. Go nuts.
