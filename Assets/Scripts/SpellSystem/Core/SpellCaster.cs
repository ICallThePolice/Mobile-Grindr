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
        [SerializeField] private EnergyDataSO energy1;
        [SerializeField] private EnergyDataSO energy2;
        [SerializeField] private EnergyDataSO energy3;

        [Header("References")]
        [SerializeField] private RuneDrawer runeDrawer;
        [SerializeField] private RuneSealUI sealUI;
        [SerializeField] private Transform castPoint;
        [SerializeField] private Transform lockOnTarget;

        [Header("Prefabs")]
        [SerializeField] private SpellProjectile projectilePrefab;
        [SerializeField] private SpellAoE aoePrefab;
        [SerializeField] private SpellDebuff debuffPrefab;
        [SerializeField] private SpellTotem totemPrefab;

        [Header("Charge Settings")]
        [SerializeField] private float baseAttackChargeTime = 1f;
        [SerializeField] private float timePerChargeLevel = 2f;
        [SerializeField] private int maxChargeLevels = 3;

        private bool isCharging = false;
        private float currentChargeTime = 0f;
        private bool isHardLocked = false;

        private EnergyDataSO currentEnergy;
        private List<ComboStep> currentCombo = new List<ComboStep>();

        private List<ComboStep> comboBeingCharged = null;
        private bool isChargingInnate = false;

        public EnergyDataSO CurrentEnergy => currentEnergy;

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
            if (energy1 != null) SetEnergy(energy1);
        }

        private void Update()
        {
            HandleEnergyHotkeys();

            if (isCharging)
            {
                currentChargeTime += Time.deltaTime;
                float absoluteMaxTime = baseAttackChargeTime + (maxChargeLevels * timePerChargeLevel);
                currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, absoluteMaxTime);
            }
        }

        public void BeginCharge()
        {
            isCharging = true;
            currentChargeTime = 0f;
            comboBeingCharged = null;
            isChargingInnate = false;
        }

        public void BeginCharge(List<ComboStep> comboToCharge, bool isInnate)
        {
            isCharging = true;
            currentChargeTime = 0f;
            comboBeingCharged = comboToCharge;
            isChargingInnate = isInnate;
        }

        public void CancelCharge()
        {
            isCharging = false;
            currentChargeTime = 0f;
            comboBeingCharged = null;
            isChargingInnate = false;
        }

        public void ReleaseCast()
        {
            // ЖЕЛЕЗОБЕТОННАЯ ПРОВЕРКА: Читаем статическое поле напрямую.
            // Если кнопку утащили за пределы джойстика - отменяем выстрел!
            if (DraggableAttackButton.IsDraggingForSeal)
            {
                Debug.Log("<color=yellow>[SpellCaster]</color> Кнопка утянута в слот! Выстрел отменен.");
                return;
            }

            if (!isCharging) return;
            isCharging = false;

            int chargeLevel = GetCurrentChargeLevel();

            List<ComboStep> actualCombo;
            bool isInnate = false;

            if (comboBeingCharged != null)
            {
                actualCombo = comboBeingCharged;
                isInnate = isChargingInnate;
            }
            else
            {
                if (currentCombo.Count == 0)
                {
                    actualCombo = new List<ComboStep> { new ComboStep { shape = ShapeType.Triangle, energy = currentEnergy } };
                    isInnate = true;
                }
                else
                {
                    actualCombo = currentCombo;
                    isInnate = false;
                }
            }

            int maxAllowedByRunes = isInnate ? 1 : actualCombo.Count;
            chargeLevel = Mathf.Min(chargeLevel, maxAllowedByRunes);
            float multiplier = GetChargeMultiplier(chargeLevel);

            Debug.Log($"<color=orange>[SpellCaster]</color> Выстрел! Удержание: {currentChargeTime:F1}с. Заряд: {chargeLevel}, Множитель: {multiplier}x");

            ExecuteComboChain(actualCombo, chargeLevel, multiplier, isInnate);

            comboBeingCharged = null;
            isChargingInnate = false;
        }

        public int GetCurrentChargeLevel()
        {
            if (currentChargeTime <= baseAttackChargeTime) return 0;
            float extraTime = currentChargeTime - baseAttackChargeTime;
            int level = 1 + Mathf.FloorToInt(extraTime / timePerChargeLevel);
            return Mathf.Min(level, maxChargeLevels);
        }

        public float GetChargeMultiplier(int level)
        {
            return 1f + (level * 0.5f);
        }

        // ====================== СМЕНА ЭНЕРГИИ ======================

        private void HandleEnergyHotkeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame && energy1 != null) SetEnergy(energy1);
            if (keyboard.digit2Key.wasPressedThisFrame && energy2 != null) SetEnergy(energy2);
            if (keyboard.digit3Key.wasPressedThisFrame && energy3 != null) SetEnergy(energy3);
        }

        // НОВЫЕ МЕТОДЫ ДЛЯ КНОПОК UI
        public void SelectEnergy1() { if (energy1 != null) SetEnergy(energy1); }
        public void SelectEnergy2() { if (energy2 != null) SetEnergy(energy2); }
        public void SelectEnergy3() { if (energy3 != null) SetEnergy(energy3); }

        public void SetEnergy(EnergyDataSO newEnergy)
        {
            currentEnergy = newEnergy;
            if (runeDrawer != null && currentEnergy != null)
                runeDrawer.SetLineColor(currentEnergy.primaryColor);
        }

        // ====================== ЛОГИКА ЦЕПОЧЕК ======================

        private void HandleShapeRecognized(ShapeType shapeType, float accuracy)
        {
            if (currentCombo.Count >= maxUnlockedRank) return;

            ComboStep newStep = new ComboStep { shape = shapeType, energy = currentEnergy };
            currentCombo.Add(newStep);

            if (sealUI != null && currentEnergy != null)
                sealUI.DisplayRune(currentCombo.Count - 1, shapeType, currentEnergy.primaryColor);
        }

        public void SetTarget(Transform target, bool hardLocked)
        {
            lockOnTarget = target;
            isHardLocked = hardLocked;
        }

        public void SetTarget(Transform target)
        {
            lockOnTarget = target;
            isHardLocked = false;
        }

        private void ExecuteComboChain(List<ComboStep> combo, int chargeLvl, float multiplier, bool isInnate)
        {
            SpellNode rootNode = BuildSpellChain(combo);
            if (rootNode == null) return;

            Vector3 startPos = castPoint != null ? castPoint.position : (transform.position + transform.forward);
            SpellContext initialContext = new SpellContext
            {
                Caster = this.transform,
                HitPosition = startPos,
                // ИСПРАВЛЕНИЕ: Берем направление строго от Cast Point (куда вы его повернули), а не от тела!
                Direction = castPoint != null ? castPoint.forward : transform.forward,
                Target = lockOnTarget,
                IsHardLocked = isHardLocked,
                ChargeLevel = chargeLvl,
                ChargeMultiplier = multiplier,
                IsChainCast = false,
                IsInnate = isInnate
            };

            rootNode.Execute(initialContext);
        }

        public void CastSealedCombo(List<SpellCaster.ComboStep> comboToCast, int savedChargeLvl, float savedMultiplier, bool isInnate)
        {
            if (comboToCast == null || comboToCast.Count == 0) return;
            ExecuteComboChain(comboToCast, savedChargeLvl, savedMultiplier, isInnate);
        }

        private SpellNode BuildSpellChain(List<ComboStep> combo)
        {
            SpellNode nextNode = null;
            for (int i = combo.Count - 1; i >= 0; i--)
            {
                ComboStep step = combo[i];
                SpellNode currentNode = CreateNodeForShape(step.shape, step.energy);
                if (currentNode != null)
                {
                    currentNode.NextNode = nextNode;
                    nextNode = currentNode;
                }
            }
            return nextNode;
        }

        private SpellNode CreateNodeForShape(ShapeType shape, EnergyDataSO energy)
        {
            return shape switch
            {
                ShapeType.Triangle => new TriangleNode(projectilePrefab, energy),
                ShapeType.Circle => new CircleNode(aoePrefab, energy),
                ShapeType.Square => new SquareNode(debuffPrefab, totemPrefab, projectilePrefab, energy),
                _ => null
            };
        }

        public void ResetCombo()
        {
            currentCombo.Clear();
            CancelCharge();
            if (sealUI != null) sealUI.ClearAllSlots();
        }

        public bool HasActiveCombo()
        {
            return currentCombo != null;
        }

        public List<SpellCaster.ComboStep> GetCurrentComboCopy()
        {
            return new List<SpellCaster.ComboStep>(currentCombo);
        }
    }
}