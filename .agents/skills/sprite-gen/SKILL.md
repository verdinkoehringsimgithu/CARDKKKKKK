---
name: sprite-gen
version: 2.5.2
description: Generate game sprites and image/video assets with the sprite-gen pipeline; extract, curate, recolor and export existing assets.
license: Apache-2.0
depends_on:
  required_bins:
    - name: codex
      why: "gen --provider codex (image_gen via ChatGPT OAuth)"
    - name: ffmpeg
      why: "video-frames (clip -> frames) and video-set"
    - name: img2webp
      why: "video-loop WebP with exact alpha (libwebp); Pillow's animated writer drops -exact"
  required_scripts:
    - scripts/prepare_sprite_run.py
    - scripts/generate_sprite_image.py
    - scripts/gen_set.py
    - scripts/generate_sprite_video.py
    - scripts/video_canvas.py
    - scripts/video_frames.py
    - scripts/video_loop.py
    - scripts/video_set.py
    - scripts/extract_sprite_row_frames.py
    - scripts/interpolate_frames.py
    - scripts/compose_sprite_atlas.py
    - scripts/preview_animation.py
    - scripts/compose_selected_cycle.py
    - scripts/compose_sprite_gif.py
    - scripts/inspect_sprite_run.py
    - scripts/score_sprite_run.py
    - scripts/run_correction_loop.py
    - scripts/curation.py
    - scripts/serve_curation.py
    - scripts/slice_sheet_cells.py
    - scripts/unpack_atlas_run.py
    - scripts/export_curated_pngs.py
    - scripts/recolor.py
    - scripts/compose_layers.py
modes:
  default: component-row
---

# Sprite Gen

Resolve `SPRITE_GEN_ROOT` to an actual installed sprite-gen repository. This skill folder contains documentation; verify the package, scripts and virtual environment before executing. Use the repository venv's absolute CLI/Python path (`.venv/bin/` on POSIX, `.venv/Scripts/` on Windows), not an arbitrary global interpreter. For missing dependencies or setup, read [interpreter](docs/interpreter.md); if the package is absent, report that blocker instead of substituting a different pipeline.

## Generate

For image/sprite generation, read [user workflow](docs/user-workflow.md) and run `sprite-gen workflow --kind image|sprite`. Pass choices already supplied. Resolve only missing choices or blockers; execute when the guide returns `ready`. Pass the chosen provider explicitly. Do not switch providers or subscription/API billing paths without the user's choice. The `openai` provider is a separately billed opt-in route. Preserve existing access confirmations and do not repeat answered questions.

Use the guide's pipeline and QA, then deliver checked files before optional curation. Use its finish stage with the actual result. Save defaults only when requested or agreed; request overrides do not modify defaults. Standalone video and direct asset utilities use their specific routes without image/sprite setup questions.

## Read the relevant route only

| Task | Commands | Reference |
|---|---|---|
| Image/edit | `gen` | [generation](docs/gen.md) |
| GPT image-row sprites | `prepare`, `gen-set --provider codex`, `extract`, compose/QA | [atlas](docs/atlas-workflow.md) |
| Video-derived sprites | `video-set` | [video pipeline](docs/video-pipeline.md) |
| Standalone video, continuation or editing | `video`, `video-extend`, `video-edit` | [video](docs/video.md) |
| Directional anchors | `anchor` | [anchors](docs/directional-anchor-workflow.md) |
| Candidate selection | `curation`, `unpack-atlas` | [curation](docs/curation.md) |
| Background removal/imported sheets | `cutout`, `slice-sheet` | [sheet slicing](docs/sheet-slicing.md) |
| Palette changes/rig layers | `recolor-palette`, `recolor`, `compose-layers` | [recolor](docs/recolor.md), [layers](docs/layer-tracks.md) |
| Breathing/export | compose, `export-aseprite`, `export-pngs` | [breathing](docs/breathing.md), [export](docs/engine-export.md) |
| Tiles, shadows, motion evidence | `background-tile`, `shadow`, `inspect-motion` | [asset tools](docs/asset-tools.md) |
| Scene composition | `scene-render`, `scene-inspect` | [scene](docs/scene.md) |

Use canonical extraction, alignment and export. Generated sprites use row/component extraction; grid slicing is for imported sheets, not a substitute generation pipeline. Read [run contract](docs/run-contract.md) for numeric requests, run layout, atomic publication and curated exports. Use `sprite-gen --help` for actual command availability; [docs index](docs/README.md) covers specialized features.

Scene specs own placement, scale, playback and lighting; asset metadata owns frames, durations and anchors. Verify stride with declared same-foot contact and an isolated foot region. Apply a report only to its exact selected asset with an explicit scene direction; unknown contact remains unverified. Do not reverse frames or modify source assets merely to fit a scene.
