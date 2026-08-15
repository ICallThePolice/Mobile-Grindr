using SpellSystem.Core;
using SpellSystem.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpellSystem.UI
{
    public class SpellSlotUI : MonoBehaviour, IPointerClickHandler, IDropHandler
    {
        [Header("References")]
        [SerializeField] private SpellCaster spellCaster;

        [Header("Visual Elements per Step")]
        [SerializeField] private Image[] stepIcons;

        [Header("Sprites for Shapes")]
        [SerializeField] private Sprite triangleSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite squareSprite;

        private List<SpellCaster.ComboStep> savedCombo = new List<SpellCaster.ComboStep>();

        private void Awake()
        {
            if (spellCaster == null) spellCaster = FindAnyObjectByType<SpellCaster>();
            ClearVisuals();
        }

        // --- Перетаскивание (Drop): Запечатывание навыка в слот ---
        public void OnDrop(PointerEventData eventData)
        {
            // Проверяем, что перетаскивают именно элемент с компонентом DraggableAttackButton
            var draggable = eventData.pointerDrag?.GetComponent<DraggableAttackButton>();
            if (draggable == null) return;

            if (spellCaster == null) spellCaster = FindAnyObjectByType<SpellCaster>();

            if (spellCaster != null && spellCaster.HasActiveCombo())
            {
                savedCombo = spellCaster.GetCurrentComboCopy();
                UpdateVisuals();

                spellCaster.ResetCombo();
                Debug.Log("[SpellSlotUI] Заклинание успешно запечатано в слот перетаскиванием!");
            }
            else
            {
                Debug.LogWarning("[SpellSlotUI] Нечего запечатывать — поле рисования пустое.");
            }
        }

        // --- Клик: Быстрый каст сохраненного навыка ---
        public void OnPointerClick(PointerEventData eventData)
        {
            if (savedCombo != null && savedCombo.Count > 0)
            {
                CastSavedCombo();
            }
            else
            {
                Debug.Log("[SpellSlotUI] Слот пуст. Перетащите сюда заклинание с кнопки атаки.");
            }
        }

        // --- Функционал ---

        private void CastSavedCombo()
        {
            if (spellCaster != null && savedCombo != null && savedCombo.Count > 0)
            {
                spellCaster.CastSealedCombo(savedCombo);
                Debug.Log("[SpellSlotUI] Каст из слота!");
            }
        }

        // --- Визуализация ---

        private void UpdateVisuals()
        {
            // Сначала очищаем старые иконки
            ClearVisuals();

            for (int i = 0; i < stepIcons.Length; i++)
            {
                if (i < savedCombo.Count)
                {
                    stepIcons[i].gameObject.SetActive(true);
                    var step = savedCombo[i];

                    stepIcons[i].sprite = step.shape switch
                    {
                        ShapeType.Triangle => triangleSprite,
                        ShapeType.Circle => circleSprite,
                        ShapeType.Square => squareSprite,
                        _ => null
                    };

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