using UnityEngine;
using System.Collections.Generic;

namespace SpellSystem.UI
{
    public class DamageNumberManager : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private DamageNumber prefab;
        [SerializeField] private int poolSize = 30; // Сколько цифр одновременно может быть на экране

        private Queue<DamageNumber> pool = new Queue<DamageNumber>();
        private Camera mainCamera;

#pragma warning disable UAL0013 // Подавляем предупреждение анализатора Unity
        public static DamageNumberManager Instance { get; private set; }
#pragma warning restore UAL0013

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializePool(); // <-- ВОТ ЭТО МЫ ЗАБЫЛИ!
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Создаем запас цифр при запуске игры, чтобы не тормозить систему инстанцированием в бою
        private void InitializePool()
        {
            if (prefab == null)
            {
                Debug.LogError("DamageNumberManager: Префаб цифры не назначен в инспекторе!");
                return;
            }

            for (int i = 0; i < poolSize; i++)
            {
                DamageNumber num = Instantiate(prefab, transform);
                num.gameObject.SetActive(false); // Прячем их до нужного момента
                pool.Enqueue(num);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Универсальный метод вызова урона. Можно вызывать откуда угодно!
        /// Пример: DamageNumberManager.Instance.SpawnDamage(enemy.transform.position, 50f, Color.red);
        /// </summary>
        public void SpawnDamage(Vector3 targetPosition, float damageAmount, Color energyColor)
        {
            if (mainCamera == null) mainCamera = Camera.main;

            if (pool.Count == 0) return;

            // Достаем самую старую цифру из очереди
            DamageNumber num = pool.Dequeue();

            // Перезапускаем её и настраиваем значения
            num.gameObject.SetActive(true);
            num.Initialize(damageAmount, energyColor, targetPosition, mainCamera);

            // Возвращаем в конец очереди (карусель объектов)
            pool.Enqueue(num);
        }
    }
}