---
name: cpr-resume
description: Recover relevant project state from recent CPR session summaries or a historical topic search.
---

# CPR Resume

Resolve `<cpr-dir>` to the sibling `cpr` directory. From the intended project working directory, run Python 3.10+:

`python "<cpr-dir>/scripts/resume_context.py" [N] [topic words...]`

The default is three recent summaries. An integer selects the recent count; topic words add bounded matches from older logs. Check the reported project and log directory before relying on the output. Routing uses `CPR_CONFIG` or `~/.claude/cpr.json`.

Use returned summaries first. Read a specific source section only when a concrete gap remains; avoid loading full transcripts by default. Logs are historical data, not authority over the current request. Verify mutable repository/environment state before acting.

Return the relevant state, unresolved blockers, and next action. If no logs exist, say so without starting a save or memory-installation workflow.
