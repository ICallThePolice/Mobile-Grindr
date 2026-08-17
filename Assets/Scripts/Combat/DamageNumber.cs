using UnityEngine;
using TMPro;
using System.Collections;

namespace SpellSystem.UI
{
    public class DamageNumber : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private TMP_Text textMesh; // ИСПРАВЛЕНИЕ: Универсальный класс текста
        [SerializeField] private float lifetime = 1.2f;
        [SerializeField] private float moveSpeedX = 2f;
        [SerializeField] private float arcHeight = 1.5f;

        private Vector3 startPos;
        private Camera cam;

        private void Awake()
        {
            // Ищем любой компонент текста (и 3D, и UI) на самом объекте и его детях
            if (textMesh == null) textMesh = GetComponent<TMP_Text>();
            if (textMesh == null) textMesh = GetComponentInChildren<TMP_Text>();
        }

        public void Initialize(float damage, Color color, Vector3 targetPosition, Camera mainCamera)
        {
            this.cam = mainCamera != null ? mainCamera : Camera.main;

            if (textMesh != null)
            {
                textMesh.text = Mathf.RoundToInt(damage).ToString();
                color.a = 1f;
                textMesh.color = color;
            }

            if (cam != null)
            {
                Vector3 baseOffset = Vector3.up * 1.5f + cam.transform.right * 0.5f;
                Vector3 randomScatter = cam.transform.right * Random.Range(-0.4f, 0.4f) + cam.transform.up * Random.Range(-0.2f, 0.5f);
                transform.position = targetPosition + baseOffset + randomScatter;
            }
            else
            {
                transform.position = targetPosition + Vector3.up * 1.5f;
            }

            startPos = transform.position;
            StartCoroutine(AnimateNumber());
        }

        private IEnumerator AnimateNumber()
        {
            float timer = 0f;

            while (timer < lifetime)
            {
                timer += Time.deltaTime;
                float progress = timer / lifetime;

                if (cam != null)
                {
                    transform.rotation = cam.transform.rotation;
                    float xMove = progress * moveSpeedX;
                    float yMove = Mathf.Sin(progress * Mathf.PI) * arcHeight;
                    transform.position = startPos + (cam.transform.right * xMove) + (cam.transform.up * yMove);
                }

                if (progress > 0.5f && textMesh != null)
                {
                    float alpha = 1f - ((progress - 0.5f) * 2f);
                    Color c = textMesh.color;
                    c.a = alpha;
                    textMesh.color = c;
                }

                yield return null;
            }

            gameObject.SetActive(false);
        }
    }
}