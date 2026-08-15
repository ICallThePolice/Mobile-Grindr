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
        [Tooltip("RectTransform UI-панели, в которой разрешено рисовать")]
        [SerializeField] private RectTransform drawingZone;

        [Tooltip("Image компонента рамки/фона для подсветки при касании")]
        [SerializeField] private Image zoneImage;

        [Tooltip("Прозрачность фоновой рамки в покое")]
        [SerializeField] private Color zoneIdleColor = new Color(0.1f, 0.1f, 0.15f, 0.2f);

        [Tooltip("Скорость плавного загорания/затухания подсветки")]
        [SerializeField] private float glowFadeSpeed = 12f;

        [Header("Settings")]
        [SerializeField] private Camera uiCamera;
        [SerializeField] private float minPointDistance = 10f;
        [SerializeField] private float trailLifetime = 0.4f;

        [Header("Slicer Width Settings")]
        [SerializeField] private float maxLineWidth = 0.25f;
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
        private bool isOutsideZone = false; // Флаг: вылетел ли палец за пределы зоны во время штриха
        private Color currentColor = Color.white;
        private Color zoneActiveColor = new Color(1f, 1f, 1f, 0.4f);
        private Canvas parentCanvas;

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

        private void Update()
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
                    // Вылетели из зоны: ставим на паузу, но НЕ сбрасываем точки!
                    isOutsideZone = true;
                    if (leadingDot != null) leadingDot.gameObject.SetActive(false);
                }
                else
                {
                    // Внутри зоны: если возвращаемся с улицы — снимаем с паузы
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
                // Если отпустили за пределами зоны — сброс. Если внутри — распознаем!
                if (isOutsideZone)
                {
                    CancelDrawing();
                }
                else
                {
                    EndDrawing();
                }
            }
        }

        private bool IsPositionInsideZone(Vector2 screenPos)
        {
            if (drawingZone == null) return true;

            if (IsPointerOverButton(screenPos))
            {
                return false;
            }

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
                if (result.gameObject != null && result.gameObject != drawingZone.gameObject)
                {
                    if (result.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null)
                    {
                        return true;
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

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }

            if (leadingDot != null)
            {
                leadingDot.gameObject.SetActive(false);
            }

            Debug.Log("[RuneDrawer] Отпустили палец за пределами зоны. Рисование отменено.");
        }

        private void StartDrawing(Vector2 screenPos)
        {
            OnDrawingStarted?.Invoke();
            isDrawing = true;
            isOutsideZone = false;
            recordedGesturePoints.Clear();
            visualPoints.Clear();

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

            UpdateLeadingDotPosition(screenPos);
        }

        private void AddPoint(Vector2 screenPos)
        {
            recordedGesturePoints.Add(screenPos);

            Vector3 worldPos = uiCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
            visualPoints.Add(new PointData
            {
                screenPos = screenPos,
                worldPos = worldPos,
                timeCreated = Time.time
            });

            UpdateLeadingDotPosition(screenPos);
        }

        private void UpdateLeadingDotPosition(Vector2 screenPos)
        {
            if (leadingDot != null)
            {
                Vector3 worldPos = uiCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 10f));
                leadingDot.position = worldPos;
            }
        }

        private void UpdateVisualTrail()
        {
            // Пока мы находимся вне зоны, шлейф плавно исчезает со временем,
            // но точки записанного жестa (recordedGesturePoints) сохраняются!
            float currentTime = Time.time;

            while (visualPoints.Count > 0 && (currentTime - visualPoints[0].timeCreated) > trailLifetime)
            {
                visualPoints.RemoveAt(0);
            }

            lineRenderer.positionCount = visualPoints.Count;
            for (int i = 0; i < visualPoints.Count; i++)
            {
                lineRenderer.SetPosition(i, visualPoints[i].worldPos);
            }

            if (!isDrawing && visualPoints.Count == 0 && leadingDot != null)
            {
                leadingDot.gameObject.SetActive(false);
            }
        }

        private void UpdateZoneGlow()
        {
            if (zoneImage == null) return;

            // Если вылезли за пределы зоны — гасим свечение, пока не вернемся
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
                Debug.Log($"[RuneDrawer] Распознана форма: <color=yellow>{recognizedShape}</color> (Точность: {accuracy * 100:F1}%)");
                OnShapeRecognized?.Invoke(recognizedShape, accuracy);
            }
        }

        public void SetLineColor(Color color)
        {
            currentColor = color;

            zoneActiveColor = new Color(color.r, color.g, color.b, 0.35f);

            Gradient gradient = new Gradient();

            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(color, 0.0f);
            colorKeys[1] = new GradientColorKey(color, 1.0f);

            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
            alphaKeys[0] = new GradientAlphaKey(0.0f, 0.0f);
            alphaKeys[1] = new GradientAlphaKey(0.7f, 0.6f);
            alphaKeys[2] = new GradientAlphaKey(1.0f, 1.0f);

            gradient.SetKeys(colorKeys, alphaKeys);
            lineRenderer.colorGradient = gradient;

            if (leadingDotRenderer != null)
            {
                leadingDotRenderer.color = color;
            }
        }
    }
}