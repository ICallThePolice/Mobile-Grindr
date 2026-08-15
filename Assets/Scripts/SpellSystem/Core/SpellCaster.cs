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
        [SerializeField] private SpellTotem totemPrefab;

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

        // Устанавливает цель из системы прицеливания
        private bool isHardLocked = false;

        // Обновленный метод установки цели с флагом жесткого лока
        public void SetTarget(Transform target, bool hardLocked)
        {
            lockOnTarget = target;
            isHardLocked = hardLocked;
        }

        // Перегрузка для совместимости
        public void SetTarget(Transform target)
        {
            lockOnTarget = target;
            isHardLocked = false;
        }

        // Вызывается по кнопке "Атака"
        public void CastCurrentCombo()
        {
            if (currentCombo.Count == 0)
            {
                Debug.Log("[SpellCaster] Печать пуста! Нарисуйте хотя бы одну руну.");
                return;
            }

            ExecuteComboChain(currentCombo);
        }

        // Запускает каст конкретного сохраненного в слоте комбо
        public void CastSealedCombo(List<SpellCaster.ComboStep> comboToCast)
        {
            if (comboToCast == null || comboToCast.Count == 0) return;

            ExecuteComboChain(comboToCast);
        }

        // ГЛАВНЫЙ МЕТОД: ЗАПУСК ЦЕПОЧКИ
        private void ExecuteComboChain(List<ComboStep> combo)
        {
            // 1. Собираем цепочку и получаем первый (корневой) узел
            SpellNode rootNode = BuildSpellChain(combo);
            if (rootNode == null) return;

            // 2. Создаем стартовый контекст
            Vector3 startPos = castPoint != null ? castPoint.position : (transform.position + transform.forward);
            SpellContext initialContext = new SpellContext
            {
                Caster = this.transform,
                HitPosition = startPos,
                Direction = transform.forward,
                Target = lockOnTarget,
                IsHardLocked = isHardLocked
            };

            // 3. Запускаем магию!
            rootNode.Execute(initialContext);
        }

        // ФАБРИКА УЗЛОВ (Собирает цепочку с конца в начало)
        private SpellNode BuildSpellChain(List<ComboStep> combo)
        {
            SpellNode nextNode = null;

            // Идем с конца списка к началу
            for (int i = combo.Count - 1; i >= 0; i--)
            {
                ComboStep step = combo[i];
                SpellNode currentNode = CreateNodeForShape(step.shape, step.energy);

                if (currentNode != null)
                {
                    // Указываем текущему узлу, кто идет после него
                    currentNode.NextNode = nextNode;

                    // Теперь текущий узел становится "следующим" для предыдущего шага цикла
                    nextNode = currentNode;
                }
            }

            // Возвращаем самый первый узел (он стал nextNode на последней итерации цикла)
            return nextNode;
        }

        // Метод, который решает, какой именно класс узла создать
        private SpellNode CreateNodeForShape(ShapeType shape, EnergyDataSO energy)
        {
            return shape switch
            {
                ShapeType.Triangle => new TriangleNode(projectilePrefab, energy),
                ShapeType.Circle => new CircleNode(aoePrefab, energy),
                ShapeType.Square => new SquareNode(shieldPrefab, totemPrefab, energy), // <--- Передаем totemPrefab
                _ => null
            };
        }

        // Вызывается по кнопке "Сброс" (Reset)
        public void ResetCombo()
        {
            currentCombo.Clear();
            if (sealUI != null) sealUI.ClearAllSlots();
            Debug.Log("[SpellCaster] Печать и комбинация сброшены!");
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
    }
}