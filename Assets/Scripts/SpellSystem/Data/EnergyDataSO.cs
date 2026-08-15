using UnityEngine;

namespace SpellSystem.Data
{
    [CreateAssetMenu(fileName = "Energy_", menuName = "Spell System/Energy Data")]
    public class EnergyDataSO : ScriptableObject
    {
        public EnergyType energyType;
        public string energyName;

        [Header("Visuals")]
        public Color primaryColor = Color.white;
        public Color trailColor = Color.cyan; // Цвет линии на экране и следа снаряда
        public GameObject castVfxPrefab;
        public GameObject impactVfxPrefab;

        [Header("Resource & Base Stats")]
        [Tooltip("Базовая стоимость в выбранном типе энергии (например, 1.0)")]
        public float baseEnergyCost = 1.0f;

        [Tooltip("Базовый урон, эквивалентный 1.0 ед. затраченной энергии")]
        public float baseDamage = 1.0f;
    }
}