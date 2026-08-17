using UnityEngine;
using SpellSystem.UI; // Обязательно подключаем UI для работы с DamageNumberManager

namespace SpellSystem.Testing
{
    public class DummyTarget : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        private float currentHealth;
        private Renderer meshRenderer;
        private Color originalColor;

        private void Awake()
        {
            currentHealth = maxHealth;
            meshRenderer = GetComponent<Renderer>();
            if (meshRenderer != null)
            {
                originalColor = meshRenderer.material.color;
            }
        }

        // ИСПРАВЛЕНИЕ: Добавили Color damageColor, чтобы снаряды могли передавать цвет своей энергии.
        public void TakeDamage(float amount, string energyName, Color damageColor = default)
        {
            currentHealth -= amount;
            Debug.Log($"[Манекен {gameObject.name}] Получил <color=red>{amount}</color> урона от энергии <color=cyan>{energyName}</color>. ХП: {currentHealth}");

            // --- СПАВН ЦИФР УРОНА ---
            // Если цвет не передан (равен default), делаем его красным по умолчанию
            if (damageColor == default) damageColor = Color.red;

            if (DamageNumberManager.Instance != null)
            {
                // Вызываем вылет цифры!
                DamageNumberManager.Instance.SpawnDamage(transform.position, amount, damageColor);
            }
            // ------------------------

            // Визуальный отклик (мигание белым при попадании)
            if (meshRenderer != null)
            {
                meshRenderer.material.color = Color.white;
                Invoke(nameof(ResetColor), 0.15f);
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void ResetColor()
        {
            if (meshRenderer != null) meshRenderer.material.color = originalColor;
        }

        private void Die()
        {
            Debug.Log($"[Манекен {gameObject.name}] Уничтожен!");
            // Для тестов просто восстанавливаем ХП
            currentHealth = maxHealth;
        }
    }
}