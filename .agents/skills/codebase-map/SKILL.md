---
name: codebase-map
description: Use or maintain an existing CODEBASE_MAP.md for repository navigation, or create a map when requested.
---

# Codebase Navigation

- Resolve `CODEBASE_MAP.md` from the target project root. It is not relative to this global skill directory.
- Use relevant existing map entries or asset indexes when they shorten discovery. Verify a mapped path before relying on it.
- If no map exists or an entry is stale, use scoped `rg --files` and `rg` searches. Expand the scope only when narrower searches fail; do not reread a whole tree for a known path.
- When maintaining a map, record verified paths, responsibilities, representative assets, and reusable commands. Keep plans and speculative entries elsewhere.
- Correct affected entries after path changes. Add new entries when explicitly requested or when the project maintains this map as part of its workflow; do not create documentation for every lookup.
- Prefer project-relative paths and existing indexes over repeated parsing of large asset bundles.

`scripts/sync_map.py` is a legacy helper for `Extracted_MapUnitWar_UI`. It updates matching count rows and checks `file:///` links; it is not a general map validator. Use it only for that layout after inspecting its targets, and inspect the resulting diff and warnings. It does not report validation failure through its exit status.
