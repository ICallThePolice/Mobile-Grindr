using UnityEngine;
using UnityEngine.EventSystems;

namespace SpellSystem.UI
{
    public class DraggableAttackButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform rectTransform;
        private Vector3 startPosition;
        private Canvas parentCanvas;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Запоминаем позицию, чтобы вернуться, если бросили не туда
            startPosition = rectTransform.anchoredPosition;

            // Чтобы кнопка "всплыла" над остальным UI при перетаскивании
            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Двигаем кнопку за курсором/пальцем
            rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Возвращаем кнопку на место (возврат будет "отменен" методом OnDrop в SealedSlotUI)
            rectTransform.anchoredPosition = startPosition;
        }
    }
}