---
name: permissioned-github
description: Use gh and HTTPS git operations in a host with scoped GitHub permission gates; resolve authorization failures without bypassing the gate.
---

# Permissioned GitHub

Use `gh` for GitHub operations and `git` over HTTPS for remote branch/content operations. Select the intended repository explicitly with `-R ORG/REPO` on repository-scoped `gh` commands; organization searches use `--owner`. Follow the host's restrictions on output redirection. Do not replace blocked commands with direct API scripts, curl or another transport.

Act within the user's authorization. Attempt the authorized command first; request additional execution permission only after a permission failure. Diagnose authentication, network and syntax failures separately. Do not ask again for permission already granted in the session.

If the host exposes `ask_custom_permission`, request the smallest matching grant below, then retry the blocked command. Otherwise use the host's actual approval mechanism or report the missing capability; permission strings are not executable tools.

## Scoped grant format

`<binary>.<action>(<resource_json>)`

All grants require `org` and `repo`. Omit unused fields. Use `*` only for the required scope; object IDs are strings.

| Operation | Grant | Additional fields |
|---|---|---|
| PR read/create/update/approve/merge | `gh.<action>` | `pr`: number or `*` |
| Issue read/create/update | `gh.<action>` | `issue`: number or `*` |
| Clone, fetch, pull, checkout; code/commit search | `git.read` | `contents: "*"` (repository-wide) |
| First push of a branch | `git.create` | `branch`: name |
| Push to existing branch, including force push | `git.update` | `branch`: name |
| Delete remote branch | `git.delete` | `branch`: name |
| Dispatch workflow | `workflow.dispatch` | `path`: workflow file; `ref`: explicit branch/tag |
| List/view/watch workflow runs | `workflow.read` | `path: "*"`, `ref: "*"` |

PR and issue searches use `gh.read` with the corresponding object field `*`; organization-wide search also uses `repo: "*"`. PR/issue comments, edits, reviews, close and reopen use `update`; approving uses `approve`.

Workflow dispatch requires an existing file at the selected `--ref`, not a display name or numeric ID. Run reads require repository-wide scope because run IDs are opaque. This custom gate does not support reruns or listing/viewing workflow definitions; obtain definitions through an authorized content read. If an operation is unsupported by the gate, report that limitation without retrying through another route.

Example: `gh.update({"org":"myorg","repo":"myrepo","pr":"123"})` authorizes an update to that PR; it does not supply user intent to post a comment.
