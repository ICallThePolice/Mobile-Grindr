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
        [SerializeField] private float baseSpeed = 20f;
        [SerializeField] private float lifetime = 5f;
        [SerializeField] private float homingSensitivity = 10f;

        [Header("Fail-Safe Settings")]
        [SerializeField] private float autoHitDistance = 1.5f;

        private float speed;
        private float damage;
        private EnergyDataSO energyData;
        private Rigidbody rb;
        private Transform homingTarget;
        private Transform sourceTarget;
        private bool hasHit = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.isKinematic = false;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            GetComponent<Collider>().isTrigger = true;
            Destroy(gameObject, lifetime);
        }

        public void Initialize(float damage, EnergyDataSO energy, Transform target = null, Transform sourceTarget = null, float chargeMultiplier = 1f)
        {
            this.damage = damage * chargeMultiplier;
            this.energyData = energy;
            this.homingTarget = target;
            this.sourceTarget = sourceTarget;

            transform.localScale *= chargeMultiplier;
            this.speed = baseSpeed / chargeMultiplier;

            if (energy != null)
            {
                var rend = GetComponent<Renderer>();
                if (rend != null) rend.material.color = energy.primaryColor;
            }

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

        private void FixedUpdate()
        {
            if (homingTarget != null && rb != null && !hasHit)
            {
                float distance = Vector3.Distance(transform.position, homingTarget.position);
                if (distance <= autoHitDistance)
                {
                    ForceHit(homingTarget);
                    return;
                }

                Vector3 directionToTarget = (homingTarget.position - transform.position).normalized;
                Vector3 newDirection = Vector3.Slerp(rb.linearVelocity.normalized, directionToTarget, homingSensitivity * Time.fixedDeltaTime);

                rb.linearVelocity = newDirection * speed;
                transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit) return;

            // 1. ИГНОР ДРУГИХ СПЕЛЛОВ: Снаряды не должны сбивать друг друга или АоЕ круги!
            if (other.GetComponent<SpellProjectile>() != null || other.GetComponent<SpellDebuff>() != null) return;

            // 2. ИГНОР ИГРОКА: Железобетонная защита по тегу и скриптам
            if (other.CompareTag("Player") ||
                other.GetComponentInParent<MobilePlayerController>() != null ||
                other.GetComponentInParent<SpellCaster>() != null)
            {
                return;
            }

            // 3. ИГНОР ИСТОЧНИКА: Тотема или врага, из которого вылетел снаряд
            if (sourceTarget != null && (other.transform == sourceTarget || other.transform.IsChildOf(sourceTarget)))
            {
                return;
            }

            ForceHit(other.transform);
        }

        private void ForceHit(Transform hitTransform)
        {
            if (hasHit) return;
            hasHit = true;

            // ИСПРАВЛЕНИЕ: Ищем компонент манекена глубоко, даже если попали в его руку/дочерний коллайдер
            DummyTarget target = hitTransform.GetComponentInParent<DummyTarget>();

            if (target != null)
            {
                string energyName = energyData != null ? energyData.energyName : "Без энергии";
                Color energyColor = energyData != null ? energyData.primaryColor : Color.white;

                target.TakeDamage(damage, energyName, energyColor);
            }

            OnImpact?.Invoke(transform.position, hitTransform);
            Destroy(gameObject);
        }
    }
}