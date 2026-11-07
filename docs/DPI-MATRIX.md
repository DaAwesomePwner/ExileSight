# Phase 5 DPI and monitor verification

Automated placement tests run on any OS. They establish geometry, not Windows rendering or in-game compatibility. All owner cells below are **pending** until checked on the release build on Windows.

| Scale | DPI conversion fixture | Physical/DIP, secondary/negative origin, cursor corners, work-area clamp, default panel separation | Windowed 🎮 | Borderless 🎮 | Exclusive fullscreen 🎮 |
|---|---|---|---|---|---|
| 100% | 96 | Automated | [ ] | [ ] | [ ] |
| 125% | 120 | Automated | [ ] | [ ] | [ ] |
| **133%** | **128 (rounded Windows DPI)** | **Automated, exact 1.33 overlay override too** | [ ] | [ ] | [ ] |
| 150% | 144 | Automated | [ ] | [ ] | [ ] |
| 175% | 168 | Automated | [ ] | [ ] | [ ] |
| 200% | 192 | Automated | [ ] | [ ] | [ ] |

`DpiMatrixTests` covers each row, including the actual content transform used by the host and popups. Its layout inventory includes status, generic, guide, both alternative timer views, both toasts, and the hidden Stashninja registration placeholder. `OverlayRefreshTests` exercises the live pure refresh decision for 100%→150%→100% monitor moves, a negative-coordinate secondary monitor, DPI-only changes, and WPF-suggested bound correction. The same tests cover the live cursor-centered wheel placement; the superseded wheel helper and its placement assertions were removed. `ProductionOverlayTests` covers correction of WPF's suggested DPI bounds and reasserting position after show. `OverlayRulesTests` covers visibility and client scaling. Windows-only `OverlayPlatformTests` checks native ownership guards and monitor work-area lookup; it does not simulate a physical mixed-DPI desktop.

## Coordinate contract and defaults

The manifest declares PerMonitorV2. Client rectangles, cursor anchors, monitor work areas and native HWND bounds use physical virtual-desktop pixels, including negative coordinates. Fixed panels use client-height / 1080 scaling, or the explicit overlay scale override (which replaces automatic scaling). Windows display scaling and the overlay override are independent; do not multiply them. At 133% Windows may report integer DPI 128; the owner's explicit 1.33 override stays exactly 1.33.

After native placement establishes the destination monitor, content transforms divide the desired physical scale by WPF's current DPI scale. WPF DpiChanged queues a placement refresh after its suggested bounds are applied. Game location/size events refresh the host. The macro wheel now supplies one placement function to that host, avoiding competing fixed-panel and cursor placements on different monitors.

Status is (440,20,160,32), clear of the generic panel. Guide is (440,470,460,440); Timers is (20,340,400,530). The compact timer view uses the same origin and width with height 220 (an alternative to the expanded view). These leave space for generic (20,20,400,300), timers toast (440,80,300,100), and events toast (500,340,340,110). Uniform scaling preserves separation. Arbitrarily large overrides or very narrow aspect ratios can put fixed panels beyond the client; no automatic relocation is applied to fixed panels. Generic panels share one slot and cursor popups may cover other panels intentionally.

Popups clamp to the intersection of the game client and its nearest monitor's work area, keeping them off taskbars and on the game's monitor after a move. If the client is entirely offscreen, use that nearest work area. Fitting popups move inward without shrinking, even when the cursor is at an edge. Only popups larger than the entire available area shrink their bounds to fit; long content may need scrolling or may clip (especially item tooltips). Offscreen cursor anchors are clamped. No game window mutation or input synthesis is involved.

## Owner checklist (each matrix cell)

1. Record build SHA, Windows version, renderer, game mode, monitor resolution/arrangement, actual reported DPI, and overlay override. Start with automatic scaling; repeat with override **1.33**. Check text, borders and click targets.
2. Show Guide with the expanded Timers panel, then its compact view; trigger both toasts. Confirm the default regions remain separate. Show a generic feature panel too.
3. Open omni, item-info, map-info and the macro wheel near all four client corners. Check edges against the taskbar (including a left or top taskbar), scrolling and long items. Confirm the wheel stays centered where possible and clamps near edges.
4. Drag the game manually between primary and secondary monitors with different DPI (100↔133%, 125↔150%, 100↔200%). Repeat with the secondary left of and above the primary (negative coordinates), and with the game straddling monitors. Confirm repositioning/rescaling without restarting, flicker loops or stale bounds. Repeat after changing Windows display scale while running, where Windows permits it.
5. Resize the game manually, minimize/restore, and alt-tab away/back. Overlays hide when unfocused/minimized, return at the correct scale, and never steal focus. Click-through overlays still pass mouse input; interactive overlays retain their previous behavior. ExileSight never moves, resizes, restyles or raises the game.
6. In exclusive fullscreen, record whether Windows/renderer composition displays overlays. True exclusive fullscreen may not show external WPF windows; this is an expected platform limitation, not permission to hook the renderer or change game settings. Manually return to borderless and verify recovery without restart.

Attach results and unresolved clipping/rendering issues to the Phase 5 🎮 gate. No in-game cell is claimed by a passing unit test.
