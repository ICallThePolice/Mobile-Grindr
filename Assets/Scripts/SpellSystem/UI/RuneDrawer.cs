using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SpellSystem.Data;
using SpellSystem.Gestures;

namespace SpellSystem.UI
{
    [RequireComponent(typeof(LineRenderer))]
    public class RuneDrawer : MonoBehaviour
    {
        [Header("Drawing Area / UI Zone")]
        [SerializeField] private RectTransform drawingZone;
        [SerializeField] private Image zoneImage;
        [SerializeField] private Color zoneIdleColor = new Color(0.1f, 0.1f, 0.15f, 0.2f);
        [SerializeField] private float glowFadeSpeed = 12f;

        [Header("Settings")]
        [SerializeField] private Camera uiCamera;
        [SerializeField] private float minPointDistance = 10f;
        [SerializeField] private float trailLifetime = 1.5f;

        [Header("Slicer Width Settings")]
        [SerializeField] private float maxLineWidth = 0.03f;
        [SerializeField] private float dotScaleMultiplier = 1.2f;

        [Header("Leading Energy Dot")]
        [SerializeField] private Transform leadingDot;
        [SerializeField] private SpriteRenderer leadingDotRenderer;

        public event Action<ShapeType, float> OnShapeRecognized;
        public event Action OnDrawingStarted;
        public event Action OnDrawingEnded;

        private LineRenderer lineRenderer;
        private List<Vector2> recordedGesturePoints = new List<Vector2>();

        private struct PointData
        {
            public Vector2 screenPos;
            public Vector3 worldPos;
            public float timeCreated;
        }
        private List<PointData> visualPoints = new List<PointData>();

        private bool isDrawing = false;
        private bool isOutsideZone = false;
        private Color currentColor = Color.white;
        private Color zoneActiveColor = new Color(1f, 1f, 1f, 0.4f);
        private Canvas parentCanvas;
        private float currentLineAlpha = 1f;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (uiCamera == null) uiCamera = Camera.main;

            if (drawingZone != null)
            {
                parentCanvas = drawingZone.GetComponentInParent<Canvas>();
                if (zoneImage == null) zoneImage = drawingZone.GetComponent<Image>();
            }

            SetupLineRenderer();
            SetupLeadingDot();
        }

        private void SetupLineRenderer()
        {
            lineRenderer.positionCount = 0;
            // ВОЗВРАЩАЕМ В 3D ПРОСТРАНСТВО
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;

            AnimationCurve widthCurve = new AnimationCurve();
            widthCurve.AddKey(0.0f, 0.0f);
            widthCurve.AddKey(0.7f, 0.75f);
            widthCurve.AddKey(1.0f, 1.0f);

            lineRenderer.widthCurve = widthCurve;
            lineRenderer.widthMultiplier = maxLineWidth;
        }

        private void SetupLeadingDot()
        {
            if (leadingDot == null)
            {
                GameObject dotGo = new GameObject("LeadingDot");
                dotGo.transform.SetParent(transform);
                leadingDot = dotGo.transform;
                leadingDotRenderer = dotGo.AddComponent<SpriteRenderer>();

                Texture2D tex = CreateCircleTexture();
                leadingDotRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }

            float dotSize = maxLineWidth * dotScaleMultiplier;
            leadingDot.localScale = new Vector3(dotSize, dotSize, 1f);

            if (leadingDot != null) leadingDot.gameObject.SetActive(false);
        }

