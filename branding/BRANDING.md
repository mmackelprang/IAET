# IAET — brand guide

**Proposed display name:** IAET
**Tagline:** *Find the API they didn't document.*

## Why this name

Keep the acronym — it's already the CLI (`iaet`), the tool id, and the package name; renaming
would cost more than it buys. Brand it instead: set the acronym in the mark, and let the
expansion ride underneath as a lockup ("Internal API Extraction Toolkit") on first mention.

**Alternates considered:** *Excavate* (what it does, loses the CLI tie), *Fieldglass*
(surveillance overtones), *Undocumented* (great tagline material, weak name).

## The mark

Captured traffic as log rows, one row lit cyan under a magnifier: the undocumented endpoint,
found. The lens is the brand — IAET observes and documents; it doesn't alter.

## Palette

| Color | Hex | Role |
|---|---|---|
| Ink | `#14121F` | Background / primary brand color |
| Slate Purple | `#4A4468` | Unexamined rows, muted UI |
| Scan Cyan | `#26C6DA` | The found row, lens, accents |

## Voice

Research voice: capture, trace, catalog, export. Keep the "educational and security research
purposes only" line visibly attached to the brand wherever the mark appears — it's part of the
identity, not fine print.

## Files in this directory

| File | Use |
|---|---|
| `logo.svg` | Full lockup (mark + wordmark + tagline) for README headers and docs |
| `favicon.svg` | Square app mark, scales from 16px to full size |
| `favicon.ico` | Legacy multi-size favicon (16/32/48) for browsers that want `.ico` |
| `favicon-32.png` | 32px PNG favicon |
| `apple-touch-icon.png` | 180px iOS home-screen icon |
| `icon-512.png` | Large raster for app manifests, social cards, stores |

### Wiring the favicon into a web page

```html
<link rel="icon" href="/branding/favicon.svg" type="image/svg+xml">
<link rel="icon" href="/branding/favicon.ico" sizes="16x16 32x32 48x48">
<link rel="apple-touch-icon" href="/branding/apple-touch-icon.png">
```

### README header

```markdown
<p align="center"><img src="branding/logo.svg" alt="IAET" width="520"></p>
```

## Typography

Wordmark: **Montserrat Bold** (falls back to Segoe UI / system sans). Body text: the platform
default sans. For code-adjacent surfaces, any monospace at hand — the brand doesn't pin one.

The logo's wordmark is live SVG text, so it renders with whatever sans is installed; if you want
it pixel-identical everywhere, convert the text to outlines in any SVG editor and re-save.

## Dark and light backgrounds

The tile carries its own background, so both `logo.svg` and `favicon.svg` work unchanged on
light or dark pages. The wordmark in `logo.svg` is dark ink — on a dark page, either rely on the
tile alone (use `favicon.svg`) or restyle the two `<text>` fills to `#F0F2F5`.

---
*Generated as a proposal — names, colors, and marks are suggestions to accept, tweak, or reject.*
