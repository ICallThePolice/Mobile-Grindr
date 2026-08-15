using UnityEngine;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5.0f;
        [SerializeField] private float rotationSpeed = 15.0f;
        [SerializeField] private float gravity = -9.81f;

        [Header("Animation")]
        [SerializeField] private Animator animator; // Ссылка на компонент Animator на вашей модели

        private CharacterController controller;
        private Vector3 velocity;
        private Camera mainCamera;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            mainCamera = Camera.main;

            // Если аниматор не назначен в инспекторе вручную, ищем его внутри дочерних объектов
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            HandleMovement();
        }

        private void HandleMovement()
        {
            // Получаем ввод с клавиатуры (WASD)
            float moveX = Input.GetAxis("Horizontal"); // A/D
            float moveZ = Input.GetAxis("Vertical");   // W/S

            Vector3 inputDir = new Vector3(moveX, 0f, moveZ).normalized;
            float inputMagnitude = inputDir.magnitude; // От 0 до 1 в зависимости от силы нажатия

            if (inputMagnitude >= 0.1f)
            {
                // Поворот персонажа по направлению камеры
                float targetAngle = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCamera.transform.eulerAngles.y;
                Quaternion rotation = Quaternion.Euler(0f, targetAngle, 0f);

                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationSpeed * Time.deltaTime);

                Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;
                controller.Move(moveDir.normalized * moveSpeed * Time.deltaTime);
            }

            // Передаем текущую скорость движения в Animator. 
            // Если игрок жмет WASD, magnitude > 0.1, включится анимация ходьбы. Если отпустил — вернется в Idle.
            if (animator != null)
            {
                animator.SetFloat("Speed", inputMagnitude);
            }

            // Гравитация
            if (controller.isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }
}