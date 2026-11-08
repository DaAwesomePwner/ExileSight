# Measurement attribution

Measured 2026-11-08 by ExileSight contributors from owner-provided screenshots.
The owner explicitly permitted screenshot use for measuring these layouts in chat.
Images were downscaled by the chat tool from a 3440x1440 game client to 2000x837.
The latter pixels are the reference coordinate system; do not rescale JSON back to
3440x1440. No screenshots or reference icons are redistributed.

The S1-X1 identification table supplied independently identified `high` cells only.
All `verify` and unknown cells were excluded, including entries whose tentative
identities came from prohibited sources. No Exile-UI data or icons were consulted.
Frame rectangles were snapped using local pixel-intensity edge analysis with
`tools/stash-layouts/snap.py`, then visually inspected over the source images.
Rectangles use half-open bounds and no folder offsets.

## Screenshot fingerprints

| Page | Owner source | SHA-256 |
|---|---|---|
| currency-general | src1.png | `a9965fec66908818f6c8908e9ab8b6b6fd7ff48757bac7615ff9855eaffbc5ea` |
| currency-atlas | src2.png | `9a383e19ddcff5b44725f8e3545e4733ff1b2fc4ab22ce12d7992e7502f33243` |
| currency-league | src3.png | `5196274745a7af47422af0ee64668d34f392a0d2c83032beeb998cefab0cb01a` |
| fragment-general | src4.png | `f46aa62f14e93ebb4abc809fd34dc7b82d2412d8aad97438fc8f13d7d0182b14` |
| fragment-allflame (deferred) | src6.png | `3ea4148b249bcb5e2b0ef3f269fb50fa952f12a68d01db9232bec8d0c6911a63` |
| delve | src8.png | `d488eb2c4babab43ba87c34e50ef97ca76e17c899a8b08d4b9aba96b00653685` |
| essence | src9.png | `108da20a346b2c7ff76da66c10f8954b8cef552138b7ee8cc64fc51aef2c5bd5` |
| blight | src10.png | `59933cc5c85f7776e9cee156a5bfd3f00cb7c98a0d771dd9c4fbd3f98bc48cbb` |
| delirium | src11.png | `0bab8deada7612260cabb86a29cb3e0141c405542049ef113de1fdb860cc3bd7` |

## Name verification

Fetched once on 2026-11-08 with `ExileSight/1.1 layout-research` User-Agent from
`https://poe.ninja/poe1/api/economy/exchange/current/overview?league=Standard&type=TYPE`.
Only item name/ID pairs are retained in the test fixture `measured-names.json`;
response image URLs were never fetched. The fixture records the API type, page,
exact name and ID for each included cell. It is historical evidence, not a promise
of price availability in every league. Refresh it alongside future layout changes.

| TYPE | Response SHA-256 |
|---|---|
| Currency | `71817ea7125cb597d6f7aece8a7fe8d0575ea8522d1d135b7a7f0ae9c9510e67` |
| Fragment | `6d3fdf837803e287253da0694a1b164c7830fad8ad5cf78ac3586372a3cf987d` |
| Fossil | `186c22f705b467aa20f628b0cedf2ae03a84b75ba3d91e08cfe57d3094ad917a` |
| Resonator | `a73d05a4914821b1fd3957cd35f14a6222e5646bbd80eb40f2c18723ae3084f1` |
| Essence | `53db17057d1f388d1cba020bbeb0adc7454419b24d558f12db1d7d9767774de9` |
| Oil | `4f042f3da1a34739badda8157951217663f26580671e3898c45c913faf9b2995` |
| DeliriumOrb | `aea82e75e08c38b2619af1cdc73c73a6bcd1f5469da96050bc40b97abf23f480` |
| AllflameEmber | `cc20f3c9fe72822e4049068ffefa0289a85baf93b70604079599404063beaade` |
