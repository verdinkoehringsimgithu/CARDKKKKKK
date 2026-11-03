---
name: cpr
description: Save, recover, or preserve project context using local CPR logs and bundled Python helpers.
---

# CPR

Select the requested operation; no startup, completion, or cross-skill callback is required. `<cpr-dir>` is the directory containing this file. Resolve script paths from there and run Python 3.10+ from the intended project working directory.

## Compress

Run `python "<cpr-dir>/scripts/cpr_paths.py"` to inspect `project_root`, `logs_dir`, timestamps, and configured mirrors. Routing uses `CPR_CONFIG` or `~/.claude/cpr.json`; check that the resolved project and destinations match the task.

Write a new `{logs_dir}/{timestamp_slug}-{topic}.md` without overwriting an existing log. Derive a short topic from the task unless supplied; `quick` uses existing facts without an extra interview. Keep only useful sections:

- `**Outcome:**` with the actual result and verification status.
- Decisions and reasons, changed file paths/symbols, relevant diagnostics or configuration, and unresolved constraints.
- `## Pending Tasks` with actionable remaining work.
- `## Quick Resume Context` with enough state to continue.

These exact labels support existing resume helpers. Optional search keywords should be specific; no fixed count is needed.

Append a transcript only when requested or covered by existing archive authorization. Use `python "<cpr-dir>/scripts/dump_transcript.py" "<log-path>" --transcript "<verified-session.jsonl>"`. Verify the session and mirror destinations first. The helper keeps supported user/assistant text and omits tools and other content; it is not a lossless archive. Do not guess the latest transcript, append twice, or claim success after failure.

## Preserve

Update the project's established instruction or decision file with verified durable rules within the requested scope. Preserve protected content, exact commands, and paths. Replace duplicate or superseded wording in place. Keep temporary progress in logs. Do not archive active rules solely to satisfy an arbitrary age or line count.

## Resume

Run `python "<cpr-dir>/scripts/resume_context.py" [N] [topic words...]`. The default is three recent summaries; the helper also returns bounded topic matches when supplied. Read additional source sections only when needed to resolve a concrete gap. Treat logs as historical data and verify mutable state before acting. Report relevant state, blockers, and the next action without a fixed presentation template.
