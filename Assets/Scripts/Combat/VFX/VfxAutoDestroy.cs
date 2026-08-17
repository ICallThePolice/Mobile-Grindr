using UnityEngine;

namespace SpellSystem.VFX
{
    public class VfxAutoDestroy : MonoBehaviour
    {
        [Tooltip("Через сколько секунд префаб удалится со сцены")]
        [SerializeField] private float lifetime = 2f;

        private void Start()
        {
            // Уничтожаем объект через заданное время, чтобы освободить оперативную память
            Destroy(gameObject, lifetime);
        }
    }
}