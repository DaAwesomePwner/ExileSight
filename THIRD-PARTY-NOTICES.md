# Third-party notices

ExileSight ports data and logic from the projects below. Each ported file or data pack names its source in a header comment or a `source` field.

## Exile-UI

https://github.com/Lailloken/Exile-UI. Ported data (map-info patterns, leveling guide, stash tab grids, mercenaries) and algorithms from the item-info and map-info features.

```
MIT License

Copyright (c) 2022 Lailloken

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

Exile-UI's own acknowledgements apply to data derived through it. The leveling guide was originally derived from [exile-leveling](https://github.com/HeartofPhos/exile-leveling); confirm its license before shipping that data. Do not redistribute data Exile-UI derived from poedb, or images (zone layouts, boss pictures), until their licensing has been checked. See docs/DECISIONS.md D-008.

## Path of Building Community

https://github.com/PathOfBuildingCommunity/PathOfBuilding (MIT). This is a possible source for mod and tier data in `tools/ExileSight.DataGen`. Add its license text here when data from it first ships.

## poe.ninja

Price data is fetched at runtime from https://poe.ninja. No poe.ninja data is redistributed in this repository.

## RePoE (repoe-fork)

Source: https://github.com/repoe-fork/repoe-fork.github.io. The default export is pinned to `d0bc438357d849bb93ad22f3db0b12938aaf6da1` (3.29.3.4); each pack manifest records the actual source SHA and per-input SHA-256. DataGen consumes its base_items and mods exports. Game data © Grinding Gear Games; published with the owner's approval per D-047 as ExileSight GitHub release assets, generated in CI. Only synthetic fixtures are checked in; generated game data is never committed. RePoE code is MIT licensed (notice below); that license does not grant rights to the generated game data.

This product isn't affiliated with or endorsed by Grinding Gear Games in any way.

Upstream code notice (https://github.com/repoe-fork/repoe/blob/master/LICENSE.md):

MIT License

Copyright (c) 2016 brather1ng

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

------------------------------------------------------------------------------

Contents of generated files (all files in the `data` directory) are owned
by Grinding Gear Games and shall not be used or published without being in
accordance with their terms of use.
