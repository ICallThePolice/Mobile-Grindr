using UnityEngine;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(CharacterController), typeof(MobileInputReader))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float moveSpeed = 5f;
        [SerializeField] private float turnSpeed = 15f;

        [Header("Physics & Jumping")]
        public float gravity = -30f;
        public float jumpHeight = 2f;

        [Header("Targeting & Combat")]
        public Transform currentTarget;
        [SerializeField] private float runAwayAngle = 120f;
        [SerializeField] private float maxBackwardsAngle = 135f;

        [Header("References")]
        [SerializeField] private SimpleThirdPersonCamera cameraScript;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform characterModel;

        private CharacterController controller;
        private MobileInputReader inputReader;
        private Camera mainCamera;

        public bool CanMove { get; set; } = true;
        public float OriginalMoveSpeed { get; private set; }

        // Переменные гравитации
        private float verticalVelocity;
        private bool isGrounded;
        private bool jumpRequested = false;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            inputReader = GetComponent<MobileInputReader>();
            mainCamera = Camera.main;
            OriginalMoveSpeed = moveSpeed;

            if (cameraScript == null) cameraScript = FindAnyObjectByType<SimpleThirdPersonCamera>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (characterModel == null && animator != null) characterModel = animator.transform;
        }

        // --- ЭТОТ МЕТОД НУЖНО ВЫЗЫВАТЬ ИЗ UI КНОПКИ ПРЫЖКА ---
        public void RequestJump()
        {
            if (isGrounded) jumpRequested = true;
        }

        private void Update()
        {
            if (inputReader.JumpInput) RequestJump();

            if (cameraScript != null)
            {
                cameraScript.SetLookInput(inputReader.LookInput);
            }

            if (CanMove)
            {
                Move(inputReader.MoveInput);
            }
            else
            {
                // Если движение заблокировано (например рывок), всё равно применяем гравитацию
                ApplyGravityOnly();
            }
        }

        private Vector3 GetClampedLookDirection(Vector3 intendedDir, Vector3 camFwd, Vector3 camRt)
        {
            float angleFromCamera = Vector3.Angle(camFwd, intendedDir);
            if (angleFromCamera > maxBackwardsAngle)
            {
                float dotRight = Vector3.Dot(camRt, intendedDir);
                if (Mathf.Abs(dotRight) < 0.01f && characterModel != null)
                {
                    dotRight = Vector3.Dot(camRt, characterModel.forward);
                }
                float sign = dotRight >= 0 ? 1f : -1f;
                return Quaternion.AngleAxis(maxBackwardsAngle * sign, Vector3.up) * camFwd;
            }
            return intendedDir;
        }

        private void ApplyGravityOnly()
        {
            isGrounded = controller.isGrounded;
            if (isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
            verticalVelocity += gravity * Time.deltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.deltaTime);
        }

        private void Move(Vector2 input)
        {
            if (mainCamera == null) return;

            // 1. Проверяем землю
            isGrounded = controller.isGrounded;
            if (isGrounded && verticalVelocity < 0)
            {
                verticalVelocity = -2f; // Прилипаем к земле
            }

            // 2. Обрабатываем запрос на прыжок
            if (jumpRequested && isGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpRequested = false;
            }

            // 3. Применяем гравитацию
            verticalVelocity += gravity * Time.deltaTime;

            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0; camRight.y = 0;
            camForward.Normalize(); camRight.Normalize();

            // 4. Формируем финальный вектор (Ходьба + Гравитация/Прыжок)
            Vector3 moveDir = camRight * input.x + camForward * input.y;
            Vector3 finalMove = (moveDir * moveSpeed) + (Vector3.up * verticalVelocity);

            controller.Move(finalMove * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat("Speed", input.magnitude);
            }

            // 5. Вращение модели
            if (moveDir.sqrMagnitude > 0.01f && characterModel != null)
            {
                Vector3 lookDir = moveDir;

                if (currentTarget != null)
                {
                    Vector3 dirToTarget = (currentTarget.position - transform.position).normalized;
                    dirToTarget.y = 0;
                    if (Vector3.Angle(dirToTarget, moveDir) < runAwayAngle) lookDir = dirToTarget;
                }
                else
                {
                    lookDir = GetClampedLookDirection(moveDir, camForward, camRight);
                }

                Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                characterModel.rotation = Quaternion.Slerp(characterModel.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }

            // 6. Проверка "Дна мира"
            if (transform.position.y <= 4.5f)
            {
                Debug.Log("Мы на краю вселенной! Получаем урон от Бездны!");
            }
        }
    }
}