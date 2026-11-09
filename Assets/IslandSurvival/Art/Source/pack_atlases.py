"""按固定格生成 Unity Sprite 元数据，仅更新对应卡牌的 Artwork 引用。

不修改图像像素。需要 Pillow 读取尺寸与透明通道；从项目目录运行或直接运行本文件均可。
"""
from pathlib import Path
import hashlib
import json
import re
import uuid
from PIL import Image

ROOT = Path(__file__).resolve().parents[4]
ART = ROOT / "Assets/IslandSurvival/Art"
CARDS = ROOT / "Assets/IslandSurvival/Configuration/Cards"
SHEETS = [
    ("IslandMaterialsAtlas.png", 4, 4, [
        "stone", "wood", "fiber", "flint", "small_shell", "large_shell", "limpet", "shell_meat",
        "aloe_leaf", "gel", "bandage", "simple_dressing", "algae", "organic", "ash", "debris"]),
    ("IslandFoodToolsAtlas.png", 4, 4, [
        "ration", "cooked_shell", "yam", "cooked_yam", "snail_meat", "cooked_snail", "dried_meat", "yam_seed",
        "aloe_bud", "bone_piece", "charcoal", "shell_knife", "flint_knife", "bone_knife", "shell_ornament", "fertilizer"]),
    ("IslandEcologyAtlas.png", 4, 4, [
        "limpet_rock", "wild_aloe", "coast", "bush", "campfire", "garden", "pen", "compost",
        "storage", "drying_rack", "yam_plant", "aloe_plant", "snail", "young_snail"]),
    ("IslandConditionsAtlas.png", 3, 2, [
        "condition_wound", "condition_bleeding", "condition_infection", "condition_pain", "condition_irritation", "condition_hunger"]),
]


def stable_guid(path):
    return uuid.uuid5(uuid.NAMESPACE_URL, "cardkkk/island-survival/" + str(path).replace("\\", "/")).hex


def plain_meta(path, folder=False):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    relative = path.relative_to(ROOT).as_posix()
    text = "fileFormatVersion: 2\nguid: " + stable_guid(relative) + "\n"
    if folder:
        text += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    else:
        text += "DefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n"
    meta.write_text(text, encoding="utf-8", newline="\n")


def texture_meta(path, width, height, columns, rows, ids):
    # Reuse this project's actual Unity 2022 TextureImporter settings, replacing all identity/slice data.
    template = (ROOT / "Assets/TestGameplay/Spellcraft/Art/SpellcraftAtlas.png.meta").read_text(encoding="utf-8")
    settings = template[template.index("  externalObjects:"):template.index("  spriteSheet:")]
    path_guid = stable_guid(path.relative_to(ROOT).as_posix())
    result = ["fileFormatVersion: 2", "guid: " + path_guid, "TextureImporter:", "  internalIDToNameTable:"]
    sprites = []
    for index, card_id in enumerate(ids):
        file_id = 21300000 + index * 2
        result.extend(["  - first:", "      213: " + str(file_id), "    second: " + card_id])
        row, column = divmod(index, columns)
        rect = {"x": width * column / columns, "y": height * (rows - row - 1) / rows,
                "width": width / columns, "height": height / rows}
        sprites.append({"id": card_id, "file_id": file_id, "row": row, "column": column, "rect": rect})
    result.append(settings.rstrip("\n"))
    result.extend(["  spriteSheet:", "    serializedVersion: 2", "    sprites:"])
    for sprite in sprites:
        result.extend(["    - serializedVersion: 2", "      name: " + sprite["id"], "      rect:", "        serializedVersion: 2"])
        result.extend("        " + key + ": " + str(value) for key, value in sprite["rect"].items())
        result.extend([
            "      alignment: 0", "      pivot: {x: 0.5, y: 0.5}", "      border: {x: 0, y: 0, z: 0, w: 0}",
            "      outline: []", "      physicsShape: []", "      tessellationDetail: -1", "      bones: []",
            "      spriteID: " + stable_guid(path.name + "/" + sprite["id"]),
            "      internalID: " + str(sprite["file_id"]), "      vertices: []", "      indices:", "      edges: []", "      weights: []"])
    result.extend(["    outline: []", "    physicsShape: []", "    bones: []", "    spriteID:", "    internalID: 0",
                   "    vertices: []", "    indices:", "    edges: []", "    weights: []", "    secondaryTextures: []", "    nameFileIdTable:"])
    result.extend("      " + s["id"] + ": " + str(s["file_id"]) for s in sprites)
    result.extend(["  mipmapLimitGroupName:", "  pSDRemoveMatte: 0", "  userData: 孤岛生存原创插画，内置image_gen生成，固定格切片",
                   "  assetBundleName:", "  assetBundleVariant:"])
    Path(str(path) + ".meta").write_text("\n".join(result) + "\n", encoding="utf-8", newline="\n")
    return path_guid, sprites


