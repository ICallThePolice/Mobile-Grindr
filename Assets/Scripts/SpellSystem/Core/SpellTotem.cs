using System;
using System.Collections;
using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    public class SpellTotem : MonoBehaviour
    {
        [Header("Totem Settings")]
        [SerializeField] private float duration = 6f;     // Время жизни тотема
        [SerializeField] private float tickInterval = 1.2f; // Частота импульсов

        private EnergyDataSO energyData;
        private Action onTickCallback;
        private bool isActive = false;

        public void Initialize(EnergyDataSO energy, Action tickAction)
        {
            this.energyData = energy;
            this.onTickCallback = tickAction;
            this.isActive = true;

            // Визуальная окраска тотема под цвет энергии
            var rend = GetComponent<Renderer>();
            if (rend != null && energy != null)
            {
                rend.material.color = energy.primaryColor;
            }

            // Запускаем цикл тиков
            StartCoroutine(TotemRoutine());

            // Уничтожаем тотем по истечении времени
            Destroy(gameObject, duration);
        }

        private IEnumerator TotemRoutine()
        {
            // Ждем один кадр на случай, если объекты еще не до конца инициализировались
            yield return null;

            while (isActive)
            {
                yield return new WaitForSeconds(tickInterval);

                try
                {
                    if (onTickCallback != null)
                    {
                        onTickCallback.Invoke();
                        Debug.Log($"<color=green>[SpellTotem]</color> Тик прошел успешно! Энергия: {energyData?.energyName}");
                    }
                    else
                    {
                        Debug.LogWarning("[SpellTotem] Ошибка: колбэк тика пуст!");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[SpellTotem] Ошибка во время выполнения тика: {ex.Message}\n{ex.StackTrace}");
                    isActive = false; // Останавливаем цикл при критической ошибке
                }
            }
        }

        private void OnDestroy()
        {
            isActive = false;
        }
    }
}