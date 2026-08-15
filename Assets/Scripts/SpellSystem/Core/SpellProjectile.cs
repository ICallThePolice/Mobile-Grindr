using System;
using UnityEngine;
using SpellSystem.Data;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public class SpellProjectile : MonoBehaviour
    {
        public event Action<Vector3, Transform> OnImpact;

        [Header("Settings")]
        [SerializeField] private float speed = 20f;
        [SerializeField] private float lifetime = 5f;
        [Tooltip("Скорость доводки на цель. 0 - летит прямо, 50 - резкий поворот")]
        [SerializeField] private float homingSensitivity = 10f;

        private float damage;
        private EnergyDataSO energyData;
        private Rigidbody rb;
        private Transform homingTarget; // Сохраненная цель

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            GetComponent<Collider>().isTrigger = true;
            Destroy(gameObject, lifetime);
        }

        // Добавили параметр sourceTarget
        public void Initialize(float damage, EnergyDataSO energy, Transform target = null, Transform sourceTarget = null)
        {
            this.damage = damage;
            this.energyData = energy;
            this.homingTarget = target;

            if (energy != null)
            {
                var rend = GetComponent<Renderer>();
                if (rend != null) rend.material.color = energy.primaryColor;
            }

            // ИГНОРИРУЕМ КОЛЛИЗИЮ с источником (работает для любых размеров мешей и коллайдеров)
            if (sourceTarget != null)
            {
                Collider projCollider = GetComponent<Collider>();
                Collider[] sourceColliders = sourceTarget.GetComponentsInChildren<Collider>();

                foreach (var sourceCol in sourceColliders)
                {
                    if (sourceCol != null && projCollider != null)
                    {
                        Physics.IgnoreCollision(projCollider, sourceCol, true);
                    }
                }
            }

            rb.linearVelocity = transform.forward * speed;
        }

        // Используем FixedUpdate для плавной физической доводки
        private void FixedUpdate()
        {
            if (homingTarget != null && rb != null)
            {
                // Находим направление к цели
                Vector3 directionToTarget = (homingTarget.position - transform.position).normalized;

                // Плавно смешиваем текущий вектор полета с вектором на цель
                Vector3 newDirection = Vector3.Slerp(rb.linearVelocity.normalized, directionToTarget, homingSensitivity * Time.fixedDeltaTime);

                // Применяем новую скорость и поворачиваем нос снаряда по курсу
                rb.linearVelocity = newDirection * speed;
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player")) return;

            DummyTarget target = other.GetComponent<DummyTarget>();
            if (target == null) return;

            string energyName = energyData != null ? energyData.energyName : "Без энергии";
            target.TakeDamage(damage, energyName);

            OnImpact?.Invoke(transform.position, other.transform);

            Destroy(gameObject);
        }
    }
}