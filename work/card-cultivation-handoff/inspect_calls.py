"""只查看已核对地址产生的直接调用索引，不使用旧全局指针表。"""
from pathlib import Path
import json
p = Path(__file__).resolve().parents[2] / 'docs/handoffs/2026-11-09-卡牌桌面底层/复核/已解析直接调用边.json'
if __name__ == '__main__':
    if not p.exists():
        raise SystemExit('先运行 audit_native.py。')
    data = json.loads(p.read_text(encoding='utf8'))
    print('直接调用边记录：', len(data), '；共享地址保留多别名，不按任意单名归因。')
