# Spellcraft original art generation record

Date: 2026-11-05 (Asia/Shanghai)

Mode: built-in image_gen.imagegen only. SpriteCook MCP credit balance was checked once at task start and was 0. No SpriteCook generation, CLI, external API key, Python drawing, or programmatic image editing was used. All delivered PNGs preserve their native generated pixels and alpha. Source outputs remain under the Codex generated_images directory.

The brief calls for original cute clockwork-alchemy art with thick dark teal-blue outlines, rounded exaggerated forms and two to three clean color levels. The palette is teal navy, cream, coral, amber and lavender. Existing franchise characters, borders and snow themes were excluded.

## Atlas first generation — rejected for cell-edge clearance

Tool: image_gen.imagegen; transparent_background: true; no reference images.

Source: C:/Users/Administrator/.codex/generated_images/01a1a991-0875-75c2-bc3b-795ebf9ccab4/exec-d40bf82c-d90f-481b-adc8-4b719178cf87.png

```text
Use case: stylized-concept.
Asset type: production-ready 2D game sprite atlas, one square transparent PNG, ideally 2048 by 2048 pixels.
Primary request: Draw EXACTLY sixteen original charming clockwork-alchemy game objects arranged in a STRICT 4 columns by 4 rows equal-sized grid. Each cell contains exactly ONE complete isolated sprite, centered in its own square, with generous transparent padding all around (at least 12% of each cell); no sprite may touch or cross a cell boundary. The grid is invisible. Every sprite is large and legible with its own strong silhouette. No captions, numbers, lettering or UI.
Art direction: original hand-drawn playful indie card-battler art, bold thick dark teal-blue outlines, slightly uneven lively ink contour, exaggerated rounded chunky forms, cute expressive characters, flat opaque solid colors, only 2–3 crisp shade shapes per material, tiny restrained highlights, graphic and readable at thumbnail scale. Cohesive palette throughout: deep teal navy #173B4B, warm cream #FFF1CF, coral orange #EF755C, amber yellow #F3BC4F, pale lavender #B9A5E3, cool teal #4D9DA5. Fresh imaginative workshop world. NOT existing franchise characters or copied designs. NO snow motif.
Cell content, strict reading order from left to right then top to bottom:
ROW 1: (0) cheerful young inventor, full-body small chibi, big round amber goggles pushed up on head, tousled cream hair, short BLUE cape, chunky mechanic boots, coral glove, compact handheld wrench; (1) scrap-metal beetle, squat friendly six-legged navy and teal mechanical beetle, cream rounded scrap shell, one amber headlamp and small mismatched metal plates; (2) mirror-wing moth, distinct cute lavender moth with four large translucent-looking violet GLASS wing panels, navy glass outlines and simple white reflections, tiny cream furry torso and antennae; (3) storm stone golem, very chunky squat teal-grey stone body, huge rounded block fists, small head, one glowing ORANGE reactor in its chest, 2 simple lavender lightning shapes close to body.
ROW 2: (4) clockwork spark cannon, short wide navy barrel pointed diagonally up-right, chunky coral carriage and one amber cog wheel, a tiny zigzag spark at muzzle; (5) rounded energy shield, a complete plump oval navy handheld shield, thick cream rim, cool teal center and one simple amber lightning emblem shape; (6) spark battery, squat cream cylinder with coral caps and small navy lightning motif, two chunky amber terminals, one tiny contained spark; (7) ember bottle, a round little glass potion bottle with navy thick outline, lavender glass, orange glowing liquid and 2 floating simple amber embers inside, cream stopper.
ROW 3: (8) frost crystal, compact cluster of THREE pale cyan and lavender faceted ice crystals, broad simple facets, thick navy outline; (9) energy siphon coil, a squat deep navy horseshoe device wrapped with thick pale-lavender coil loops around a teal core, bright amber energy bead visibly drawn toward open center; (10) repair mechanical glove, one large rounded cream and coral articulated mechanic gauntlet, palm facing viewer at three-quarter angle, broad navy segmented fingers, tiny wrench at wrist; (11) clockwork clock, round cream alarm clock with two coral bells, navy thick outline, amber single gear visible at lower edge, navy hands but NO numerals or writing.
ROW 4: (12) cracked breastplate, chunky cream metal chest armor with dark teal straps and a prominent coral-orange jagged crack in center, distinct empty armor silhouette; (13) branching copper pipe, one Y-shaped coral copper pipe with three thick cream-rimmed open ends, simple teal shadow bands, rounded elbow; (14) pair of resonance bells, TWO chunky small amber bells together as one grouped sprite, tilted toward each other, navy handles, simple lavender echo arcs close beside bells; (15) overload reactor, compact round navy spherical machine, thick cream support braces, wide glowing coral-orange center with amber angular energy swirl, two squat lavender vents, readable powerful silhouette.
Transparency: genuinely transparent alpha background everywhere outside sprites, including between cells. NO white, colored, checkerboard, grey or black painted background. No cast floor shadows, no ground, no platform, no frame, no labels, no border. Objects must remain fully intact, include all protrusions, and stay well clear of all four edges of their own cell.
Avoid: realism, 3D render, photo textures, gradients, thin detailed etching, brown parchment, ornate gold filigree, copied characters, words, symbols resembling letters.
```

