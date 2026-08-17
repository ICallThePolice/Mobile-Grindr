using SpellSystem.Core;
using SpellSystem.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpellSystem.UI
{
    public class SpellSlotUI : MonoBehaviour, IDropHandler, IPointerClickHandler, IPointerEnterHandler
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

        private bool savedIsInnate = false;
        private int savedChargeLevel = 0;
        private float savedMultiplier = 1f;

        private void Awake()
        {
            if (spellCaster == null) spellCaster = FindAnyObjectByType<SpellCaster>();
            ClearVisuals();
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
            if (spellCaster == null) spellCaster = FindAnyObjectByType<SpellCaster>();
            if (spellCaster == null) return;

            var combo = spellCaster.GetCurrentComboCopy();

            if (combo.Count == 0)
            {
                Debug.Log("[SpellSlotUI] Пустое заклинание! Отмена запечатывания.");
                return;
            }

            savedChargeLevel = spellCaster.GetCurrentChargeLevel();
            savedCombo = combo;
            savedIsInnate = false;

            savedChargeLevel = Mathf.Min(savedChargeLevel, savedCombo.Count);
            savedMultiplier = spellCaster.GetChargeMultiplier(savedChargeLevel);

            UpdateVisuals();

            spellCaster.CancelCharge();
            spellCaster.ResetCombo();

            // Жестко отрываем кнопку от пальца
            DraggableAttackButton.CurrentDraggedButton?.MarkAsSealedAndStop();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (savedCombo != null && savedCombo.Count > 0 && spellCaster != null)
            {
                spellCaster.CastSealedCombo(savedCombo, savedChargeLevel, savedMultiplier, savedIsInnate);
            }
        }

        private void UpdateVisuals()
        {
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
                        col.a = 1f;
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