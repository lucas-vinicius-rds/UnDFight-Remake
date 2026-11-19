using UnityEngine;
using UnityEngine.EventSystems;

namespace UnDFight
{
    /// <summary>
    /// Pointer hover bridge for MenuNavigationGroup items.
    /// Lives in its own file matching class name so Unity serializes scene references cleanly without warnings.
    /// </summary>
    public sealed class MenuItemPointerHook : MonoBehaviour, IPointerEnterHandler
    {
        private MenuNavigationGroup _group;
        private int _index;

        public void Bind(MenuNavigationGroup group, int index)
        {
            _group = group;
            _index = index;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _group?.SetSelectedIndex(_index);
        }
    }
}
