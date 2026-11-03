---
name: skill-creator
description: Create or update an agent skill with concise instructions and task-specific supporting resources.
metadata:
  short-description: Create or update a skill
---

# Skill Creator

Write instructions for a capable agent. Keep only task-specific decisions, operational constraints and resources that improve execution. Remove tutorials, motivational prose, repeated rules and compulsory workflows unrelated to the request.

## Structure

- `SKILL.md`: YAML `name` and `description`, then the essential workflow. Describe when the skill applies precisely enough to avoid unrelated activation. Preserve existing supported metadata.
- `references/`: substantial guidance needed only in certain modes. Link each reference with its read condition; keep each fact in one place.
- `scripts/`: reusable deterministic operations that justify maintained code.
- `assets/`: files consumed by generated output, not extra instructions.

Keep simple skills self-contained. Add no empty directories, README, changelog or duplicate quick reference unless required. Avoid invoking another skill unless the task truly depends on an available capability. Do not make every task load memory, orchestration or review skills.

## Update or create

Inspect the existing skill, callers and resources before changing them. Preserve the user's task, valid tool contracts, permissions and invocation policy. Respect the requested location; otherwise use the target agent's configured skills directory.

Use names of at most 64 lowercase letters, digits and hyphens; match the folder name. For a new skill, `scripts/init_skill.py <name> --path <parent>` can initialize files. Request `--resources` or `--examples` only when needed; do not reinitialize an existing skill.

Keep automatic discovery enabled unless the user requests explicit-only invocation. Read [UI metadata](references/openai_yaml.md) only when editing `agents/openai.yaml`. Its generator replaces the whole file, so preserve existing policy and dependencies when updating metadata.

Match specificity to risk: give open-ended work useful decision criteria; reserve fixed sequences for fragile operations. Define bounded retries where cost or side effects matter. Keep instructions in concise English; user-facing output follows the user's language.

Run `scripts/quick_validate.py <skill-directory>` for basic structure. Check referenced paths and tool availability. Run changed scripts and test meaningful behavior when warranted; syntax validation alone does not prove usefulness. Use independent scenario testing only when complexity or risk justifies it. Refine from observed failures rather than adding rules for speculative cases.
