using UnityEngine;
using UnityEngine.UI;

namespace SpellSystem.UI
{
    [RequireComponent(typeof(Image))]
    [ExecuteAlways]
    public class UIRoundedCorners : MonoBehaviour, ICanvasRaycastFilter
    {
        [Header("Individual Corner Rounding (%)")]
        [Range(0f, 100f)][SerializeField] private float topLeft = 50f;
        [Range(0f, 100f)][SerializeField] private float topRight = 50f;
        [Range(0f, 100f)][SerializeField] private float bottomLeft = 50f;
        [Range(0f, 100f)][SerializeField] private float bottomRight = 50f;

        private Image targetImage;
        private RectTransform rectTransform;
        private Texture2D generatedTexture;
        private Sprite generatedSprite;

        private Vector4 lastAngles = new Vector4(-1, -1, -1, -1);

        private void Awake()
        {
            targetImage = GetComponent<Image>();
            rectTransform = GetComponent<RectTransform>();
            UpdateSprite();
        }

        private void OnValidate()
        {
            targetImage = GetComponent<Image>();
            rectTransform = GetComponent<RectTransform>();
            UpdateSprite();
        }

        public void UpdateSprite()
        {
            if (targetImage == null) return;

            Vector4 currentAngles = new Vector4(topLeft, topRight, bottomLeft, bottomRight);
            if (currentAngles == lastAngles && generatedSprite != null) return;
            lastAngles = currentAngles;

            int texSize = 128;
            float maxR = texSize / 2f; // 64px

            float rTL = maxR * (topLeft / 100f);
            float rTR = maxR * (topRight / 100f);
            float rBL = maxR * (bottomLeft / 100f);
            float rBR = maxR * (bottomRight / 100f);

            if (generatedTexture != null) DestroyImmediate(generatedTexture);
            generatedTexture = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);

            for (int x = 0; x < texSize; x++)
            {
                for (int y = 0; y < texSize; y++)
                {
                    bool isRight = x >= maxR;
                    bool isTop = y >= maxR;

                    float r = 0f;
                    float cx = 0f, cy = 0f;
                    bool inCornerRegion = false;

                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    if (!isRight && isTop) // Top-Left
                    {
                        r = rTL;
                        cx = rTL;
                        cy = texSize - rTL;
                        if (px < rTL && py > (texSize - rTL)) inCornerRegion = true;
                    }
                    else if (isRight && isTop) // Top-Right
                    {
                        r = rTR;
                        cx = texSize - rTR;
                        cy = texSize - rTR;
                        if (px > (texSize - rTR) && py > (texSize - rTR)) inCornerRegion = true;
                    }
                    else if (!isRight && !isTop) // Bottom-Left
                    {
                        r = rBL;
                        cx = rBL;
                        cy = rBL;
                        if (px < rBL && py < rBL) inCornerRegion = true;
                    }
                    else // Bottom-Right
                    {
                        r = rBR;
                        cx = texSize - rBR;
                        cy = rBR; // ИСПРАВЛЕНО: cy указывает на нижнюю часть!
                        if (px > (texSize - rBR) && py < rBR) inCornerRegion = true;
                    }

                    float alpha = 1f;
                    if (inCornerRegion && r > 0f)
                    {
                        float dist = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                        alpha = Mathf.Clamp01(r - dist + 0.5f);
                    }

                    generatedTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            generatedTexture.Apply();

            Vector4 border = new Vector4(maxR, maxR, maxR, maxR);
            generatedSprite = Sprite.Create(generatedTexture, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);

            targetImage.sprite = generatedSprite;
            targetImage.type = Image.Type.Sliced;
        }

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (rectTransform == null) return false;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 localPoint))
            {
                float w = rectTransform.rect.width;
                float h = rectTransform.rect.height;

                float maxR = Mathf.Min(w, h) / 2f;

                bool isRight = localPoint.x >= 0;
                bool isTop = localPoint.y >= 0;

                float cornerPercent = 0f;
                Vector2 center = Vector2.zero;
                bool inCornerArea = false;

                if (!isRight && isTop) // TL
                {
                    cornerPercent = topLeft;
                    float r = maxR * (cornerPercent / 100f);
                    center = new Vector2(-w / 2f + r, h / 2f - r);
                    if (localPoint.x < (-w / 2f + r) && localPoint.y > (h / 2f - r)) inCornerArea = true;
                }
                else if (isRight && isTop) // TR
                {
                    cornerPercent = topRight;
                    float r = maxR * (cornerPercent / 100f);
                    center = new Vector2(w / 2f - r, h / 2f - r);
                    if (localPoint.x > (w / 2f - r) && localPoint.y > (h / 2f - r)) inCornerArea = true;
                }
                else if (!isRight && !isTop) // BL
                {
                    cornerPercent = bottomLeft;
                    float r = maxR * (cornerPercent / 100f);
                    center = new Vector2(-w / 2f + r, -h / 2f + r);
                    if (localPoint.x < (-w / 2f + r) && localPoint.y < (-h / 2f + r)) inCornerArea = true;
                }
                else // BR
                {
                    cornerPercent = bottomRight;
                    float r = maxR * (cornerPercent / 100f);
                    center = new Vector2(w / 2f - r, -h / 2f + r);
                    if (localPoint.x > (w / 2f - r) && localPoint.y < (-h / 2f + r)) inCornerArea = true;
                }

                if (inCornerArea)
                {
                    float r = maxR * (cornerPercent / 100f);
                    return Vector2.Distance(localPoint, center) <= r;
                }

                return true;
            }

            return false;
        }
    }
}