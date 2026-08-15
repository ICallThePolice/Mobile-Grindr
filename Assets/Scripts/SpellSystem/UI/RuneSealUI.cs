using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SpellSystem.Data;

namespace SpellSystem.UI
{
    public class RuneSealUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RuneDrawer runeDrawer;

        [Header("Concentric Slots (80%, 60%, 40%)")]
        [SerializeField] private Image slot1_Outer;  // 80%
        [SerializeField] private Image slot2_Middle; // 60%
        [SerializeField] private Image slot3_Inner;  // 40%

        [Header("Shape Icons Sprites")]
        [SerializeField] private Sprite triangleSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite squareSprite;

        [Header("Action Buttons")]
        [SerializeField] private Button attackButton;
        [SerializeField] private Button resetButton;

        private List<Image> slots = new List<Image>();

        private void Awake()
        {
            if (slot1_Outer != null) slots.Add(slot1_Outer);
            if (slot2_Middle != null) slots.Add(slot2_Middle);
            if (slot3_Inner != null) slots.Add(slot3_Inner);

            ClearAllSlots();
        }

        private void OnEnable()
        {
            if (runeDrawer != null)
            {
                runeDrawer.OnDrawingStarted += HandleDrawingStarted;
                runeDrawer.OnDrawingEnded += HandleDrawingEnded;
            }
        }

        private void OnDisable()
        {
            if (runeDrawer != null)
            {
                runeDrawer.OnDrawingStarted -= HandleDrawingStarted;
                runeDrawer.OnDrawingEnded -= HandleDrawingEnded;
            }
        }

        // При начале рисования — ВРЕМЕННО "УДАЛЯЕМ" КНОПКИ
        private void HandleDrawingStarted()
        {
            SetButtonsVisibility(false);
        }

        // При завершении или отмене — ВОЗВРАЩАЕМ КНОПКИ
        private void HandleDrawingEnded()
        {
            SetButtonsVisibility(true);
        }

        public void SetButtonsVisibility(bool visible)
        {
            if (attackButton != null) attackButton.gameObject.SetActive(visible);
            if (resetButton != null) resetButton.gameObject.SetActive(visible);
        }

        public void DisplayRune(int stepIndex, ShapeType shape, Color energyColor)
        {
            if (stepIndex < 0 || stepIndex >= slots.Count) return;

            Image targetSlot = slots[stepIndex];
            if (targetSlot == null) return;

            Sprite icon = GetSpriteForShape(shape);
            if (icon == null) return;

            targetSlot.sprite = icon;
            targetSlot.color = energyColor;

            float zRotation = 0f;
            if (stepIndex == 1)
            {
                if (shape == ShapeType.Triangle) zRotation = 180f;
                else if (shape == ShapeType.Square) zRotation = 45f;
            }

            targetSlot.rectTransform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
            targetSlot.gameObject.SetActive(true);
        }

        public void ClearAllSlots()
        {
            foreach (var slot in slots)
            {
                if (slot != null)
                {
                    slot.rectTransform.localRotation = Quaternion.identity;
                    slot.gameObject.SetActive(false);
                }
            }
        }

        private Sprite GetSpriteForShape(ShapeType shape)
        {
            return shape switch
            {
                ShapeType.Triangle => triangleSprite,
                ShapeType.Circle => circleSprite,
                ShapeType.Square => squareSprite,
                _ => null
            };
        }
    }
}