## Atlas targeted correction — delivered

Tool: image_gen.imagegen; transparent_background: true; referenced_image_paths: the first generated atlas above.

Source: C:/Users/Administrator/.codex/generated_images/01a1a991-0875-75c2-bc3b-795ebf9ccab4/exec-5afd2683-6c69-4534-b540-a1a0dee43299.png

```text
Use case: precise-object-edit. Edit the referenced original 16-sprite clockwork alchemy atlas into a production-ready atlas. Keep EXACTLY the same sixteen designs, same order, same friendly thick navy-outline hand-drawn style, same cream / coral / amber / teal / lavender palette. Change only sprite size and placement within the sheet. The current sprites are TOO LARGE for safe regular slicing.
CRITICAL LAYOUT: a perfect square, strict 4 columns by 4 rows of equal square cells; exactly one original sprite centered in each of the sixteen cells. Each entire sprite INCLUDING sparks, echo arcs, loose lightning and every protrusion must fit inside the central 68% of its own cell's width and height. Leave at least 16% transparent padding on EACH SIDE of EVERY cell. All sprites must be clearly separated from every other sprite by a wide transparent gutter. The whole outer border of the atlas must be at least 5% transparent. No object or decoration may reach a cell boundary. Small objects should not become oversized.
Request a 2048×2048 PNG, if the output tool chooses a different square resolution keep the exact equal four-by-four layout and the same padding proportions. No painted grid lines. No new objects. No text or numbers. Genuinely transparent alpha background, preserve opaque solid sprite interiors and clean antialiased contour edges. No background color, no checkerboard image, no cast shadows. Do NOT redraw as realistic or 3D. Do not introduce paper texture, ornate borders or gold filigree.
Reading order must remain: inventor, scrap beetle, mirror-wing moth, storm stone golem; spark cannon, energy shield, spark battery, ember bottle; frost crystal, siphon coil, repair gauntlet, clockwork clock; cracked breastplate, Y copper pipe, paired resonance bells, overload reactor.
```

## Workshop background — delivered

Tool: image_gen.imagegen; transparent_background: false; referenced_image_paths: D:/wenjian/cardkkk/Assets/TestGameplay/Spellcraft/Art/SpellcraftAtlas.png (style reference only).

Source: C:/Users/Administrator/.codex/generated_images/01a1a991-0875-75c2-bc3b-795ebf9ccab4/exec-491a8f80-2b2c-4319-9cf5-4a1332811ce6.png

