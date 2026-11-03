---
name: antigravity-skill-orchestrator
description: Select relevant skills when a task spans multiple specialties or skill selection is explicitly requested.
metadata:
  category: meta
---

# Skill Selection

- Use the available names and descriptions to select skills that add task-specific knowledge or tooling. Ordinary edits can proceed directly; honor explicitly requested skills.
- Load only the selected entrypoints and references needed for the current step. Do not preload general review, memory, navigation, or hygiene skills.
- A task spanning domains may need several skills; each must have a concrete role. Do not recursively invoke this selector or build a hierarchy among skills.
- Follow the host's instruction priority and the user's scope. Skills do not override either or grant additional permissions.
- Reuse a loaded skill without rereading it unless its content changed. Stop selection once the current work has enough guidance.
- Do not search memory or record skill combinations merely because this selector ran.
