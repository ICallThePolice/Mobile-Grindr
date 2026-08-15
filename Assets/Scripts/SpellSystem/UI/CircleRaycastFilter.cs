using UnityEngine;
using UnityEngine.UI;

namespace SpellSystem.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class CircleRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        private RectTransform rectTransform;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (rectTransform == null) return false;

            // Переводим точку клика в локальные координаты кнопки
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
            {
                float radius = rectTransform.rect.width / 2f;
                Vector2 centerOffset = localPoint - rectTransform.rect.center;

                // Клик сработает ТОЛЬКО если палец попал строго в радиус круга
                return centerOffset.sqrMagnitude <= (radius * radius);
            }

            return false;
        }
    }
}