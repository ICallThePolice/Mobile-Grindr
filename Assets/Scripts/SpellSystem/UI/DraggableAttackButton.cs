using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using SpellSystem.Core; // Подключаем доступ к SpellCaster

namespace SpellSystem.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    [NoAutoStaticsCleanup]
    public class DraggableAttackButton : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, IPointerDownHandler
    {
        public static bool IsDraggingForSeal { get; private set; }
        public static DraggableAttackButton CurrentDraggedButton { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            IsDraggingForSeal = false;
            CurrentDraggedButton = null;
        }

        [Header("Settings")]
        [Tooltip("Мертвая зона. Внутри нее кнопка - джойстик, навык не запечатывается.")]
        [SerializeField] private float dragDeadzone = 60f;

        [Header("Limits")]
        [Tooltip("Зона, за пределы которой кнопка не сможет вылететь.")]
        [SerializeField] private RectTransform boundsZone;

        private RectTransform rectTransform;
        private Vector3 basePosition; // ИСПРАВЛЕНИЕ: Переименовали для ясности
        private Canvas parentCanvas;

        private bool hasExceededDeadzone;
        public bool HasBeenSealedThisDrag { get; private set; }

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
            IsDraggingForSeal = false;
        }

        private void Start()
        {
            if (rectTransform != null)
            {
                basePosition = rectTransform.anchoredPosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Начинаем новое касание - сбрасываем блокировку
            HasBeenSealedThisDrag = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            // ГАРАНТИРОВАННЫЙ ВЫСТРЕЛ: Если навык НЕ запечатан - стреляем!
            // Это решает проблему залипания на краях экрана.
            if (!HasBeenSealedThisDrag)
            {
                SpellCaster caster = FindAnyObjectByType<SpellCaster>();
                if (caster != null) caster.ReleaseCast();
            }

            ForceResetPosition();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (HasBeenSealedThisDrag) return;

            CurrentDraggedButton = this;
            hasExceededDeadzone = false;
            IsDraggingForSeal = false;

            transform.SetAsLastSibling();
        }

        public void OnDrag(PointerEventData eventData)
        {
            // ЖЕСТКАЯ ОСТАНОВКА: Если запечатали, игнорируем палец до следующего клика
            if (HasBeenSealedThisDrag) return;

            rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;

            if (boundsZone != null)
            {
                Vector3[] zoneCorners = new Vector3[4];
                boundsZone.GetWorldCorners(zoneCorners);

                Vector3[] btnCorners = new Vector3[4];
                rectTransform.GetWorldCorners(btnCorners);

                float widthExtents = (btnCorners[2].x - btnCorners[0].x) / 2f;
                float heightExtents = (btnCorners[2].y - btnCorners[0].y) / 2f;

                Vector3 pos = rectTransform.position;
                pos.x = Mathf.Clamp(pos.x, zoneCorners[0].x + widthExtents, zoneCorners[2].x - widthExtents);
                pos.y = Mathf.Clamp(pos.y, zoneCorners[0].y + heightExtents, zoneCorners[2].y - heightExtents);
                rectTransform.position = pos;
            }

            float currentDistance = Vector2.Distance(eventData.pressPosition, eventData.position);

            if (!hasExceededDeadzone && currentDistance > dragDeadzone)
            {
                hasExceededDeadzone = true;
                IsDraggingForSeal = true;
            }

            if (IsDraggingForSeal)
            {
                // Поиск слотов под кнопкой (с включенной физикой)
                List<RaycastResult> results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);

                foreach (RaycastResult result in results)
                {
                    if (result.gameObject == gameObject) continue;
                    if (result.gameObject.GetComponentInParent<IPointerEnterHandler>() != null)
                    {
                        ExecuteEvents.Execute(result.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
                    }
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ForceResetPosition();
        }

        // КОМАНДА ОТ СЛОТОВ: Запечатать и остановить
        public void MarkAsSealedAndStop()
        {
            HasBeenSealedThisDrag = true;
            ForceResetPosition();
        }

        private void ForceResetPosition()
        {
            rectTransform.anchoredPosition = basePosition;
            IsDraggingForSeal = false;
            hasExceededDeadzone = false;
            CurrentDraggedButton = null;
        }
    }
}