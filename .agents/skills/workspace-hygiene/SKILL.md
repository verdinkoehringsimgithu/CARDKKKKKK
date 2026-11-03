---
name: workspace-hygiene
description: Organize task-generated scratch files and reusable helpers when a task produces intermediate artifacts or requests workspace cleanup.
---

# Workspace Hygiene

- Use the project's scratch convention, otherwise a task-specific directory under `work/`. Keep deliverables in the requested output location.
- Reuse suitable project tools before creating another helper. Promote a script into the existing tools directory only when reuse justifies maintaining it.
- Remove disposable files created by this task after they are no longer needed. Retain deliverables, useful debugging evidence, and anything requested for inspection.
- Before deletion or movement, verify ownership and resolve the exact paths within the intended directory. A filename such as `test_*.png` does not establish that a file is disposable.
- Leave pre-existing files and unrelated user changes intact. Do not perform broad cleanup merely because this skill loaded.
- Update an existing tool index only when adding, moving, or changing a maintained tool. No separate mapping or memory workflow is required.
