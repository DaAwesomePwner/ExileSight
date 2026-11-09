# Measurement attribution

Measured 2026-11-08 by ExileSight contributors from owner-provided screenshots.
The owner explicitly permitted screenshot use for measuring these layouts in chat.
Images were downscaled by the chat tool from a 3440x1440 game client to 2000x837.
The latter pixels are the reference coordinate system; do not rescale JSON back to
3440x1440. No screenshots or reference icons are redistributed.

The S1-X1 identification table supplied independently identified `high` cells.
These initial labels are now cross-checked against independent icons where available;
price availability does not determine inclusion. Incorrect atlas positions were corrected.
Four `verify` Essence cells were subsequently confirmed by visual comparison of the
owner screenshot's ghost silhouettes against pinned GitHub icons (see recheck).
Other `verify` and unknown cells remain excluded. No Exile-UI data or icons were consulted.
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
Item names, available IDs and identity evidence are retained in `measured-names.json`;
response image URLs were never fetched. The fixture records the API type, page,
exact name, confidence and optional ID/reference icon for each included cell.
A null ID means the high-confidence identity is retained without a current API entry. It is historical evidence, not a promise
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

## First fix-round verification

The current challenge league was discovered as **Allflame** through
`https://poe.ninja/poe1/api/data/index-state` on 2026-11-08. The same overview endpoint
was fetched with `league=Allflame` for all eight supported pricing types. Standard
responses above were reused from the local research cache. High-confidence cells
no longer depend on either league's listing. Four independently confirmed Essence
identities have exact API names; other uncertain cells remain excluded.

| Allflame TYPE | Response SHA-256 |
|---|---|
| Currency | `5c70d1ee64e6addbbae3d94926ca81450cc38c2cd5381b22636310618060faf9` |
| Fragment | `7b2a660948f4816079ed0ce6f1b2ca1323275d1dc4ba70b95b69cea1dd83bbe0` |
| Fossil | `d6a76551c93e86de186c98dd324f481c08db2d6901a9d2909dc3fda2a6455ad9` |
| Resonator | `150762a8cc955cf643e38a2d1fd176f7c0aa0c52445d2a2fca8c1ce8ea6ea3ae` |
| Essence | `11711856a927f8d07ee3f10421ea8c4795d1b51e9cbaa192d62b5d93b6dcc581` |
| Oil | `c8011b7486841c69d4c9afaba89ec7e7986168b0803ae7eb1a2e2ac564362116` |
| DeliriumOrb | `9d070ed86526e6686c51c1a28a643cc3b91b72e24a9f9f5e2ba73d03643ae464` |
| AllflameEmber | `15ea009c92dff8200c829bf4420d861b870f8d16b44f71fd83827705d1d2d8d3` |

Index-state SHA-256: `1a15c7f1e5d50e8febe252ee2cda846decb0907b71b713c58fdee4d3ffe85f7c`.

## Final icon audit

Each cell's evidence entry now records a pinned `referenceIcon` GitHub URL
(repo, revision and path), or a concrete `noReferenceIcon` reason. No images are
redistributed. The final audit corrects the initial atlas identification table:
Hunter's Exalted Orb is at (362,161), Warlord's at (407,205), and the silver spiked
orb at (451,161) remains unidentified and omitted.

Currency General was compared visually against PoE1 icons from
[casid/poe-stash-buddy at a2ecc1dc773afbd37f1bbd723857ed2b83ba0910](https://github.com/casid/poe-stash-buddy/tree/a2ecc1dc773afbd37f1bbd723857ed2b83ba0910/src/main/resources/currency).
All 25 cells match. Those references resolve artwork differences in Project-Vaal's
same-named currency icons; filename equality alone was not treated as evidence.
Atlas and remaining Fragment cells were compared against
[Jxycee/Project-Vaal at 87ddd63b7abf9dc9f3e37897818447830ebfc166](https://github.com/Jxycee/Project-Vaal/tree/87ddd63b7abf9dc9f3e37897818447830ebfc166/public/data/wiki/2026-08-25/icons/items).
The reviewer supplied independent confirmation for Essence, Delirium Orb, Oil,
Fossil, Legion splinter/emblem, crest, Eldritch ember/ichor, Lifeforce, Simulacrum
and Resonator cells; their exact reference paths were resolved against the pinned
GitHub inventories and recorded in the fixture. Atlas and fragment comparisons
also rechecked the available icons in those pages directly.

225 included cells have reference icons. Hivebrain Gland is the sole exception:
no icon was found in the checked inventories, so its original owner-screenshot
identification is retained with an explicit reason. It is not marked icon-confirmed.
