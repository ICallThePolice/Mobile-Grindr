using UnityEngine;
using System.Collections; // Обязательно для корутин
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
        private GameObject vfxInstance; // Сохраняем ссылку на VFX, чтобы удалить его вместе с АоЕ

        public void Initialize(float spellDamage, float radius, EnergyDataSO energy, float chargeMultiplier = 1f, int chargeLevel = 0)
        {
            float slightDamageMult = 1f + ((chargeMultiplier - 1f) * 0.5f);
            this.damage = spellDamage * slightDamageMult;

            this.targetRadius = Mathf.Max(radius * chargeMultiplier, 2.0f);
            this.energyData = energy;

            float[] speedMultipliers = { 1f, 1.5f, 2f, 4f };
            int clampedLevel = Mathf.Clamp(chargeLevel, 0, speedMultipliers.Length - 1);
            this.expandSpeed = baseExpandSpeed * speedMultipliers[clampedLevel];

            transform.localScale = new Vector3(0.1f, ringHeight, 0.1f);

            var renderer = GetComponent<Renderer>();
            if (renderer != null && energy != null)
            {
                renderer.material.color = energy.primaryColor;
            }

            if (energy != null && energy.impactVfxPrefab != null)
            {
                Vector3 vfxPos = transform.position + Vector3.up * 0.1f;
                vfxInstance = Instantiate(energy.impactVfxPrefab, vfxPos, Quaternion.identity);
                vfxInstance.transform.localScale = new Vector3(chargeMultiplier, chargeMultiplier, chargeMultiplier);

                if (renderer != null) renderer.enabled = false;
            }
        }

        private void Update()
        {
            if (currentRadius < targetRadius)
            {
                currentRadius += expandSpeed * Time.deltaTime;
                transform.localScale = new Vector3(currentRadius, ringHeight, currentRadius);

                if (!hasTriggered && currentRadius >= targetRadius * 0.3f)
                {
                    StartCoroutine(DamageRoutine());
                }
            }
            else if (!hasTriggered)
            {
                StartCoroutine(DamageRoutine());
            }
        }

        private IEnumerator DamageRoutine()
        {
            hasTriggered = true;
            string eName = energyData != null ? energyData.name.ToLower() : "";
            if (energyData != null && !string.IsNullOrEmpty(energyData.energyName))
            {
                eName = energyData.energyName.ToLower();
            }

            // --- ЛОГИКА ЭРЕБА: 5 УДАРОВ ---
            if (eName.Contains("ereb"))
            {
                for (int i = 0; i < 5; i++) // ИСПРАВЛЕНИЕ: Теперь бьет 5 раз
                {
                    PerformDamagePulse();
                    yield return new WaitForSeconds(1f); // Ждем 1 секунду между ударами
                }

                // Удаляем объекты после завершения тиков
                if (vfxInstance != null) Destroy(vfxInstance);
                Destroy(gameObject);
            }
            // --- ЛОГИКА ОСТАЛЬНЫХ СТИХИЙ: 1 УДАР ---
            else
            {
                PerformDamagePulse();
                yield return new WaitForSeconds(0.5f); // Даем время VFX проиграться

                if (vfxInstance != null) Destroy(vfxInstance);
                Destroy(gameObject);
            }
        }

        private void PerformDamagePulse()
        {
            Collider[] hitEnemies = Physics.OverlapSphere(transform.position, targetRadius / 2f, enemyLayer);

            foreach (var enemyCollider in hitEnemies)
            {
                DummyTarget target = enemyCollider.GetComponent<DummyTarget>();
                if (target != null)
                {
                    string energyName = energyData != null ? energyData.energyName : "Энергия";
                    Color energyColor = energyData != null ? energyData.primaryColor : Color.white;
                    target.TakeDamage(damage, energyName, energyColor);
                }
            }
        }
    }
}