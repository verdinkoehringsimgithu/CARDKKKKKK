"""旧全局索引映射已撤回，统一调用独立模块/RID复核工具。"""
from pathlib import Path
import runpy
if __name__ == '__main__':
    runpy.run_path(str(Path(__file__).with_name('audit_native.py')), run_name='__main__')
