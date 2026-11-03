# -*- coding: utf-8 -*-
import os, sys, shutil

sys.stdout.reconfigure(encoding='utf-8')

ROOT = os.path.abspath(os.getcwd())
TOOLS_DIR = os.path.join(ROOT, "Tools")
ARCHIVE_DIR = os.path.join(ROOT, "Archive_Scripts")

# 1. Disposable image patterns in root
def clean_scratch_images():
    count = 0
    for f in os.listdir(ROOT):
        p = os.path.join(ROOT, f)
        if os.path.isfile(p) and f.lower().endswith(".png"):
            os.remove(p)
            count += 1
    print(f"[OK] Cleaned {count} disposable scratch PNG images from root.")

# 2. Reusable tools mapping
REUSABLE_TOOLS = {
    # Extractors
    "extract_wall_frame_and_hud.py": os.path.join(TOOLS_DIR, "Extractors"),
    "clean_unified_popup_folder.py": os.path.join(TOOLS_DIR, "Extractors"),
    "consolidate_into_single_popup_folder.py": os.path.join(TOOLS_DIR, "Extractors"),
    # Parsers
    "dump_main_hud_trees.py": os.path.join(TOOLS_DIR, "Parsers"),
    "dump_all_sprites_list.py": os.path.join(TOOLS_DIR, "Parsers"),
    "parse_il2cpp_metadata.py": os.path.join(TOOLS_DIR, "Parsers"),
    # Analysis
    "search_all_game_files.py": os.path.join(TOOLS_DIR, "Analysis"),
}

def organize_and_archive_scripts():
    os.makedirs(os.path.join(TOOLS_DIR, "Extractors"), exist_ok=True)
    os.makedirs(os.path.join(TOOLS_DIR, "Parsers"), exist_ok=True)
    os.makedirs(os.path.join(TOOLS_DIR, "Analysis"), exist_ok=True)
    os.makedirs(ARCHIVE_DIR, exist_ok=True)

    moved_tools = 0
    archived = 0

    for f in os.listdir(ROOT):
        p = os.path.join(ROOT, f)
        if os.path.isfile(p) and (f.endswith(".py") or f.endswith(".ps1")):
            if f in REUSABLE_TOOLS:
                dest = os.path.join(REUSABLE_TOOLS[f], f)
                shutil.move(p, dest)
                moved_tools += 1
                print(f"[TOOL] Promoted {f} -> {dest}")
            else:
                dest = os.path.join(ARCHIVE_DIR, f)
                shutil.move(p, dest)
                archived += 1

    print(f"[OK] Promoted {moved_tools} reusable tools to Tools/.")
    print(f"[OK] Archived {archived} one-off historical scripts to Archive_Scripts/.")

if __name__ == "__main__":
    print("=== Executing Workspace Hygiene Cleanup ===")
    clean_scratch_images()
    organize_and_archive_scripts()
    print("=== Cleanup Complete ===")
