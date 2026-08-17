using UnityEngine;
using System.Collections.Generic;

namespace SpellSystem.UI
{
    public class DamageNumberManager : MonoBehaviour
    {
        // Паттерн Singleton для вызова из любой точки игры без ссылок
        public static DamageNumberManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private DamageNumber prefab;
        [SerializeField] private int poolSize = 30; // Сколько цифр одновременно может быть на экране

        private Queue<DamageNumber> pool = new Queue<DamageNumber>();
        private Camera mainCamera;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            mainCamera = Camera.main;

            // Заранее создаем пул выключенных объектов
            for (int i = 0; i < poolSize; i++)
            {
                DamageNumber num = Instantiate(prefab, transform);
                num.gameObject.SetActive(false);
                pool.Enqueue(num);
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

            // Если она почему-то еще летит, мы её принудительно перезапустим
            num.gameObject.SetActive(true);
            num.Initialize(damageAmount, energyColor, targetPosition, mainCamera);

            // Возвращаем в конец очереди (карусель объектов)
            pool.Enqueue(num);
        }
    }
}