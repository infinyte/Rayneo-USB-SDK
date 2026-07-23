# RayNeo HUD — Theme Engine

The HUD can be re-skinned entirely from a folder of graphics plus a small JSON
manifest. Drop a theme folder next to the executable (or point the app at one),
select it with `--theme`, and the HUD builds its scene from your assets instead
of the built-in debug chrome. No recompile required.

## Selecting a theme

The overlay chooses a theme in this order (first match wins):

1. `--theme <reference>` on the command line.
2. The `RAYNEO_HUD_THEME` environment variable.
3. Nothing — the built-in default HUD (clock, status, pitch/yaw/roll readout,
   crosshair) is used.

A `<reference>` is one of:

- a **name** — e.g. `--theme aviator`. Resolved to `Themes/<name>/theme.json`
  (also `themes/<name>/theme.json`) under the app directory or the working
  directory. Bundled themes are copied beside the exe by the build, so a name
  works out of the box.
- a **folder** — e.g. `--theme "D:\my-themes\night"`. Must contain `theme.json`.
- a **manifest path** — e.g. `--theme "D:\my-themes\night\theme.json"`.

If a theme fails to load or validate, the HUD falls back to the built-in scene
and shows the reason as an on-glass warning — it never comes up blank.

Examples:

```
dotnet run --project src/RayNeo.Hud -- --theme aviator
setx RAYNEO_HUD_THEME aviator      &  dotnet run --project src/RayNeo.Hud
dotnet run --project src/RayNeo.Hud -- --theme "D:\themes\night\theme.json"
```

## Folder layout

```
Themes/
  aviator/
    theme.json        # the manifest (required, must be named exactly this)
    crosshair.png     # your assets, referenced by name from the manifest
    panel.png
    myfont.ttf        # optional bundled font
```

Design assets on a **transparent background** and lean on bright, glowing
colors — the glasses are see-through, so anything dark or opaque fights the
world behind it. The engine adds a soft dark glow to text and images by default
(`"glow"`), which is what makes light elements readable.

## Manifest schema (`theme.json`)

```json
{
  "name": "aviator",
  "author": "Kurt Mitchell",
  "version": "1.0",
  "description": "…",
  "defaults": { "font": "Consolas", "fontSize": 18, "color": "#E8FBFF", "glow": true },
  "elements": [ … ]
}
```

`name` and a non-empty `elements` array are required. `defaults` supply values
any element can override.

### Defaults

| Key        | Meaning                                                        |
|------------|----------------------------------------------------------------|
| `font`     | System family name (`"Segoe UI"`) or a bundled `.ttf`/`.otf`.   |
| `fontSize` | Text size in DIPs.                                             |
| `color`    | `#RRGGBB` or `#AARRGGBB`.                                      |
| `glow`     | `true`/`false` — soft dark glow behind text and images.        |

### Elements

Every element has a `type` and, usually, an `anchor`. Anchors are either a
screen position — `top-left`, `top-center`, `top-right`, `bottom-left`,
`bottom-center`, `bottom-right` (casing and `-`/`_`/space are ignored) — or
`world` for a direction-locked element.

**`text`** — data-bound text pinned to a screen anchor.

```json
{ "type": "text", "anchor": "bottom-center", "margin": 28, "align": "center",
  "format": "pitch {pitch:F1}°   yaw {yaw:F1}°   roll {roll:F1}°",
  "fontSize": 20, "color": "#FFFFFF" }
```

**`image`** — a PNG. Screen-anchored (natural size, or set `width`/`height`) or
`world`-anchored (requires `width`/`height`; supports `yawDeg`, `pitchDeg`,
`levelWithHorizon`, `anchorToFirstFrame`).

```json
{ "type": "image", "anchor": "top-right", "asset": "logo.png", "width": 120, "height": 40, "opacity": 0.9 }
```

**`panel`** — a background image behind optional centered `format` text.
Requires `width`/`height`. Add `slice` for nine-slice scaling so a small PNG
stretches cleanly (corners stay crisp; edges and center stretch).

```json
{ "type": "panel", "anchor": "top-left", "margin": 22,
  "asset": "panel.png", "width": 320, "height": 52,
  "slice": { "left": 24, "top": 24, "right": 24, "bottom": 24 },
  "format": "● {status}   {temp:F1}°C", "color": "#7FE9FF", "fontSize": 16 }
```

**`crosshair`** — convenience for a world-locked reticle. With an `asset` it uses
your PNG; without one it draws the built-in vector crosshair in `color`.
Defaults to `world`, `levelWithHorizon`, and `anchorToFirstFrame`.

```json
{ "type": "crosshair", "anchor": "world", "asset": "crosshair.png",
  "width": 96, "height": 96, "levelWithHorizon": true, "anchorToFirstFrame": true }
```

## Text binding tokens

Inside a `format` string, `{token}` or `{token:spec}` is replaced each frame.
`spec` is standard .NET formatting (invariant culture). `{{` and `}}` emit
literal braces; an unknown token is left verbatim so typos are visible.

| Token            | Value                                             |
|------------------|---------------------------------------------------|
| `pitch`          | Head pitch, degrees                               |
| `yaw`            | Head yaw, degrees                                 |
| `roll`           | Head roll, degrees                                |
| `temp` / `temperature` | Die temperature, °C                         |
| `status`         | Provider status text                              |
| `connection`     | `connected` / `simulated` / `disconnected`        |
| `clock` / `time` / `date` | Current time (use a format, e.g. `{clock:HH:mm:ss}`) |

## Fonts

Set `font` (in `defaults` or per element) to a system family name, or to a
bundled font file dropped in the theme folder:

```json
"defaults": { "font": "Orbitron-Regular.ttf", "fontSize": 18 }
```

The first family in the file is used. Anything that fails to load falls back to
Consolas, so a missing or bad font never blanks the HUD. (Only bundle fonts you
have the rights to distribute.)

## Limitations (v1)

- **Live text must be screen-anchored.** World-locked elements have no per-frame
  text hook, so a `world` panel's `format` is rendered once from the current
  frame rather than updated continuously. `text` elements reject a `world`
  anchor outright.
- **Assets are PNG.** SVG is not supported yet (WPF has no native SVG renderer).
- **No hot-reload yet.** Editing a theme requires relaunching the HUD.

## How it fits the code

The engine reuses the existing HUD machinery rather than replacing it. A theme
element becomes a WPF `Image`/`TextBlock`/nine-slice panel wrapped in the same
`ScreenFixedElement` / `WorldAnchoredElement` the built-in HUD uses, added to
the same `HudCompositor` — so themed content inherits the identical anchoring,
FOV clamp/fade, and roll-leveling. The parsing, binding, anchor, and nine-slice
logic live in display-free helpers under `src/RayNeo.Hud/Theming/` and are
covered by unit tests in `tests/RayNeo.Hud.Tests/Theming/`.
