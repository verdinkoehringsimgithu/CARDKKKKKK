---
name: cpr-preserve
description: Preserve verified durable project decisions in the project's established instruction or decision file.
---

# CPR Preserve

Locate the project's existing rules or decision document, such as `AGENTS.md`, `AGENT.md`, `GEMINI.md`, or `CLAUDE.md`. Use its established role; do not create competing instruction files by default.

- Record only verified durable rules, decisions with necessary reasons, or hard-to-rediscover constraints within the requested scope.
- Update the relevant section in place. Preserve exact paths, commands, protected content, and unrelated edits; remove duplication without changing meaning.
- Keep temporary progress and completed task history out of active rules. Archive superseded material only when that is needed and within scope, retaining useful provenance and a discoverable pointer.
- Do not remove active requirements because a file exceeds a line limit or a record passes an age threshold.

Review the resulting diff for accidental policy changes. Report the updated path and substantive changes; no transcript export or secondary memory action is required.
