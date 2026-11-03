---
name: cpr-compress
description: Save a resumable CPR session summary for a handoff or context compaction.
---

# CPR Compress

Resolve `<cpr-dir>` to the sibling `cpr` directory. Run Python 3.10+ from the intended project working directory.

1. Run `python "<cpr-dir>/scripts/cpr_paths.py"`. Check the resolved project, log destination, and mirrors; routing comes from `CPR_CONFIG` or `~/.claude/cpr.json`.
2. Write a new `{logs_dir}/{timestamp_slug}-{topic}.md`. Use the supplied topic or derive one; `quick` uses known facts without an extra interview. Avoid overwriting an existing log.
3. Keep the objective, accepted constraints, decisions/reasons, exact changed paths and symbols, verification results, and blockers. Include `**Outcome:**`, `## Pending Tasks`, and `## Quick Resume Context` for the existing resume helpers. Omit empty sections and redundant prose.

Append a transcript only when requested or already authorized: `python "<cpr-dir>/scripts/dump_transcript.py" "<log-path>" --transcript "<verified-session.jsonl>"`. Verify the session and configured mirror destinations before execution. The helper exports filtered user/assistant text, not a lossless record; do not select an arbitrary latest session or append the same transcript twice. Keep the summary and report a transcript-export failure accurately.

Return the saved path and material omissions. No rule-file update or other memory workflow is required.
