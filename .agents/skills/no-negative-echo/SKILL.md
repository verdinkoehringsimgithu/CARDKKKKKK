---
name: no-negative-echo
description: Finalize artifacts after corrections or discarded drafts without carrying irrelevant session history into the result.
---

# Finalize from the Accepted Result

- Write from the accepted target and observed state. Describe current behavior in titles, filenames, comments, metadata, commits, and handoffs.
- Omit discarded session-only alternatives and explanations of their absence. Preserve exclusions needed for accuracy, safety, compatibility, or a requested comparison, audit, quotation, or migration account.
- Preserve unrelated user changes, required identifiers, and actual external events or failures. Rejected drafts are not the same as changes to a committed or user-approved baseline.
- Inspect the surfaces being delivered for irrelevant history and missing required facts. Recheck affected surfaces after edits or tool-generated changes; state material limits on what could be inspected.
- Use `scripts/check_surface.py` only when exact non-sensitive terms or filenames need checking. A zero-match result does not establish semantic correctness.

For sensitive content or explicitly requested strict validation, read [references/high-assurance-finalization.md](references/high-assurance-finalization.md). Ordinary drafting and editing use the rules above.
