using UnityEngine;

namespace SpellSystem.Data
{
    [CreateAssetMenu(fileName = "Shape_", menuName = "Spell System/Shape Data")]
    public class ShapeDataSO : ScriptableObject
    {
        public ShapeType shapeType;
        public string shapeName;
        public Sprite shapeIcon;

        [Header("Modifiers & Area")]
        [Tooltip("Множитель области или радиуса зоны (для Круга)")]
        public float baseRadius = 3.0f;

        [Tooltip("Множитель дальности (для Треугольника/Вектора)")]
        public float maxRange = 15.0f;
    }
}