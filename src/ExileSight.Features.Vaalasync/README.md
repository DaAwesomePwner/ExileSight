# Shop repricing

Omni reads the priced item and arms one price action. Open the game's note box yourself; ExileSight never opens it. Each physical price-button click redeems one overlay-click token. After a successful send or an ambiguous failure (including exceptions and partial sends), buttons stay disabled until another omni press reads an item. Enter closes the game's note box, so this re-arm requirement prevents another price text from reaching chat while the box is closed. An omni read during an in-flight send does not re-arm. The UI ignores subsequent clicks classified by WPF as part of a system double-click sequence, before token redemption.

The commands `vaalasync:minus1`, `vaalasync:plus1`, `vaalasync:minus10`, `vaalasync:plus10` and `vaalasync:custom` can also be hotkey-bound. One physical key press supplies one token for one SetPriceNote, which FAIR-PLAY allows. Hotkeys obey the same omni re-arm requirement; binding a command does not grant background input authority.

All suggestions, including ±1 and custom fractions, round away from zero at midpoint: divine/mirror to two decimals; other currencies to two decimals below 10 and whole units at 10 or above. Nonpositive rounded prices and values above 1,000,000 are refused. Prefixes are preserved.

History is bounded to 500 listings, 50 entries per listing and 8 MB. Invalid history is moved to a timestamped `.corrupt` file and the shop starts with an empty history; the recovery path remains visible in the model status. If preservation fails, writing is disabled to protect the original. Whisper matches may use item name, base type or both, but remain uncertain evidence, never completed sales. No buyer name or raw whisper is stored.

Only explicit pre-input refusals (MissingToken, UsedOrUnknownToken, Expired, NotForeground, Undeclared and WrongAction) keep repricing armed. A generic Failed result does not prove that no keys went out: clipboard restoration or pumping can fail after Enter closes the note box. These attempts are logged as “Possibly set”; a fresh omni read is required before another action. Success displays and stores the gate’s paste confirmation status once.
