using UnityEngine;
using SpellSystem.Data;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    public class SpellAoE : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float baseExpandSpeed = 6f;
        [SerializeField] private float ringHeight = 0.1f;
        [SerializeField] private LayerMask enemyLayer;

        private float expandSpeed;
        private float targetRadius;
        private float currentRadius = 0.1f;
        private float damage;
        private EnergyDataSO energyData;
        private bool hasTriggered = false;

        // ИСПРАВЛЕНИЕ 2: Добавили int chargeLevel, чтобы точно знать стадию заряда
        public void Initialize(float spellDamage, float radius, EnergyDataSO energy, float chargeMultiplier = 1f, int chargeLevel = 0)
        {
            float slightDamageMult = 1f + ((chargeMultiplier - 1f) * 0.5f);
            this.damage = spellDamage * slightDamageMult;

            this.targetRadius = Mathf.Max(radius * chargeMultiplier, 2.0f);
            this.energyData = energy;

            // КАСТОМНЫЕ СКОРОСТИ РАСШИРЕНИЯ ДЛЯ КРУГА: 1x, 1.5x, 2x, 4x
            float[] speedMultipliers = { 1f, 1.5f, 2f, 4f };
            // Защита от выхода за пределы массива (Mathf.Clamp)
            int clampedLevel = Mathf.Clamp(chargeLevel, 0, speedMultipliers.Length - 1);

            this.expandSpeed = baseExpandSpeed * speedMultipliers[clampedLevel];

            transform.localScale = new Vector3(0.1f, ringHeight, 0.1f);

            var renderer = GetComponent<Renderer>();
            if (renderer != null && energy != null)
            {
                renderer.material.color = energy.primaryColor;
            }
        }

        private void Update()
        {
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

            Collider[] hitEnemies = Physics.OverlapSphere(transform.position, targetRadius / 2f, enemyLayer);

            foreach (var enemyCollider in hitEnemies)
            {
                DummyTarget target = enemyCollider.GetComponent<DummyTarget>();
                if (target != null)
                {
                    string energyName = energyData != null ? energyData.energyName : "Энергия";

                    // --- НОВОЕ: Передаем цвет энергии в круг ---
                    Color energyColor = energyData != null ? energyData.primaryColor : Color.white;

                    target.TakeDamage(damage, energyName, energyColor);
                }
            }

            Destroy(gameObject, 0.3f);
        }
    }
}