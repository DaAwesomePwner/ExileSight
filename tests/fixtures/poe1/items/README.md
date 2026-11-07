# Synthetic PoE1 item copies

These are synthetic English PoE 3.2x-style examples, not client recordings.
Real owner recordings override them. No upstream item data is distributed here.
The fixtures are static text and JSON; no generator or Python runtime is required.

Each expected.json contains independently authored assertions in common, plain,
and advanced objects. Objects assert the supplied fields; arrays assert exact
contents. Warnings refers to the parse result; other fields refer to ParsedItem.
Every expectation asserts Warnings and OtherText, including empty arrays.
New copies without expectations must still parse without error diagnostics.

Magic bases are derived from advanced prefix/suffix names when present; otherwise
unknown bases remain null. Influence flags come from trailing item markers in
both copy modes; advanced modifier kinds additionally retain their source.
Property sections preserve textual properties (weapon type, gem tags, radius),
while modifier sections retain digit-free lines. Gem descriptions, currency
instructions and card rewards live in OtherText. Quoted prose and punctuated
unique/card flavour sections live in FlavourText. Real recordings should refine
these boundaries where the plain text does not carry explicit metadata.

Classes without modifiers (currency, cards, fragments, these gem examples) assert
empty Mods arrays; numeric property rolls are checked where applicable.
