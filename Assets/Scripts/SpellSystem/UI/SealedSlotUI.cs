using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SpellSystem.Core;

namespace SpellSystem.UI
{
    public class SealedSlotUI : MonoBehaviour, IDropHandler
    {
        [Header("Slot Settings")]
        [SerializeField] private int slotIndex = 0;
        [SerializeField] private Image iconImage;

        private List<SpellCaster.ComboStep> savedCombo = new List<SpellCaster.ComboStep>();
        private bool isUnlocked = true;

        // Оптимизация: кэшируем ссылку на кастер, чтобы не искать его каждый раз на сцене
        private SpellCaster cachedCaster;

        private void Awake()
        {
            if (iconImage != null)
                iconImage.gameObject.SetActive(false);

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
                UpdateSlotVisual(savedCombo[savedCombo.Count - 1]);
                // Используем slotIndex, чтобы предупреждение исчезло
                Debug.Log($"[SealedSlot] Заклинание запечатано в слот №{slotIndex}");
            }
        }

        private void UpdateSlotVisual(SpellCaster.ComboStep lastStep)
        {
            if (iconImage == null) return;

            iconImage.gameObject.SetActive(true);
            // Исправлено: обращаемся к primaryColor, как принято в EnergyDataSO[cite: 6]
            iconImage.color = lastStep.energy != null ? lastStep.energy.primaryColor : Color.white;
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
    }
}