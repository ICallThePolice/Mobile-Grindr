using UnityEngine;
using SpellSystem.Data;
using SpellSystem.Testing; // Подключаем пространство имен манекенов

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
            targetRadius = Mathf.Max(radius, 2.0f); // Защита от 0 радиуса[cite: 9]
            energyData = energy;

            // Устанавливаем начальный плоский размер[cite: 9]
            transform.localScale = new Vector3(0.1f, ringHeight, 0.1f);

            // Окрашиваем материал зоны в цвет выбранной энергии[cite: 9]
            var renderer = GetComponent<Renderer>();
            if (renderer != null && energy != null)
            {
                renderer.material.color = energy.primaryColor;
            }
        }

        private void Update()
        {
            // Плавное расширение круга в плоскости земли (X и Z)[cite: 9]
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

            // Поиск всех врагов внутри радиуса[cite: 9]
            Collider[] hitEnemies = Physics.OverlapSphere(transform.position, targetRadius / 2f, enemyLayer);

            foreach (var enemyCollider in hitEnemies)
            {
                // Ищем компонент манекена на объекте и наносим ему урон
                DummyTarget target = enemyCollider.GetComponent<DummyTarget>();
                if (target != null)
                {
                    string energyName = energyData != null ? energyData.energyName : "Энергия";

                    // Реальное нанесение урона (теперь ХП манекена будет уменьшаться)
                    target.TakeDamage(damage, energyName);
                }
            }

            // Исчезновение[cite: 9]
            Destroy(gameObject, 0.3f);
        }
    }
}