# Make your game - free sample

This sample is a taste of Game-in-a-Box: Card Battler Duel Kit. The files here are a subset, so some sets are incomplete
(for example only one direction or one colour of a sprite).

- Strips named `_stripN.png` hold N frames side by side.
- Use the palette in `palette/` to keep any new art consistent.
- The full kit includes a step-by-step MAKE-YOUR-GAME.md guide for this genre, the complete sprite sets and mockups.

## Engine import tips (general guidance - no engine project files are included)
- **Godot 4:** set the default texture filter to *Nearest* (Project Settings > Rendering > Textures). Use `AnimatedSprite2D` + `SpriteFrames` and slice each `_stripN.png` into N frames, or use the sheet JSON rows. Tiles go into a `TileSet` with the tile size 16x16 unless the file name says otherwise.
- **Unity:** import PNGs with Filter Mode *Point*, Compression *None*, Pixels Per Unit = tile size. Slice sheets in the Sprite Editor (Grid By Cell Size). Use a Pixel Perfect Camera.
- **GameMaker:** create sprites from strips (Import Strip Image, frame count = the N in `_stripN`). Turn off texture interpolation in Game Options.
- Any engine: frame sizes, frame counts and suggested speeds are in the JSON files next to each sheet/strip.
