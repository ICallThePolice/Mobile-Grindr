using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    public class SpellAoE : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float expandSpeed = 12f;
        [SerializeField] private float ringHeight = 0.1f; // Плоская зона на земле
        [SerializeField] private LayerMask enemyLayer;

        private float targetRadius;
        private float currentRadius = 0.1f;
        private float damage;
        private EnergyDataSO energyData;
        private bool hasTriggered = false;

        public void Initialize(float spellDamage, float radius, EnergyDataSO energy)
        {
            damage = spellDamage;
            targetRadius = Mathf.Max(radius, 2.0f); // Защита от 0 радиуса
            energyData = energy;

            // Устанавливаем начальный плоский размер
            transform.localScale = new Vector3(0.1f, ringHeight, 0.1f);

            // Окрашиваем материал зоны в цвет выбранной энергии
            var renderer = GetComponent<Renderer>();
            if (renderer != null && energy != null)
            {
                renderer.material.color = energy.primaryColor;
            }
        }

        private void Update()
        {
            // Плавное расширение круга в плоскости земли (X и Z)
            if (currentRadius < targetRadius)
            {
                currentRadius += expandSpeed * Time.deltaTime;
                transform.localScale = new Vector3(currentRadius, ringHeight, currentRadius);
            }
            else if (!hasTriggered)
            {
                ApplyAoEDamage();
            }
        }

        private void ApplyAoEDamage()
        {
            hasTriggered = true;

            // Поиск всех врагов внутри радиуса
            Collider[] hitEnemies = Physics.OverlapSphere(transform.position, targetRadius / 2f, enemyLayer);
            foreach (var enemy in hitEnemies)
            {
                Debug.Log($"<color=red>[AoE УРОН]</color> {enemy.name} получил {damage:F1} урона от {energyData?.energyName}");
            }

            // Исчезновение
            Destroy(gameObject, 0.3f);
        }
    }
}