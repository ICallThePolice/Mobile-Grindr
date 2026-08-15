using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    public class SpellProjectile : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed = 25f;
        [SerializeField] private float lifetime = 5f;

        private float damage;
        private EnergyDataSO energyData;
        private Transform target;

        public void Initialize(float spellDamage, EnergyDataSO energy, Transform targetTransform)
        {
            damage = spellDamage;
            energyData = energy;
            target = targetTransform;

            Destroy(gameObject, lifetime);

            // Окрашиваем детские объекты/свет в цвет выбранной энергии
            var lightComp = GetComponentInChildren<Light>();
            if (lightComp != null) lightComp.color = energy.primaryColor;
        }

        private void Update()
        {
            if (target != null)
            {
                // Наведение на залоченную цель
                Vector3 dir = (target.position + Vector3.up - transform.position).normalized;
                transform.forward = Vector3.Lerp(transform.forward, dir, Time.deltaTime * 10f);
            }

            transform.position += transform.forward * (speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Проверка попадания во врага или преграду
            if (other.CompareTag("Enemy") || other.CompareTag("Environment"))
            {
                // Эффект попадания (Impact VFX)
                if (energyData != null && energyData.impactVfxPrefab != null)
                {
                    Instantiate(energyData.impactVfxPrefab, transform.position, Quaternion.identity);
                }

                Debug.Log($"<color=red>[ПАКЕТ УРОНА]</color> {other.name} получил {damage:F1} урона стихией {energyData?.energyName}");

                Destroy(gameObject);
            }
        }
    }
}