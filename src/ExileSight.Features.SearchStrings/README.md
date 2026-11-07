# Search strings

F9 (`searchstr.open`) opens a manual vendor/UI menu. F7/F8 remain the existing
copy/hideout defaults. Persisted bindings with ID `searchstr.open` override this
default, including disabled bindings. Opening and choosing a vendor send nothing.
Click a saved search to highlight matches once. Wheel over a search cycles its
lines without sending input. Esc, Close or clicking outside dismisses the menu.

Edit the feature section in the active profile's settings JSON, then reopen:

```json
{
  "features": {
    "sections": {
      "searchstr": {
        "vendors": {
          "Vendor": [
            { "Name": "Boots", "Text": "movement speed", "Regex": false },
            { "Name": "Socket colours", "Text": "r-g-b|r-b-g", "Regex": true }
          ],
          "Stash": [
            { "Name": "Map names", "Text": "Beach Map\nStrand Map" }
          ]
        }
      }
    }
  }
}
```

A supplied vendor list replaces that vendor's defaults, including an empty list;
missing vendors receive built-in defaults. User edits are never written over.
Settings reload on each open. Invalid settings disable the menu's searches and
show a notice without altering the saved data.

Each literal line escapes regex metacharacters and is quoted. `Regex: true`
preserves intentional PoE expressions but still quotes the line. Quotes, control
characters, Unicode line/paragraph separators, blank lines and searches exceeding 250 characters **after** escaping
and quoting are refused, never truncated. Regex lines ending in an unescaped
backslash are also refused. CRLF and LF separate cycling lines.
Limits: 50 user vendors, 100 entries per vendor, 100 lines per entry, 80-character
names. Names within a vendor and vendor keys must be unique ignoring case.

Behaviour reference: Exile-UI `modules/search-strings.ahk`. These simple PoE1
defaults are independently authored. There is no screen detection or vendor
inference. Hosted windows use NOACTIVATE; paste buttons redeem physical overlay
clicks through the existing hook redemption API in a feature-local click handler.
Preparation uses the shared overlay failure guard. Pending selections are
single-use, expire after 1.5 seconds, and clear whenever the menu hides or a
click finishes (including failed redemption or dispatch). The menu closes after
submission completes so hiding cannot race normal queued dispatch. Shared host
command routing is unchanged; an overlay-only command restriction remains host
work.
