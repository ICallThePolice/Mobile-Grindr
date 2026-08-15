using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SpellSystem.Core;
using SpellSystem.Data; // Добавлено для доступа к ShapeType/EnergyDataSO

namespace SpellSystem.UI
{
    public class SealedSlotUI : MonoBehaviour, IDropHandler
    {
        [Header("Slot Settings")]
        [SerializeField] private int slotIndex = 0;

        [Header("Concentric Visuals")]
        [SerializeField] private Image slot1_Outer;
        [SerializeField] private Image slot2_Middle;
        [SerializeField] private Image slot3_Inner;

        [Header("Shape Icons Sprites")]
        [SerializeField] private Sprite triangleSprite;
        [SerializeField] private Sprite circleSprite;
        [SerializeField] private Sprite squareSprite;

        private List<SpellCaster.ComboStep> savedCombo = new List<SpellCaster.ComboStep>();
        private bool isUnlocked = true;
        private SpellCaster cachedCaster;

        private void Awake()
        {
            ClearVisuals();
            cachedCaster = FindAnyObjectByType<SpellCaster>();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (!isUnlocked) return;

            if (cachedCaster == null) cachedCaster = FindAnyObjectByType<SpellCaster>();
            if (cachedCaster == null) return;

            savedCombo = cachedCaster.GetCurrentComboCopy();

            if (savedCombo.Count > 0)
            {
                UpdateSlotVisuals(savedCombo);
                Debug.Log($"[SealedSlot] Заклинание из {savedCombo.Count} слоев запечатано в слот №{slotIndex}");
            }
        }

        private void UpdateSlotVisuals(List<SpellCaster.ComboStep> combo)
        {
            ClearVisuals();

            // Проходимся по всем шагам комбо и активируем нужные слои
            for (int i = 0; i < combo.Count; i++)
            {
                if (i == 0 && slot1_Outer != null) SetupLayer(slot1_Outer, combo[i], i);
                else if (i == 1 && slot2_Middle != null) SetupLayer(slot2_Middle, combo[i], i);
                else if (i == 2 && slot3_Inner != null) SetupLayer(slot3_Inner, combo[i], i);
            }
        }

        private void SetupLayer(Image layerImage, SpellCaster.ComboStep step, int stepIndex)
        {
            layerImage.gameObject.SetActive(true);

            // 1. Устанавливаем цвет энергии (тут всё верно, energy — это класс EnergyDataSO, его можно проверить на null)
            if (step.energy != null)
            {
                layerImage.color = step.energy.primaryColor;
            }

            // 2. Устанавливаем спрайт формы
            // step.shape уже является ShapeType, поэтому передаем его напрямую!
            layerImage.sprite = GetSpriteForShape(step.shape);

            // 3. Вращение слоев
            float zRotation = 0f;
            if (stepIndex == 1)
            {
                if (step.shape == ShapeType.Triangle) zRotation = 180f;
                else if (step.shape == ShapeType.Square) zRotation = 45f;
            }

            layerImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        }

        private void ClearVisuals()
        {
            if (slot1_Outer != null) slot1_Outer.gameObject.SetActive(false);
            if (slot2_Middle != null) slot2_Middle.gameObject.SetActive(false);
            if (slot3_Inner != null) slot3_Inner.gameObject.SetActive(false);
        }

        public void OnSlotClicked()
        {
            if (savedCombo == null || savedCombo.Count == 0) return;

            if (cachedCaster == null) cachedCaster = FindAnyObjectByType<SpellCaster>();
            if (cachedCaster != null)
            {
                cachedCaster.CastSealedCombo(savedCombo);
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