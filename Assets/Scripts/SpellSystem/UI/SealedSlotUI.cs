using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SpellSystem.Core;
using SpellSystem.Data;

namespace SpellSystem.UI
{
    public class SealedSlotUI : MonoBehaviour, IDropHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler
    {
        [Header("Slot Settings")]
        [SerializeField] private int slotIndex = 0;

        [Header("Visual Elements")]
        [SerializeField] private Image[] stepIcons;

        [Header("Shape Icons Sprites")]
        [SerializeField] private Sprite triangleSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite squareSprite;

        private List<SpellCaster.ComboStep> savedCombo = new List<SpellCaster.ComboStep>();
        private bool savedIsInnate = false;
        private SpellCaster cachedCaster;

        private void Awake()
        {
            ClearVisuals();
            cachedCaster = FindAnyObjectByType<SpellCaster>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (DraggableAttackButton.IsDraggingForSeal)
            {
                TrySealCombo();
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (DraggableAttackButton.IsDraggingForSeal)
            {
                TrySealCombo();
            }
        }

        private void TrySealCombo()
        {
            if (cachedCaster == null) cachedCaster = FindAnyObjectByType<SpellCaster>();
            if (cachedCaster == null) return;

            var combo = cachedCaster.GetCurrentComboCopy();

            if (combo.Count == 0)
            {
                Debug.Log($"[SealedSlotUI] Слот #{slotIndex}: Пустое заклинание, отмена!");
                return;
            }

            cachedCaster.CancelCharge();

            savedCombo = combo;
            savedIsInnate = false;

            UpdateSlotVisuals(savedCombo);

            // Жестко отрываем кнопку от пальца
            DraggableAttackButton.CurrentDraggedButton?.MarkAsSealedAndStop();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (savedCombo != null && savedCombo.Count > 0)
            {
                if (cachedCaster == null) cachedCaster = FindAnyObjectByType<SpellCaster>();
                if (cachedCaster != null)
                {
                    cachedCaster.BeginCharge(savedCombo, savedIsInnate);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (savedCombo != null && savedCombo.Count > 0)
            {
                if (cachedCaster != null)
                {
                    cachedCaster.ReleaseCast();
                }
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
                        col.a = 1f;
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