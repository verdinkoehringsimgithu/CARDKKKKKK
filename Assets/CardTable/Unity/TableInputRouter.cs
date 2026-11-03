using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardKKK.Tabletop.Unity
{
    /// <summary>只把输入映射为命令，切换设备无需修改规则或视图。</summary>
    public sealed class TableInputRouter : MonoBehaviour
    {
        private CardTableController controller;
        public void Bind(CardTableController value) => controller = value;
        private void Update()
        {
            if (controller == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { controller.CancelDrag(); return; }
            GameObject focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused != null && focused.GetComponent<InputField>() != null) return;
#if !CARDTABLE_DISABLE_FLIP
            if (Input.GetKeyDown(KeyCode.Space))
            {
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
                controller.Execute(TableAction.Flip);
            }
#endif
#if !CARDTABLE_DISABLE_SORT
            if (Input.GetKeyDown(KeyCode.S)) controller.Execute(TableAction.Sort);
#endif
#if !CARDTABLE_DISABLE_SHUFFLE
            if (Input.GetKeyDown(KeyCode.H)) controller.Execute(TableAction.Shuffle);
#endif
#if !CARDTABLE_DISABLE_POP
            if (Input.GetKeyDown(KeyCode.P)) controller.Execute(TableAction.Pop);
#endif
#if !CARDTABLE_DISABLE_REORDER
            if (Input.GetKeyDown(KeyCode.LeftArrow)) controller.Execute(TableAction.ReorderEarlier);
            if (Input.GetKeyDown(KeyCode.RightArrow)) controller.Execute(TableAction.ReorderLater);
#endif
#if !CARDTABLE_DISABLE_TIDY
            if (Input.GetKeyDown(KeyCode.R)) controller.Execute(TableAction.Tidy);
#endif
#if !CARDTABLE_DISABLE_SAVE
            if (Input.GetKeyDown(KeyCode.F5)) controller.Execute(TableAction.Save);
            if (Input.GetKeyDown(KeyCode.F9)) controller.Execute(TableAction.Load);
#endif
        }
    }
}
