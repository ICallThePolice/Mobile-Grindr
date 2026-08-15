using System;
using System.Collections;
using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    public class SpellShield : MonoBehaviour
    {
        [Header("Tick Settings")]
        [SerializeField] private float duration = 5f;     // Общее время жизни ауры
        [SerializeField] private float tickInterval = 1f; // Интервал между импульсами (тиками)

        private Transform target;
        private EnergyDataSO energyData;
        private Action onTickCallback;

        public void Initialize(Transform targetTransform, EnergyDataSO energy, Action tickAction = null)
        {
            this.target = targetTransform;
            this.energyData = energy;
            this.onTickCallback = tickAction;

            // Окрашиваем ауру в цвет энергии
            var rend = GetComponent<Renderer>();
            if (rend != null && energy != null)
            {
                rend.material.color = energy.primaryColor;
            }

            // Запускаем периодические тики
            StartCoroutine(TickRoutine());

            // Уничтожаем ауру по истечении длительности
            Destroy(gameObject, duration);
        }

        private IEnumerator TickRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(tickInterval);

            // Ждем первый интервал перед первым тиком
            while (true)
            {
                yield return wait;

                if (target == null) break;

                // Срабатывает импульс тика!
                onTickCallback?.Invoke();

                Debug.Log($"[SpellShield] Импульс тика! Энергия: {energyData?.energyName}");
            }
        }

        private void Update()
        {
            // Если аура не прикреплена в иерархии, плавно следуем за целью
            if (target != null && transform.parent == null)
            {
                transform.position = target.position;
            }
        }
    }
}