        private Texture2D CreateCircleTexture()
        {
            int res = 64;
            Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            float radius = res / 2f;
            Vector2 center = new Vector2(radius, radius);

            for (int x = 0; x < res; x++)
            {
                for (int y = 0; y < res; y++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = dist <= radius ? Mathf.SmoothStep(1f, 0f, dist / radius) : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        // ИСПРАВЛЕНИЕ: Используем LateUpdate вместо Update.
        // Теперь рисование происходит строго ПОСЛЕ того, как камера переместилась за персонажем.
        private void LateUpdate()
        {
            HandleInput();
            UpdateVisualTrail();
            UpdateZoneGlow();
        }

        private void HandleInput()
        {
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer == null) return;

            Vector2 pointerPos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                if (IsPositionInsideZone(pointerPos))
                {
                    StartDrawing(pointerPos);
                }
            }
            else if (pointer.press.isPressed && isDrawing)
            {
                bool inside = IsPositionInsideZone(pointerPos);

                if (!inside)
                {
                    isOutsideZone = true;
                    if (leadingDot != null) leadingDot.gameObject.SetActive(false);
                }
                else
                {
                    if (isOutsideZone)
                    {
                        isOutsideZone = false;
                        if (leadingDot != null) leadingDot.gameObject.SetActive(true);
                    }
                    ContinueDrawing(pointerPos);
                }
            }
            else if (pointer.press.wasReleasedThisFrame && isDrawing)
            {
                if (isOutsideZone) CancelDrawing();
                else EndDrawing();
            }
        }

        private bool IsPositionInsideZone(Vector2 screenPos)
        {
            if (drawingZone == null) return true;
            if (IsPointerOverButton(screenPos)) return false;

            if (parentCanvas == null) parentCanvas = drawingZone.GetComponentInParent<Canvas>();

            Camera eventCamera = null;
            if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                eventCamera = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : uiCamera;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(drawingZone, screenPos, eventCamera, out Vector2 localPoint))
            {
                float radius = drawingZone.rect.width / 2f;
                Vector2 centerOffset = localPoint - drawingZone.rect.center;
                return centerOffset.sqrMagnitude <= (radius * radius);
            }
            return false;
        }

        private bool IsPointerOverButton(Vector2 screenPos)
        {
            if (UnityEngine.EventSystems.EventSystem.current == null) return false;

            var eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            {
                position = screenPos
            };

            var results = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                // Игнорируем саму панель рисования (фон)
                if (result.gameObject != null && result.gameObject != drawingZone.gameObject)
                {
                    GameObject go = result.gameObject;

                    // ИСПРАВЛЕНИЕ: Теперь мы проверяем не только стандартные кнопки (Selectable),
                    // но и любые объекты, которые умеют нажиматься (IPointerDownHandler, IPointerClickHandler)
                    // или перетаскиваться (IBeginDragHandler) — то есть наши кастомные слоты и джойстик!
                    if (go.GetComponentInParent<UnityEngine.UI.Selectable>() != null ||
                        go.GetComponentInParent<UnityEngine.EventSystems.IPointerDownHandler>() != null ||
                        go.GetComponentInParent<UnityEngine.EventSystems.IPointerClickHandler>() != null ||
                        go.GetComponentInParent<UnityEngine.EventSystems.IBeginDragHandler>() != null)
                    {
                        return true; // Блокируем рисование, отдаем касание слоту
                    }
                }
            }

