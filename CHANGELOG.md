# Changelog

Player-facing changes for each ExileSight app release. The release workflow
copies the `## <version>` section into the GitHub release notes and fails if it
is missing, so add the section on main before dispatching a release.

## 1.0.0

First public release for Path of Exile 1. Install with `ExileSight-stable-Setup.exe`
below; installed copies update themselves after you exit the app. Windows may
show a SmartScreen warning because the installer is not signed yet.

- **Omni key (CapsLock):** tap on an item for the item-info tooltip with mod
  tiers and roll ranges; hold for a menu with gear comparison, map info, wiki
  and crafting links, and shop repricing.
- **Price tags (Ctrl+Shift+N):** poe.ninja prices over stash tabs. Disabled in
  this release until stash layouts are measured.
- **Shop repricing and sales log:** click a price to paste a whole-number amount
  into the async **Set Item Price** box; you pick the currency and click LIST
  ITEM. Optional `~b/o` note mode. Uses your league's poe.ninja prices.
- **Map tracker (Ctrl+Shift+M), map-event alerts and act guide:** run times,
  deaths and notes from the game log, with leagues from poe.ninja.
- **Chat macros (F5, F6), search strings (F9) and timers (hold Tab):** one
  command per key press.
- **Fair play:** one key press gives one action, nothing automatic, no memory
  reading. See the user guide for what is read and sent.
