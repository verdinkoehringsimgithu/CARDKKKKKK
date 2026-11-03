---
name: anti-patchwork-engineering
description: Reassess a failing implementation when repeated patches, hardcoded overrides, or shared-state changes suggest a faulty design assumption.
---

# Reassess the Mechanism

1. Reproduce the failure and trace the responsible data flow, lifecycle, or physical interaction. Identify the assumption contradicted by evidence.
2. After two unsuccessful parameter or special-case patches, gather new evidence before another edit. Retain a sound design; replace it only when evidence shows it cannot satisfy the requirement.
3. Fix the responsible mechanism at the narrowest useful boundary. Preserve configuration ownership, shared-asset contracts, and unrelated behavior. Do not reset serialized user settings at startup to hide a bug.
4. Choose engine or framework primitives appropriate to the actual problem. Raycasts, contacts, materials, and velocity changes each have valid uses; verify their behavior in the target engine instead of treating a past example as a universal rule.
5. Validate the failing scenario and affected integration boundary. Report what ran and any remaining gap. A standalone probe does not establish that the feature works in the application.

Keep redesign proportional to the defect. A failed patch is a reason to revise the hypothesis, not permission for unrelated architectural work.
