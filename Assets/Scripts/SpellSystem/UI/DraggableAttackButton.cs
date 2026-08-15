using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SpellSystem.Core;

namespace SpellSystem.UI
{
    [RequireComponent(typeof(Button))]
    public class DraggableAttackButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Settings")]
        [SerializeField] private Canvas canvas;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector3 originalPosition;
        private Transform originalParent;
        private SpellCaster cachedCaster; // Кэш

        [HideInInspector] public bool isDragging = false;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (canvas == null)
            {
                canvas = GetComponentInParent<Canvas>();
            }

            cachedCaster = FindAnyObjectByType<SpellCaster>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (cachedCaster == null) cachedCaster = FindAnyObjectByType<SpellCaster>();

            if (cachedCaster == null || !cachedCaster.HasActiveCombo())
            {
                eventData.pointerDrag = null;
                return;
            }

            isDragging = true;
            originalPosition = rectTransform.anchoredPosition;
            originalParent = transform.parent;

            transform.SetParent(canvas.transform, true);
            transform.SetAsLastSibling();

            canvasGroup.alpha = 0.6f;
            canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            if (canvas != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                canvas.worldCamera,
                out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!isDragging) return;

            isDragging = false;
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;

            transform.SetParent(originalParent, true);
            rectTransform.anchoredPosition = originalPosition;
        }
    }
}