            return false;
        }

        private void CancelDrawing()
        {
            OnDrawingEnded?.Invoke();
            isDrawing = false;
            isOutsideZone = false;
            recordedGesturePoints.Clear();
            visualPoints.Clear();

            if (lineRenderer != null) lineRenderer.positionCount = 0;
            if (leadingDot != null) leadingDot.gameObject.SetActive(false);
        }

        private void StartDrawing(Vector2 screenPos)
        {
            OnDrawingStarted?.Invoke();
            isDrawing = true;
            isOutsideZone = false;
            recordedGesturePoints.Clear();
            visualPoints.Clear();

            currentLineAlpha = 1f;
            ApplyGradientAlpha(1f);

            lineRenderer.positionCount = 0;
            if (leadingDot != null) leadingDot.gameObject.SetActive(true);

            AddPoint(screenPos);
        }

        private void ContinueDrawing(Vector2 screenPos)
        {
            if (recordedGesturePoints.Count == 0) return;

            Vector2 lastScreenPos = recordedGesturePoints[recordedGesturePoints.Count - 1];
            float dist = Vector2.Distance(lastScreenPos, screenPos);

            if (dist >= minPointDistance)
            {
                AddPoint(screenPos);
            }
        }

        private void AddPoint(Vector2 screenPos)
        {
            recordedGesturePoints.Add(screenPos);

            float zDepth = uiCamera.nearClipPlane + 1f;
            Vector3 worldPos = uiCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, zDepth));

            visualPoints.Add(new PointData
            {
                screenPos = screenPos,
                worldPos = worldPos,
                timeCreated = Time.time
            });
        }

        private void UpdateVisualTrail()
        {
            float currentTime = Time.time;

            while (visualPoints.Count > 0 && (currentTime - visualPoints[0].timeCreated) > trailLifetime)
            {
                visualPoints.RemoveAt(0);
            }

            lineRenderer.positionCount = visualPoints.Count;
            float zDepth = uiCamera.nearClipPlane + 1f;

            for (int i = 0; i < visualPoints.Count; i++)
            {
                // Постоянно пересчитываем позиции точек в мировом пространстве
                // Так как это происходит в LateUpdate (после камеры), линия всегда будет приклеена к экрану
                Vector3 currentWorldPos = uiCamera.ScreenToWorldPoint(new Vector3(visualPoints[i].screenPos.x, visualPoints[i].screenPos.y, zDepth));
                lineRenderer.SetPosition(i, currentWorldPos);
            }

            if (isDrawing && leadingDot != null && visualPoints.Count > 0)
            {
                Vector2 lastScreenPos = visualPoints[visualPoints.Count - 1].screenPos;
                leadingDot.position = uiCamera.ScreenToWorldPoint(new Vector3(lastScreenPos.x, lastScreenPos.y, zDepth));
            }

            if (!isDrawing && visualPoints.Count > 0)
            {
                currentLineAlpha -= Time.deltaTime * 3f;

                if (currentLineAlpha <= 0f)
                {
                    currentLineAlpha = 0f;
                    visualPoints.Clear();
                    lineRenderer.positionCount = 0;
                }

                ApplyGradientAlpha(currentLineAlpha);
            }

            if (!isDrawing && visualPoints.Count == 0 && leadingDot != null)
            {
                leadingDot.gameObject.SetActive(false);
            }
        }

        private void UpdateZoneGlow()
        {
            if (zoneImage == null) return;
            Color targetColor = (isDrawing && !isOutsideZone) ? zoneActiveColor : zoneIdleColor;
            zoneImage.color = Color.Lerp(zoneImage.color, targetColor, Time.deltaTime * glowFadeSpeed);
        }

        private void EndDrawing()
        {
            OnDrawingEnded?.Invoke();
            isDrawing = false;
            isOutsideZone = false;
            if (leadingDot != null) leadingDot.gameObject.SetActive(false);

            if (recordedGesturePoints.Count >= 5)
            {
                ShapeType recognizedShape = GestureRecognizer.RecognizeShape(recordedGesturePoints, out float accuracy);
                OnShapeRecognized?.Invoke(recognizedShape, accuracy);
            }
        }

        private void ApplyGradientAlpha(float globalAlpha)
        {
            Gradient gradient = new Gradient();

            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(currentColor, 0.0f);
            colorKeys[1] = new GradientColorKey(currentColor, 1.0f);

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(0.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(globalAlpha, 1.0f);

            gradient.SetKeys(colorKeys, alphaKeys);
            if (lineRenderer != null) lineRenderer.colorGradient = gradient;
        }

        public void SetLineColor(Color color)
        {
            currentColor = color;
            zoneActiveColor = new Color(color.r, color.g, color.b, 0.35f);
            ApplyGradientAlpha(currentLineAlpha);

            if (leadingDotRenderer != null)
            {
                leadingDotRenderer.color = color;
            }
        }
    }
}