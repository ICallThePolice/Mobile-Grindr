using System;
using System.Collections;
using UnityEngine;
using SpellSystem.Data;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    public class SpellDebuff : MonoBehaviour
    {
        [Header("Tick Settings")]
        [SerializeField] private float duration = 5f;
        [SerializeField] private float tickInterval = 1f;

        [Header("Visual Settings")]
        [SerializeField] private float rotationSpeed = 180f; // ИСПРАВЛЕНИЕ: Скорость вращения вокруг врага

        private Transform target;
        private EnergyDataSO energyData;
        private Action onTickCallback;
        private float tickDamage;

        public void Initialize(Transform targetTransform, float damage, EnergyDataSO energy, float chargeMultiplier, Action tickAction = null)
        {
            this.target = targetTransform;
            this.energyData = energy;
            this.onTickCallback = tickAction;

            this.tickDamage = damage * chargeMultiplier * 0.5f;
            this.duration *= chargeMultiplier;

            float visualBoost = 1f + ((chargeMultiplier - 1f) * 0.3f);
            transform.localScale *= visualBoost;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (var rend in renderers)
            {
                if (energy != null) rend.material.color = energy.primaryColor;
            }

            StartCoroutine(TickRoutine());
            Destroy(gameObject, duration);
        }

        private IEnumerator TickRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(tickInterval);

            while (true)
            {
                yield return wait;
                if (target == null) break;

                DummyTarget dummy = target.GetComponent<DummyTarget>();
                if (dummy != null)
                {
                    string energyName = energyData != null ? energyData.energyName : "Дебафф";
                    Color energyColor = energyData != null ? energyData.primaryColor : Color.white;
                    dummy.TakeDamage(tickDamage, energyName, energyColor);
                }

                onTickCallback?.Invoke();
            }
        }

        private void Update()
        {
            if (target != null && transform.parent == null)
            {
                // ИСПРАВЛЕНИЕ: Держимся на цели и красиво кружимся вокруг нее!
                transform.position = target.position;
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
        }
    }
}