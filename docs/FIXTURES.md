# Recording test fixtures

The parsers are tested against real text copied from the game. The owner records these on Windows during the Phase 0 gate and whenever an in-game bug shows up. Each bug becomes a fixture first, then a failing test, then a fix.

## Layout

```
tests/fixtures/
  poe1/items/<class>/<short-name>.plain.txt      Ctrl+C on the item
  poe1/items/<class>/<short-name>.advanced.txt   Ctrl+Alt+C on the same item
  poe1/items/<class>/<short-name>.expected.json  optional, written by agents once a fixture is verified
  poe1/logs/<scenario>.txt                       a Client.txt excerpt
```

`<class>` uses the game's item class in kebab-case (`body-armours`, `rings`, `maps`, `stackable-currency` …).

## What to record (Phase 0)

- **Items:** about 150 in total, covering:
  - every rarity (normal, magic, rare, unique)
  - every gear slot, both weapon hands, flasks and jewels (including cluster jewels)
  - maps, logbooks, currency, fragments, gems and divination cards
  - corrupted, fractured, synthesised, influenced, anointed, mirrored and unidentified items
  - items with a `~b/o` or `~price` note
- **Logs:**
  - a campaign run covering Acts 1–3 (area changes and level-ups)
  - one mapping session with deaths and league-mechanic NPC dialogue (enable `output_all_dialogue_to_chat`)
  - a hideout session

## Privacy

`Client.txt` contains whispers, character names and account names. Run `dotnet run --project tools/ExileSight.RedactLog -- EXCERPT.txt --output REDACTED.txt` on a copied excerpt (never the live game file). Output must be a new file; omit `--output` for stdout. Add `--keep "YourCharacter"` to preserve your own name. Names are consistently pseudonymized per invocation; whisper/chat bodies, filter names, dialogue text and unrecognized lines are removed conservatively. Review the result before committing. Item texts are fine to commit as-is.
