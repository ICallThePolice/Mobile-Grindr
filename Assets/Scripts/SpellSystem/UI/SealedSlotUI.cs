using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SpellSystem.Core;
using SpellSystem.Data;

namespace SpellSystem.UI
{
    public class SealedSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [Header("Slot Settings")]
        [SerializeField] private int slotIndex = 0;

        [Header("Visual Elements (Перетащите сюда 3 шага этого слота)")]
        [SerializeField] private Image[] stepIcons; // Сюда массив из 3 картинок (например, S1_Step_1, S1_Step_2, S1_Step_3)

        [Header("Shape Icons Sprites")]
        [SerializeField] private Sprite triangleSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite squareSprite;

        private List<SpellCaster.ComboStep> savedCombo = new List<SpellCaster.ComboStep>();
        private SpellCaster cachedCaster;

        private void Awake()
        {
            ClearVisuals();
            cachedCaster = FindAnyObjectByType<SpellCaster>();
        }

        // Срабатывает, когда на этот слот перетаскивают кнопку атаки
        public void OnDrop(PointerEventData eventData)
        {
            var draggable = eventData.pointerDrag?.GetComponent<DraggableAttackButton>();
            if (draggable == null) return;

            if (cachedCaster == null) cachedCaster = FindAnyObjectByType<SpellCaster>();
            if (cachedCaster == null) return;

            var combo = cachedCaster.GetCurrentComboCopy();
            if (combo != null && combo.Count > 0)
            {
                savedCombo = combo;
                UpdateSlotVisuals(savedCombo);
                cachedCaster.ResetCombo(); // Очищаем поле рисования после успешного запечатывания
                Debug.Log($"[SealedSlotUI] Навык успешно запечатан в слот #{slotIndex}!");
            }
            else
            {
                Debug.LogWarning("[SealedSlotUI] Нечего запечатывать — текущее комбо пустое.");
            }
        }

        // Срабатывает при клике по запечатанному слоту — для быстрого каста
        public void OnPointerClick(PointerEventData eventData)
        {
            if (savedCombo != null && savedCombo.Count > 0)
            {
                if (cachedCaster == null) cachedCaster = FindAnyObjectByType<SpellCaster>();
                if (cachedCaster != null)
                {
                    cachedCaster.CastSealedCombo(savedCombo);
                    Debug.Log($"[SealedSlotUI] Каст сохраненного навыка из слота #{slotIndex}!");
                }
            }
            else
            {
                Debug.Log("[SealedSlotUI] Слот пуст. Перетащите сюда заклинание с кнопки атаки.");
            }
        }

        private void UpdateSlotVisuals(List<SpellCaster.ComboStep> combo)
        {
            ClearVisuals();

            for (int i = 0; i < stepIcons.Length; i++)
            {
                if (stepIcons[i] == null) continue;

                if (i < combo.Count)
                {
                    stepIcons[i].gameObject.SetActive(true);
                    var step = combo[i];

                    // Устанавливаем спрайт формы
                    stepIcons[i].sprite = step.shape switch
                    {
                        ShapeType.Triangle => triangleSprite,
                        ShapeType.Circle => circleSprite,
                        ShapeType.Square => squareSprite,
                        _ => null
                    };

                    // Устанавливаем цвет энергии
                    if (step.energy != null)
                    {
                        Color col = step.energy.primaryColor;
                        col.a = 1f; // Гарантируем полную видимость
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
            if (stepIcons == null) return;
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