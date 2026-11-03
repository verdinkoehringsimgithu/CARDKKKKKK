---
name: context-compression
description: Produce a compact, resumable task handoff when context is being shortened or work is transferred.
---

# Context Compression

Summarize when a handoff or actual context limit requires it. Use available state rather than estimating an invisible token threshold. Update an existing summary when possible; avoid a separate logging or memory cycle.

Retain what the next executor needs:

- The objective, accepted constraints, user authorizations, and current status.
- Decisions and their relevant reasons; distinguish confirmed facts from assumptions.
- Created or changed file paths, affected symbols, and the nature of each change. Include read-only references only when they prevent costly rediscovery.
- Exact identifiers, commands, configuration keys, and diagnostic text needed to continue. Exclude secrets and trim irrelevant output.
- Completed verification and its limits; pending tasks, blockers, and the next concrete action.

Remove repetition, obsolete plans, conversational framing, and reproducible detail that can be replaced by a precise pointer. Preserve unresolved issues and prior failures that would otherwise be repeated.

Check that another executor could locate the work, distinguish completion from intention, and continue without repeating finished steps. Return the summary in the requested location or format; saving files or editing project rules is a separate action.
