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
- **Freehand curve drawing** — click and drag to paint a custom gamma curve like a true maniac
- **Skew sliders** — twist and remap your drawn curve from any corner without starting over
- **Named profiles** — save your "dark room at 2am" and "midday gaming session" setups
- **Global hotkeys** — switch profiles without alt-tabbing (Ctrl+Alt+anything you want)
- **System tray** — lives quietly in your tray, out of your way
- **Start with Windows** — set it and forget it

---

## Install

1. Download `GammaSutra.exe` from [Releases](../../releases)
2. Put it somewhere. Desktop, games folder, a folder named "definitely not cheating", whatever
3. Run it
4. That's it

Settings save to `%AppData%\GammaSutra\`. No registry garbage (well, only if you enable Start with Windows — that's just one key).

> **⚠️ HDR Note:** Windows disables gamma ramps when HDR is on. Turn off HDR for this to work. Yes, that's Microsoft's fault, not mine.

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

### Draw Mode

Click **Draw Mode** to paint a completely freehand gamma curve. The sliders gray out and the canvas becomes your canvas.

- **Click and drag** to draw
- **Left/Right sliders** — twist the curve from its dark or bright endpoint
- **Top/Bottom sliders** — remap the input axis (compress highlights from the top, lift shadows from the bottom) — bakes in on release
- **Revert** — undo your mess back to the last saved state

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
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

---

## Why Does This Exist

Because f.lux is for color temperature, monitor OSD is a pain in the ass, and NVIDIA's color settings reset themselves every driver update. Sometimes you just want a curve editor that sits in your tray and does exactly what you tell it to do.

---

## License

MIT. Go nuts.
