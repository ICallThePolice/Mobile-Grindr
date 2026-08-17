using System;
using System.Collections;
using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    public class SpellTotem : MonoBehaviour
    {
        [Header("Totem Settings")]
        [SerializeField] private float duration = 6f;
        [SerializeField] private float tickInterval = 1.2f;

        [Header("Orbit Settings")]
        [Range(0.5f, 5f)] // Ползунок от 0.5 до 5 метров
        [SerializeField] private float orbitRadius = 1.5f;   // Сделали поближе по умолчанию

        [Range(10f, 360f)] // Ползунок скорости
        [SerializeField] private float orbitSpeed = 180f;

        [Range(0f, 3f)] // Ползунок высоты тотема над землей
        [SerializeField] private float heightOffset = 1f;

        private EnergyDataSO energyData;
        private Action onTickCallback;
        private bool isActive = false;
        private int rank = 1;

        // Переменные для орбиты
        private Transform orbitCenter;
        private float currentAngle = 0f;

        // В Initialize добавился параметр Transform center
        public void Initialize(EnergyDataSO energy, float chargeMultiplier, Transform center, Action tickAction)
        {
            this.energyData = energy;
            this.onTickCallback = tickAction;
            this.orbitCenter = center;
            this.isActive = true;

            this.duration *= chargeMultiplier;

            float visualBoost = 1f + ((chargeMultiplier - 1f) * 0.3f);
            transform.localScale *= visualBoost;

            var rend = GetComponent<Renderer>();
            if (rend != null && energy != null) rend.material.color = energy.primaryColor;

            // Вычисляем начальный угол, чтобы тотем спавнился ровно перед игроком
            if (orbitCenter != null)
            {
                Vector3 forward = orbitCenter.forward;
                currentAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            }

            StartCoroutine(TotemRoutine());
            Destroy(gameObject, duration);
        }

        private void Update()
        {
            // Двигаем тотем по орбите каждый кадр
            if (isActive && orbitCenter != null)
            {
                currentAngle += orbitSpeed * Time.deltaTime;
                currentAngle %= 360f; // Держим угол в пределах 0-360 градусов

                // Переводим градусы в радианы для математических функций
                float rad = currentAngle * Mathf.Deg2Rad;

                // Вычисляем позицию на окружности (X и Z)
                float x = Mathf.Sin(rad) * orbitRadius;
                float z = Mathf.Cos(rad) * orbitRadius;

                // Устанавливаем новую позицию относительно центра (кастера)
                Vector3 orbitPosition = orbitCenter.position + new Vector3(x, heightOffset, z);
                transform.position = orbitPosition;

                // Поворачиваем тотем так, чтобы он всегда "смотрел" по ходу движения орбиты 
                // (или можно оставить Quaternion.identity, если он не должен вращаться вокруг своей оси)
                Vector3 lookDirection = new Vector3(Mathf.Cos(rad), 0, -Mathf.Sin(rad));
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
            else if (isActive && orbitCenter == null)
            {
                // Если кастер умер/исчез, тотем должен разрушиться
                Destroy(gameObject);
            }
        }

        public void MutateTotem(EnergyDataSO newEnergy)
        {
            rank++;
            var rend = GetComponent<Renderer>();
            if (rend != null) rend.material.color = Color.Lerp(rend.material.color, Color.white, 0.2f);

            // Ускоряем вращение при мутации для визуального эффекта (опционально)
            orbitSpeed += 50f;
        }

        private IEnumerator TotemRoutine()
        {
            yield return null;
            while (isActive)
            {
                yield return new WaitForSeconds(tickInterval);
                try
                {
                    if (onTickCallback != null) onTickCallback.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SpellTotem] Error: {ex.Message}");
                    isActive = false;
                }
            }
        }

        private void OnDestroy() => isActive = false;
    }
}