# -*- coding: utf-8 -*-
import os, sys, re

sys.stdout.reconfigure(encoding='utf-8')

def find_workspace_root():
    curr = os.path.abspath(os.getcwd())
    while True:
        if os.path.exists(os.path.join(curr, "CODEBASE_MAP.md")):
            return curr
        parent = os.path.dirname(curr)
        if parent == curr:
            break
        curr = parent
    return os.path.abspath(os.getcwd())

WORKSPACE_ROOT = find_workspace_root()
MAP_FILE = os.path.join(WORKSPACE_ROOT, "CODEBASE_MAP.md")
UI_DIR = os.path.join(WORKSPACE_ROOT, "Extracted_MapUnitWar_UI")
SCRIPTS_DIR = os.path.join(WORKSPACE_ROOT, "Scripts")

def verify_map():
    """Verify that all referenced paths in CODEBASE_MAP.md physically exist."""
    if not os.path.exists(MAP_FILE):
        print(f"[ERROR] Map file not found: {MAP_FILE}")
        return False
    
    with open(MAP_FILE, "r", encoding="utf-8") as f:
        content = f.read()

    # Find file:/// links
    links = re.findall(r'file:///([^\)\s]+)', content)
    missing = []
    for link in links:
        # Normalize windows path
        norm_path = os.path.normpath(link.replace('/', os.sep))
        if not os.path.exists(norm_path):
            missing.append(norm_path)
    
    if missing:
        print(f"[WARN] Found {len(missing)} dead paths in CODEBASE_MAP.md:")
        for m in missing:
            print(f"  - {m}")
        return False
    else:
        print(f"[OK] All {len(links)} linked physical paths in CODEBASE_MAP.md exist on disk!")
        return True

def sync_ui_counts():
    """Sync file counts in the Extracted_MapUnitWar_UI section of CODEBASE_MAP.md."""
    if not os.path.exists(MAP_FILE) or not os.path.exists(UI_DIR):
        return
    
    with open(MAP_FILE, "r", encoding="utf-8") as f:
        content = f.read()

    updated = content
    for d in os.listdir(UI_DIR):
        full = os.path.join(UI_DIR, d)
        if os.path.isdir(full):
            actual_count = len([f for f in os.listdir(full) if os.path.isfile(os.path.join(full, f))])
            # Pattern: | **[`<dir>`]...** | <count> |
            pattern = re.compile(rf'(\|\s*\*\*\[`{re.escape(d)}`\].*?\*\*\s*\|\s*)(\d+)(\s*\|)')
            if pattern.search(updated):
                updated = pattern.sub(rf'\g<1>{actual_count}\g<3>', updated)

    if updated != content:
        with open(MAP_FILE, "w", encoding="utf-8") as f:
            f.write(updated)
        print("[OK] Synchronized folder counts in CODEBASE_MAP.md successfully.")
    else:
        print("[OK] Folder counts in CODEBASE_MAP.md are already up to date.")

if __name__ == "__main__":
    print(f"=== CODEBASE_MAP Sync Utility ===")
    print(f"Workspace: {WORKSPACE_ROOT}")
    sync_ui_counts()
    verify_map()
