using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SpellSystem.Data;
using SpellSystem.UI;

namespace SpellSystem.Core
{
    public class SpellCaster : MonoBehaviour
    {
        [System.Serializable]
        public struct ComboStep
        {
            public ShapeType shape;
            public EnergyDataSO energy;
        }

        [Header("Rank Settings")]
        [Range(1, 3)]
        [SerializeField] private int maxUnlockedRank = 1;

        [Header("Energies (Keys 1, 2, 3)")]
        [SerializeField] private EnergyDataSO energy1; // Витал (Клавиша 1)
        [SerializeField] private EnergyDataSO energy2; // Psy (Клавиша 2)
        [SerializeField] private EnergyDataSO energy3; // Эреб (Клавиша 3)

        [Header("References")]
        [SerializeField] private RuneDrawer runeDrawer;
        [SerializeField] private RuneSealUI sealUI;
        [SerializeField] private Transform castPoint;
        [SerializeField] private Transform lockOnTarget;

        [Header("Prefabs")]
        [SerializeField] private SpellProjectile projectilePrefab;
        [SerializeField] private SpellAoE aoePrefab;
        [SerializeField] private SpellShield shieldPrefab;

        private EnergyDataSO currentEnergy;
        private List<ComboStep> currentCombo = new List<ComboStep>();

        private void OnEnable()
        {
            if (runeDrawer != null) runeDrawer.OnShapeRecognized += HandleShapeRecognized;
        }

        private void OnDisable()
        {
            if (runeDrawer != null) runeDrawer.OnShapeRecognized -= HandleShapeRecognized;
        }

        private void Start()
        {
            // По умолчанию ставим энергию №1
            if (energy1 != null) SetEnergy(energy1);
        }

        private void Update()
        {
            HandleEnergyHotkeys();
        }

        // Переключение стихий на 1, 2, 3
        private void HandleEnergyHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame && energy1 != null) SetEnergy(energy1);
            if (keyboard.digit2Key.wasPressedThisFrame && energy2 != null) SetEnergy(energy2);
            if (keyboard.digit3Key.wasPressedThisFrame && energy3 != null) SetEnergy(energy3);
        }

        public void SetEnergy(EnergyDataSO newEnergy)
        {
            currentEnergy = newEnergy;
            if (runeDrawer != null && currentEnergy != null)
            {
                runeDrawer.SetLineColor(currentEnergy.primaryColor);
            }
            Debug.Log($"[SpellCaster] Выбрана энергия: <color=cyan>{currentEnergy?.energyName}</color>");
        }

        private void HandleShapeRecognized(ShapeType shapeType, float accuracy)
        {
            if (currentCombo.Count >= maxUnlockedRank)
            {
                Debug.LogWarning($"[SpellCaster] Достигнут лимит ({maxUnlockedRank})! Нажмите Атаку или Сброс.");
                return;
            }

            // Запоминаем фигуру ВМЕСТЕ с энергией, которая была выбрана в момент рисования!
            ComboStep newStep = new ComboStep { shape = shapeType, energy = currentEnergy };
            currentCombo.Add(newStep);

            // Отображаем на UI именно в цвете выбранной для этой руны стихии
            if (sealUI != null && currentEnergy != null)
            {
                sealUI.DisplayRune(currentCombo.Count - 1, shapeType, currentEnergy.primaryColor);
            }
        }

        // Вызывается по кнопке "Атака"
        // Вызывается по кнопке "Атака"
        public void CastCurrentCombo()
        {
            if (currentCombo.Count == 0)
            {
                Debug.Log("[SpellCaster] Печать пуста! Нарисуйте хотя бы одну руну.");
                return;
            }

            float rankMultiplier = 1f + (currentCombo.Count - 1) * 0.5f;

            // Выпускаем заклинания
            foreach (var step in currentCombo)
            {
                ExecuteSingleSpell(step.shape, step.energy, rankMultiplier);
            }

            // ВАЖНО: Мы НЕ вызываем здесь ResetCombo()!
            // Комбо и печать остаются активными для повторных выстрелов.
            // Сброс происходит только по кнопке "Сброс" (ResetCombo).
        }

        // Вызывается по кнопке "Сброс" (Reset)
        public void ResetCombo()
        {
            currentCombo.Clear();
            if (sealUI != null) sealUI.ClearAllSlots();
            Debug.Log("[SpellCaster] Печать и комбинация сброшены!");
        }

        private void ExecuteSingleSpell(ShapeType shape, EnergyDataSO energy, float rankMultiplier)
        {
            if (energy == null) return;

            float finalDamage = energy.baseDamage * rankMultiplier;
            Vector3 spawnPos = castPoint != null ? castPoint.position : (transform.position + transform.forward);

            switch (shape)
            {
                case ShapeType.Triangle:
                    if (projectilePrefab != null)
                    {
                        var proj = Instantiate(projectilePrefab, spawnPos, transform.rotation);
                        proj.Initialize(finalDamage, energy, lockOnTarget);
                    }
                    break;

                case ShapeType.Circle:
                    Vector3 aoePos = lockOnTarget != null ? lockOnTarget.position : (transform.position + transform.forward * 4f);
                    aoePos.y = transform.position.y + 0.1f;

                    if (aoePrefab != null)
                    {
                        var aoe = Instantiate(aoePrefab, aoePos, Quaternion.identity);
                        aoe.Initialize(finalDamage, 4f, energy);
                    }
                    break;

                case ShapeType.Square:
                    if (shieldPrefab != null)
                    {
                        var shield = Instantiate(shieldPrefab, transform.position, Quaternion.identity, transform);
                        shield.Initialize(transform, energy);
                    }
                    break;
            }
        }

        // Проверяет, есть ли активное комбо для перетаскивания
        public bool HasActiveCombo()
        {
            return currentCombo != null && currentCombo.Count > 0;
        }

        // Возвращает копию текущего комбо для сохранения в слот
        public List<SpellCaster.ComboStep> GetCurrentComboCopy()
        {
            return new List<SpellCaster.ComboStep>(currentCombo);
        }

        // Запускает каст конкретного сохраненного в слоте комбо
        public void CastSealedCombo(List<SpellCaster.ComboStep> comboToCast)
        {
            if (comboToCast == null || comboToCast.Count == 0) return;

            float rankMultiplier = 1f + (comboToCast.Count - 1) * 0.5f;

            foreach (var step in comboToCast)
            {
                ExecuteSingleSpell(step.shape, step.energy, rankMultiplier);
            }
        }

    }


}