def main():
    card_paths = {}
    card_texts = {}
    for path in CARDS.glob("*.asset"):
        content = path.read_text(encoding="utf-8-sig")
        match = re.search(r'^  Id: "([^"]+)"$', content, re.MULTILINE)
        if match:
            card_paths[match[1]] = path
            card_texts[match[1]] = content
    all_ids = [card_id for _, _, _, ids in SHEETS for card_id in ids]
    if len(all_ids) != len(set(all_ids)) or set(all_ids) != set(card_paths):
        raise ValueError("图集 ID 与真实卡牌目录不一致：" + repr(set(all_ids).symmetric_difference(card_paths)))
    manifest = {"date": "2026-11-06", "provider": "内置image_gen", "spritecook_available_credits": 0,
                "style": "原创纸感孤岛物件；深青轮廓、奶白、珊瑚橙与柔和绿块面。",
                "slice_origin": "row/column从左上角0开始；rect为Unity左下角像素坐标。原图无像素改写。",
                "card_count": len(all_ids), "atlases": []}
    for filename, columns, rows, ids in SHEETS:
        path = ART / filename
        with Image.open(path) as image:
            width, height = image.size
            if image.mode != "RGBA" or image.getchannel("A").getextrema()[0] != 0:
                raise ValueError("图集缺少真实透明背景：" + filename)
            coverage = []
            for index in range(len(ids)):
                row, column = divmod(index, columns)
                alpha = image.getchannel("A").crop((round(width * column / columns), round(height * row / rows),
                    round(width * (column + 1) / columns), round(height * (row + 1) / rows)))
                coverage.append(sum(1 for value in alpha.tobytes() if value > 128))
        guid, sprites = texture_meta(path, width, height, columns, rows, ids)
        for sprite, visible_pixels in zip(sprites, coverage):
            if visible_pixels <= 0:
                raise ValueError("切片没有可见物件：" + sprite["id"])
            card_id = sprite["id"]
            before = card_texts[card_id]
            after, count = re.subn(r"^  Artwork: .*?$", "  Artwork: {fileID: " + str(sprite["file_id"]) + ", guid: " + guid + ", type: 3}", before, flags=re.MULTILINE)
            if count != 1:
                raise ValueError("卡牌Artwork字段数量异常：" + card_id)
            if after != before:
                card_paths[card_id].write_text(after, encoding="utf-8", newline="\n")
            sprite["card_asset"] = card_paths[card_id].relative_to(ROOT).as_posix()
            sprite["opaque_pixels"] = visible_pixels
        manifest["atlases"].append({"path": path.relative_to(ROOT).as_posix(), "guid": guid,
            "sha12": hashlib.sha256(path.read_bytes()).hexdigest()[:12], "width": width, "height": height,
            "columns": columns, "rows": rows, "sprites": sprites})
    manifest["cleanup"] = {"method": "内置image_gen参考编辑，仅清透明边缘，保留格位与对象；没有用Python改图像像素。",
        "originals": [{"path": p.relative_to(ROOT).as_posix(), "sha12": hashlib.sha256(p.read_bytes()).hexdigest()[:12]}
                      for p in sorted((ART / "Source/Originals").glob("*.png"))]}
    (ART / "art-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    plain_meta(ART, True)
    plain_meta(ART / "Source", True)
    for path in ART.rglob("*"):
        if path.is_dir():
            plain_meta(path, True)
            continue
        if path.suffix == ".png" and path.parent == ART / "Source/Originals":
            atlas_name = "IslandFoodToolsAtlas.png" if "FoodToolsAtlas" in path.name else path.name
            original_meta = (ART / (atlas_name + ".meta")).read_text(encoding="utf-8")
            original_meta = re.sub(r"(?m)^guid: .*$", "guid: " + stable_guid(path.relative_to(ROOT).as_posix()), original_meta)
            original_meta = re.sub(r"(?m)^  userData: .*$", "  userData: 孤岛生存清边前原始生成源，保留供追溯", original_meta)
            Path(str(path) + ".meta").write_text(original_meta, encoding="utf-8", newline="\n")
            continue
        if path.is_file() and path.suffix != ".meta" and path.suffix != ".png":
            plain_meta(path)
    print(json.dumps({"atlases": len(SHEETS), "sprites": len(all_ids), "artwork_references_updated": len(all_ids)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
