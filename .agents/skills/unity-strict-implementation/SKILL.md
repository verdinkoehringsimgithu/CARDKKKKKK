---
name: unity-strict-implementation
description: Implement requested Unity features while preserving the project's architecture, supplied design, and UI content.
---

# Unity Strict Implementation

- Implement the requested behavior in the existing Unity project using its scenes, prefabs, components, and packages. Use another platform only when the user requests it.
- Inspect the relevant hierarchy, scripts, prefabs, and dependencies before editing. Reuse existing systems; add scenes, managers, or architecture only when required by the requested feature. Preserve unrelated assets and user changes.
- Treat supplied mechanics, references, and UI text as the design boundary. Do not invent gameplay rules, rewards, player counts, promotional text, decorative assets, or extra systems. Reference images specify visible appearance, not hidden interactions or backend behavior.
- Reuse supplied text and assets. Clearly mark necessary placeholder content; do not present mock data, static images, or temporary implementations as working final behavior. Prototypes are appropriate when requested.
- Resolve routine implementation details from project conventions. Ask when missing information changes gameplay, UI meaning, data contracts, or core architecture; continue independent work while that decision is pending. Existing user authorization remains valid. Automated hooks do not supply a missing human decision.
- Check the changed feature in its real scene or integration path when available, including affected serialization and lifecycle behavior. Report which checks ran and any runtime limitations.
