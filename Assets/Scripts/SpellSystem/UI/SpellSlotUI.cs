using SpellSystem.Core;
using SpellSystem.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpellSystem.UI
{
    public class SpellSlotUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Header("References")]
        [SerializeField] private SpellCaster spellCaster;

        [Header("Visual Elements per Step (Иконки шагов)")]
        [SerializeField] private Image[] stepIcons;

        [Header("Sprites for Shapes")]
        [SerializeField] private Sprite triangleSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite squareSprite;

        [Header("Settings")]
        [SerializeField] private float holdDuration = 0.5f;

        private List<SpellCaster.ComboStep> savedCombo = new List<SpellCaster.ComboStep>();
        private bool isHolding = false;
        private float holdTimer = 0f;
        private bool hasSealedThisHold = false;

        private void Awake()
        {
            if (spellCaster == null) spellCaster = FindAnyObjectByType<SpellCaster>();
            ClearVisuals();
        }

        private void Update()
        {
            if (isHolding && !hasSealedThisHold)
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdDuration)
                {
                    TrySealActiveCombo();
                    hasSealedThisHold = true;
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isHolding = true;
            holdTimer = 0f;
            hasSealedThisHold = false;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isHolding = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!hasSealedThisHold)
            {
                if (savedCombo != null && savedCombo.Count > 0)
                {
                    CastSavedCombo();
                }
                else
                {
                    TrySealActiveCombo();
                }
            }
        }

        private void TrySealActiveCombo()
        {
            if (spellCaster != null && spellCaster.HasActiveCombo())
            {
                savedCombo = spellCaster.GetCurrentComboCopy();
                UpdateVisuals();

                spellCaster.ResetCombo();
                Debug.Log("[SpellSlotUI] Комбо успешно запечатано в слот!");
            }
            else
            {
                Debug.LogWarning("[SpellSlotUI] Нечего запечатывать — поле рисования пустое.");
            }
        }

        private void CastSavedCombo()
        {
            if (spellCaster != null && savedCombo != null && savedCombo.Count > 0)
            {
                spellCaster.CastSealedCombo(savedCombo);
                Debug.Log("[SpellSlotUI] Каст сохраненного комбо из слота!");
            }
        }

        private void UpdateVisuals()
        {
            for (int i = 0; i < stepIcons.Length; i++)
            {
                if (stepIcons[i] == null) continue;

                if (i < savedCombo.Count)
                {
                    stepIcons[i].gameObject.SetActive(true);
                    var step = savedCombo[i];

                    // Выбираем соответствующий спрайт формы
                    Sprite targetSprite = step.shape switch
                    {
                        ShapeType.Triangle => triangleSprite,
                        ShapeType.Circle => circleSprite,
                        ShapeType.Square => squareSprite,
                        _ => null
                    };

                    if (targetSprite != null)
                    {
                        stepIcons[i].sprite = targetSprite;
                    }
                    else
                    {
                        Debug.LogWarning($"[SpellSlotUI] Не назначен спрайт для формы {step.shape} в инспекторе слота!");
                    }

                    // Устанавливаем цвет стихии, принудительно делая его видимым (alpha = 1)
                    if (step.energy != null)
                    {
                        Color col = step.energy.primaryColor;
                        col.a = 1f; // Защита от нулевой прозрачности
                        stepIcons[i].color = col;
                    }
                    else
                    {
                        stepIcons[i].color = Color.white;
                    }
                }
                else
                {
                    stepIcons[i].gameObject.SetActive(false);
                }
            }
        }

        private void ClearVisuals()
        {
            foreach (var icon in stepIcons)
            {
                if (icon != null)
                {
                    icon.gameObject.SetActive(false);
                    icon.sprite = null;
                }
            }
        }
    }
}