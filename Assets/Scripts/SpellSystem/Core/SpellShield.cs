using UnityEngine;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    public class SpellShield : MonoBehaviour
    {
        [Header("Rotation & Shield Settings")]
        [SerializeField] private float rotationSpeed = 120f;
        [SerializeField] private float duration = 8f;

        private Transform casterTransform;
        private EnergyDataSO energyData;

        public void Initialize(Transform caster, EnergyDataSO energy)
        {
            casterTransform = caster;
            energyData = energy;

            transform.position = caster.position;
            transform.SetParent(caster);

            Destroy(gameObject, duration);
        }

        private void Update()
        {
            // Вращение щитов вокруг персонажа
            if (casterTransform != null)
            {
                transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            }
        }
    }
}