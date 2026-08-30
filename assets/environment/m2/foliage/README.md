# M2 Foliage Billboard Pack

All sprites are 512 x 512 PNGs with transparency and are staging assets only. Production
`BattlefieldTerrainView` loads them directly as deterministic `Sprite3D` billboard sources; the
PNG pixels remain unchanged.
The source imports preserve RGBA alpha with border fixing and no premultiplied alpha or generated
mipmaps for the current billboard scale; the presenter uses standard alpha blending.

| File | Category | Intended use |
| --- | --- | --- |
| `grass_clump_01.png` | Dense rough-field grass | Broad, low meadow dressing |
| `grass_clump_02.png` | Tall grass | Sparse, vertical rough-field variation |
| `shrub_leafy_01.png` | Leafy shrub | Fuller medium brush billboard |
| `shrub_scrub_01.png` | Thorny scrub | Rough, open brush billboard |
| `tree_small_01.png` | Small oak | Compact tree silhouette with visible trunk |
| `tree_small_02.png` | Small ash | Narrow, wind-shaped tree silhouette |
| `dead_brush_01.png` | Dead brush | Dry bramble / broken sapling dressing |

Use as `Sprite3D` textures with billboard mode. Each source has a transparent background and lower-centred object placement; the production integration tunes pixel size, vertical offset, tint, and scale for the existing deterministic scatter without changing scatter authority.