```text
Use case: stylized-concept.
Asset type: wide 16:9 background illustration for a 2D spell-assembly card game battle and workshop board. Aim for 1920×1080 or a similar wide landscape ratio.
Input image: reference only for drawing style, shape language and palette; do not recreate its sprite grid, characters, objects, or transparent background.
Primary request: an original cozy clockwork-alchemy workbench viewed almost straight down, a wide abstract teal-blue tabletop with very broad simple surface shapes. The CENTER 70% of the entire image must stay OPEN, calm, unmarked and low contrast for game cards and UI. A large smooth cool medium-deep teal field dominates the image. Keep low local contrast throughout the play area; no central object or bright focal point.
At the four far corners and along just a little of the outermost 12% of the image, show a FEW softly subordinate workshop shapes cropped by the canvas: upper left a round lavender glass vessel with faint amber liquid and chunky cream tube; upper right a small cream-and-teal clockwork pressure gauge with a short coral pipe; bottom left two chunky teal tools and one closed little cream parts tin; bottom right a small squat navy-and-cream power coil with a restrained amber light. Four small corner clusters, much quieter and darker than the gameplay sprites. No people, no creatures.
Style/medium: match the reference's original playful hand-drawn indie card game art, thick deep teal-blue outlines, slightly irregular lively contour, rounded chunky forms, flat opaque solid colors and only 2–3 clearly defined clean shade shapes. Graphic illustration, no realistic texture. The background is atmospheric but simple. Corners can be slightly darker while the wide central tabletop remains an even teal. Use a broad gentle warm amber color shape from upper left and muted lavender-cool shade at lower right, with no visible ray or high contrast effects.
Palette: deep teal navy #173B4B, cool tabletop teal #396A76 and #487B86, cream #FFF1CF used sparingly at edges, muted coral #D67D68, amber #DCA855, lavender #A99ABC. No brown.
Constraints: landscape horizontal composition, edge-to-edge fully opaque illustration, no transparency needed. NO lettering, numbers, labels, logos, watermarks, UI, card frames, cards, circular placement targets, grid, slots, portraits, decorative center sigils, floorboards, snow, parchment, ornate gold filigree, realistic 3D materials or photo texture. The center must stay clean for actual gameplay.
```

## Reusable card frame — delivered

Tool: image_gen.imagegen; transparent_background: true; referenced_image_paths: D:/wenjian/cardkkk/Assets/TestGameplay/Spellcraft/Art/SpellcraftAtlas.png (style reference only).

Source: C:/Users/Administrator/.codex/generated_images/01a1a991-0875-75c2-bc3b-795ebf9ccab4/exec-e6abcbf8-202b-412b-b429-868cf5df9660.png

```text
Use case: stylized-concept.
Asset type: ONE reusable portrait card-frame sprite for a 2D clockwork-alchemy card game, designed to be sliced in Unity with 9-slice scaling. A single flat isolated card panel on a genuinely transparent alpha background. Aim for 1024×1536 or equivalent 2:3 portrait ratio.
Input image: style reference only. Match its playful thick dark teal-blue ink outlines and clean solid cream/teal palette. Do not draw any of the reference's characters or objects.
Primary request: a simple beautiful chunky card FRAME with SOLID OPAQUE warm cream interior. The card is a tall softly rounded rectangle. It fills about 92% of the canvas width and 94% of the height, is centered exactly, and has a small even transparent outer margin on all sides. A bold clean deep navy #173B4B outer outline, uniform rounded corners and a narrow cool-teal inner rim. Interior main area is a flat untextured warm cream #FFF1CF. The upper 70% of the entire card is blank cream with enough room for a title and a large illustration to be placed later; do not draw a title bar, inset illustration frame or any illustration. The lower 28% is a single solid deep navy #173B4B information panel, separated from cream above by ONE straight clean horizontal edge. This dark lower panel extends cleanly to the same card's inner sides and rounded bottom corners. It will hold light text added later, so leave it completely empty.
Art direction: original friendly indie card game UI, slightly lively hand-drawn contour but geometrically stable straight sides for 9-slice, bold dark outline, softly rounded chunky proportions, flat solid colors, at most one narrow teal shade along the right interior rim. Clean and deliberate. No paper grain or surface texture. No drop shadow outside the card, no glow. No perspective, no tilt; straight-on orthographic flat UI asset.
9-slice requirements: long vertical straight edges and straight horizontal interior division; no ornaments crossing the stretchable center; keep all corner curves and rim features within the outer 12% left/right and 8% top, with the dark information panel entirely within the bottom 32%. Every corner and edge is complete within the image. Outside the outer card silhouette is actual transparent alpha. INSIDE the card all areas, including the cream center, are fully OPAQUE; do NOT make the illustration region transparent.
Constraints: exactly one card frame, no text, letters, numbers, symbols, badges, screws, gears, circles, icons, portraits, artwork, scene, slots, extra card, examples, UI screenshot, checkerboard backdrop, painted white backdrop, parchment, brown paper, gilded gold, floral details, filigree, ornate border, realistic 3D materials or photo textures.
```
