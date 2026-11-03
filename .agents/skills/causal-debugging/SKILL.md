---
name: causal-debugging
description: Diagnose reproducible bugs or unexplained behavior before changing the responsible implementation.
---

# Causal Debugging

1. Establish expected behavior, observed behavior, and reproduction conditions.
2. Locate the first divergence and the code or system that owns the affected state. Check relevant data flow, lifecycle timing, configuration, and dependencies.
3. Use a focused log, inspection, or experiment to distinguish plausible causes. If a change fails, revise the hypothesis before trying another variation.
4. Correct the responsible mechanism with a proportionate change that preserves surrounding contracts.
5. Reproduce the original scenario and check affected adjacent paths. Report the supported cause, change, observed result, and material gaps; distinguish static inspection from runtime verification.
