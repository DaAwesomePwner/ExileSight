# Changelog

Player-facing changes for each ExileSight app release. The release workflow
copies the `## <version>` section into the GitHub release notes and fails if it
is missing, so add the section on main before dispatching a release.

## 1.1.0

- **Automatic stash prices:** open a supported stash page and poe.ninja prices
  appear on it, with no menu or calibration step. ExileSight recognises the
  page from a small, display-only read of the game window while the game is in
  front, and never sends input. Turn it off with **Show stash prices
  automatically** in the Ctrl+Shift+N menu. The first visit to each page takes
  a few seconds. Stash pages calibrated with an earlier build need **Auto-calibrate**
  again.
- **Lab overlay (Ctrl+Shift+L):** import the day's lab compass file from
  poelab (**Open poelab**, then **Import file…** or **Paste JSON**) to see the
  room map, darkshrines, golden doors, Argus and Izaro's weapon, phases and
  traps. Your current room is followed from the game log. ExileSight never
  contacts poelab itself.
- **New app icon** for the app, tray and installer.
- **Known issue:** stash price tags can blink briefly when you switch currency
  sub-tabs or move the mouse over the stash. A big tooltip covering most of the
  stash may hide them for a couple of seconds; they come back on their own.

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
