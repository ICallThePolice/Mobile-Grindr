using UnityEngine;

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

        // Метод получения урона
        public void TakeDamage(float amount, string energyName)
        {
            currentHealth -= amount;
            Debug.Log($"[Манекен {gameObject.name}] Получил <color=red>{amount}</color> урона от энергии <color=cyan>{energyName}</color>. ХП: {currentHealth}");

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
            // Для тестов можно просто восстанавливать ХП вместо удаления объекта
            currentHealth = maxHealth;
        }
    }
}