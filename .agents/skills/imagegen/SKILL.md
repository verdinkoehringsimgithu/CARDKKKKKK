---
name: imagegen
description: Generate or edit raster images, including reference-based variants and transparent cutouts. Use native source editing for existing SVG, vector, or code-rendered assets.
---

# Image Generation

Use the available built-in `image_gen` tool by default. Inspect local edit targets first; distinguish targets from style references and state which features must remain unchanged. Follow the live tool schema for image inputs, transparency and output handling.

- Preserve exact requested text, composition and identity constraints. Add prompt detail only when it improves the requested result; omit mandatory taxonomy and prompt templates.
- Request actual transparency when needed and preserve alpha. Use a separate prompt for each distinct asset; multiple variants may share a prompt.
- Inspect the result for the requested subject, text and edit invariants. Retry only to fix an observed defect, carrying the invariants forward.
- Use returned output locations, not assumed host directories. Copy project assets to the requested destination or workspace and update consumers. Preserve existing files unless replacement was requested. Preview-only images may remain at the tool's output location.
- Deliver images and saved paths. Include prompts or execution details when requested or needed to reproduce the result.

## Explicit CLI/API mode

Use bundled `scripts/image_gen.py` only when the user selects or confirms CLI/API execution. A batch request or tool failure alone does not authorize switching providers or billing paths. If the built-in tool is unavailable, explain the available fallback and its requirements.

Resolve scripts relative to this skill directory; adapt reference command examples to that location and the active shell. Read [CLI usage](references/cli.md) for `generate`, `edit` or `generate-batch`, and [API parameters](references/image-api.md) only for the options needed. The bundled CLI defaults to `gpt-image-2`; do not silently switch models. Do not modify the bundled generator during image work.

Live CLI calls require `OPENAI_API_KEY`, the `openai` package and network access. Ask users to configure missing credentials locally; never request or print secrets. Use explicit output paths. Check model support before passing transparency, masks, size or fidelity options; CLI parameters are not built-in tool parameters.

Read [prompting](references/prompting.md) or [examples](references/sample-prompts.md) only when prompt construction needs guidance. Read [network troubleshooting](references/codex-network.md) only for a CLI network failure; current host permissions govern execution.
