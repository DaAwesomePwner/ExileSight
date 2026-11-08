# Design reference

The 1.1 UI redesign is specified in [UI-SPEC.md](UI-SPEC.md). This folder holds the
mockups that spec refers to. They are the visual reference for every UI task in
[Phase 7](../ROADMAP.md#phase-7-ui-redesign-11).

## Mockups

Each mockup is a static HTML page with sample values, plus a PNG render of it.
Open the PNG to look at it (agents: read the image file); open the HTML in a
browser to inspect exact sizes and colours.

| Mockup | What it shows | Tasks |
|---|---|---|
| [item-card](mockups/png/item-card.png) ([html](mockups/item-card.html)) | Item card in a mock game screen: anatomy, placement, colour scale | P7-T4, P7-T5, P7-T11, P7-T15 |
| [item-card-states](mockups/png/item-card-states.png) ([html](mockups/item-card-states.html)) | Rare with implicit + crafted, weapon with hybrid mod, unique, plain copy, unidentified | P7-T5, P7-T11 |
| [map-card](mockups/png/map-card.png) ([html](mockups/map-card.html)) | Map card, rated and first-time | P7-T6 |
| [trade](mockups/png/trade.png) ([html](mockups/trade.html)) | Shop pricer (armed, disarmed) and stash price tags | P7-T7, P7-T8 |
| [ambient-hud](mockups/png/ambient-hud.png) ([html](mockups/ambient-hud.html)) | Act guide, timers, event toasts, omni menu, chat wheel, search strings | P7-T9, P7-T10 |
| [app-home](mockups/png/app-home.png) ([html](mockups/app-home.html)) | Desktop app shell and Home (Hotkeys reuses its key list) | P7-T12 |
| [app-item-rules](mockups/png/app-item-rules.png) ([html](mockups/app-item-rules.html)) | Highlight-rule editor with live card preview | P7-T13 |
| [app-overlay-settings](mockups/png/app-overlay-settings.png) ([html](mockups/app-overlay-settings.html)) | Overlay settings and the transparency log | P7-T14 |
| [tokens](mockups/png/tokens.png) ([html](mockups/tokens.html)) | Colours, type, mod-row and card specs | P7-T1, all |

## How to compare your work

1. Read the spec section for your surface in [UI-SPEC.md](UI-SPEC.md), then open its PNG.
2. Build the view as a pure builder (`model + theme → FrameworkElement`) and add your own
   snapshot test class to the P7-T2 harness, one test per state with fixture models. Get the
   renders from Windows: run the snapshot tests on the Windows host (the only sure route until
   P7-T2's `workflow_dispatch` trigger is on `main`), or read the `ui-snapshots` CI artifact from a
   manual dispatch of your branch or from a PR into `main` or `integration/**`.
3. Compare side by side. Must match: section order, which values appear, column
   order and widths, colours (by token), unknown-value treatment, and what the
   view leaves out. May differ: sample values, font rendering (Segoe UI vs the
   mockup's Inter), and anti-aliasing.
4. If the mockup is impossible or wrong for real data (a value we don't have, a
   label that doesn't fit), don't silently diverge. Follow the spec text, and list
   the difference under `RISKS` in your report so the owner can decide.

The spec text wins over a mockup when they disagree. Sample values in mockups are
illustrations, not data: never copy them into code or fixtures.

## Changing a mockup

Mockups change only with owner approval. After editing an HTML mockup, re-render
its PNG with `docs/design/render-mockups.sh <name>` (needs Chrome or Chromium;
set `CHROME` to its path if it isn't in the default location) and commit both.
The owner also keeps an editable design canvas; keep the two in step.
