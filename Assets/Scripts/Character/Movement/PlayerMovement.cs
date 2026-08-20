using UnityEngine;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(CharacterController), typeof(MobileInputReader))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Settings")]
        public float walkSpeed = 3f;
        public float runSpeed = 7f;
        [SerializeField] private float runThreshold = 0.65f;
        [SerializeField] private float turnSpeed = 15f;
        public float speedMultiplier = 1f;

        [Header("Physics & Jumping")]
        public float gravity = -30f;
        public float jumpHeight = 2f;
        public LayerMask obstacleMask = ~0;

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
        private PlayerClimbing climbingScript;

        public bool CanMove { get; set; } = true;

        public float VerticalVelocity { get; set; }
        public bool IsGrounded { get; private set; }
        public bool IsSliding => slopeSlideVelocity != Vector3.zero;

        private bool jumpRequested = false;
        private Vector3 slopeSlideVelocity;
        private Vector3 hitNormal;

        // Переменная для отслеживания столкновений со стенами
        private CollisionFlags collisionFlags;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            inputReader = GetComponent<MobileInputReader>();
            climbingScript = GetComponent<PlayerClimbing>();
            mainCamera = Camera.main;

            if (cameraScript == null) cameraScript = FindAnyObjectByType<SimpleThirdPersonCamera>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (characterModel == null && animator != null) characterModel = animator.transform;
        }

        public void RequestJump()
        {
            // Базовая проверка от Unity
            bool canJump = IsGrounded;

            // === ИСТРЕБИТЕЛЬ КОЗОЧЕК (Строгая проверка пола) ===
            // Unity часто врет, что мы на земле, когда мы тремся о стену. Перепроверяем!
            if (canJump)
            {
                Vector3 rayStart = transform.position + controller.center;
                // Длина луча: половина капсулы + небольшой запас вниз
                float checkDist = (controller.height / 2f) + 0.25f;

                // Стреляем лучом строго вниз
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, checkDist, obstacleMask))
                {
                    // Если то, на чем мы стоим, круче нашего лимита - это стена. Прыгать нельзя!
                    if (Vector3.Angle(Vector3.up, hit.normal) > controller.slopeLimit)
                    {
                        canJump = false;
                    }
                }
                else
                {
                    // Если под ногами вообще нет земли (мы висим в воздухе у стены) - прыгать нельзя!
                    canJump = false;
                }
            }

            // Прыгаем только если мы реально на земле и не соскальзываем
            if (canJump && slopeSlideVelocity == Vector3.zero)
            {
                jumpRequested = true;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {

        }

        private void Update()
        {
            if (cameraScript != null)
            {
                cameraScript.SetLookInput(inputReader.LookInput);
            }

            if (controller == null || !controller.enabled)
            {
                // === ОЧИСТКА ПАМЯТИ 1 ===
                // Пока мы висим в воздухе или в анимации, сбрасываем старую нормаль стены!
                hitNormal = Vector3.up;
                slopeSlideVelocity = Vector3.zero;
                return;
            }

            if (climbingScript != null && climbingScript.IsParkourActive)
            {
                // === ОЧИСТКА ПАМЯТИ 2 ===
                hitNormal = Vector3.up;
                slopeSlideVelocity = Vector3.zero;
                if (animator != null) animator.SetFloat("Speed", 0f);
                return;
            }

            if (inputReader.JumpInput)
            {
                if (climbingScript != null && climbingScript.IsHanging)
                {
                    climbingScript.DropFromLedge();
                }
                else
                {
                    RequestJump();
                }
            }

            if (CanMove)
            {
                Move(inputReader.MoveInput);
            }
            else
            {
                ApplyGravityOnly();
            }

            HandleAirborneWallSlide();

        }

        private void ApplyGravityOnly()
        {
            if (!controller.enabled) return;

            IsGrounded = controller.isGrounded;
            // Возвращаем стандартную гравитацию покоя
            if (IsGrounded && VerticalVelocity < 0)
            {
                VerticalVelocity = -2f;
            }

            VerticalVelocity += gravity * Time.deltaTime;
            collisionFlags = controller.Move(Vector3.up * VerticalVelocity * Time.deltaTime);

            AntiWedgeLogic();
        }

        private void HandleAirborneWallSlide()
        {
            // Работает ТОЛЬКО если мы в воздухе и не заняты паркуром
            if (controller == null || IsGrounded) return;
            if (climbingScript != null && climbingScript.IsParkourActive) return;

            // Вычисляем, куда направлен персонаж (или куда мы давим стик)
            Vector3 moveDir = characterModel.forward;
            if (inputReader.MoveInput.sqrMagnitude > 0.1f && mainCamera != null)
            {
                Vector3 camFwd = mainCamera.transform.forward; camFwd.y = 0; camFwd.Normalize();
                Vector3 camRt = mainCamera.transform.right; camRt.y = 0; camRt.Normalize();
                moveDir = (camRt * inputReader.MoveInput.x + camFwd * inputReader.MoveInput.y).normalized;
            }

            Vector3 capsuleCenter = transform.position + controller.center;

            // Забрасываем толстую сферу прямо перед персонажем
            // Радиус чуть больше капсулы, чтобы поймать стену за миллисекунду до удара
            if (Physics.SphereCast(capsuleCenter, controller.radius + 0.1f, moveDir, out RaycastHit hit, 0.2f, obstacleMask))
            {
                float wallAngle = Vector3.Angle(Vector3.up, hit.normal);

                // Если стена круче нашего Slope Limit (не холм, а именно скала)
                if (wallAngle > controller.slopeLimit)
                {
                    // 1. Имитация удара: быстро гасим инерцию прыжка вверх
                    if (VerticalVelocity > 0)
                    {
                        VerticalVelocity -= 20f * Time.deltaTime;
                        if (VerticalVelocity < 0) VerticalVelocity = 0;
                    }

                    // 2. Гладкое скольжение: тянем вниз (5 м/с) и отталкиваем от стены по нормали (1.5 м/с).
                    // Отталкивание критически важно - оно не дает капсуле врезаться в острые стыки вокселей!
                    Vector3 slideDir = (Vector3.down * 5f) + (hit.normal * 1.5f);
                    controller.Move(slideDir * Time.deltaTime);
                }
            }
        }

        private void CheckSlope()
        {
            slopeSlideVelocity = Vector3.zero;

            if (IsGrounded)
            {
                // === ПУЛЕНЕПРОБИВАЕМЫЙ СКАНЕР ПОЛА ===
                // Пускаем луч строго из центра персонажа вниз. 
                // Это гарантирует, что мы измеряем только ту поверхность, на которой реально стоим!
                Vector3 rayStart = transform.position + controller.center;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, (controller.height / 2f) + 0.5f, obstacleMask))
                {
                    float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);

                    if (slopeAngle > controller.slopeLimit)
                    {
                        Vector3 slideDir = new Vector3(hit.normal.x, -hit.normal.y, hit.normal.z);
                        Vector3 floorNormal = hit.normal;

                        Vector3.OrthoNormalize(ref floorNormal, ref slideDir);
                        slopeSlideVelocity = slideDir * runSpeed * 1.5f;
                    }
                }
            }
        }

        private void Move(Vector2 input)
        {
            if (mainCamera == null || !controller.enabled) return;

            IsGrounded = controller.isGrounded;
            // Возвращаем стандартную гравитацию движения
            if (IsGrounded && VerticalVelocity < 0)
            {
                VerticalVelocity = -2f;
            }

            CheckSlope();

            if (jumpRequested)
            {
                VerticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpRequested = false;
            }

            VerticalVelocity += gravity * Time.deltaTime;

            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0; camRight.y = 0;
            camForward.Normalize(); camRight.Normalize();

            Vector3 moveDir = camRight * input.x + camForward * input.y;

            float currentBaseSpeed = (input.magnitude >= runThreshold) ? runSpeed : walkSpeed;
            float finalSpeed = currentBaseSpeed * speedMultiplier;
            Vector3 finalMove;

            if (slopeSlideVelocity != Vector3.zero)
            {
                finalMove = slopeSlideVelocity + (Vector3.up * VerticalVelocity);
            }
            else
            {
                finalMove = (moveDir * finalSpeed) + (Vector3.up * VerticalVelocity);
            }

            if (controller.enabled)
            {
                collisionFlags = controller.Move(finalMove * Time.deltaTime);
                AntiWedgeLogic();
            }

            if (animator != null)
            {
                float speedAnim = (input.magnitude >= runThreshold) ? 1f : (input.magnitude > 0.05f ? 0.5f : 0f);
                if (slopeSlideVelocity != Vector3.zero) speedAnim = 0f;
                animator.SetFloat("Speed", speedAnim);
            }

            if (moveDir.sqrMagnitude > 0.01f && characterModel != null && slopeSlideVelocity == Vector3.zero)
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
        }

        // Логика предотвращения застревания в V-образных ямах
        private void AntiWedgeLogic()
        {
            bool isTouchingSides = (collisionFlags & CollisionFlags.Sides) != 0;

            // Если мы не на земле, касаемся стен и летим вниз - сбрасываем гравитацию до минимума (-2).
            // Это не даст капсуле вбиться в щель и застрять намертво.
            if (!IsGrounded && isTouchingSides && VerticalVelocity < 0)
            {
                VerticalVelocity = Mathf.Max(VerticalVelocity, -2f);
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

        // === ВСТРОЕННЫЙ ДЕБАГГЕР (Показывает статы на экране) ===
        private void OnGUI()
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 24;
            style.normal.textColor = Color.green; // Зеленый цвет, чтобы было видно на фоне скал
            style.fontStyle = FontStyle.Bold;

            string debugText = "=== АНАЛИЗАТОР ФИЗИКИ ===\n";

            if (controller != null)
            {
                debugText += $"CC Включен (Enabled): {controller.enabled}\n";
                debugText += $"На земле (IsGrounded): {controller.isGrounded}\n";
                debugText += $"Удары о стену (CollFlags): {collisionFlags}\n";
            }

            debugText += $"Гравитация (Vel Y): {VerticalVelocity:F2}\n";
            debugText += $"Сила скольжения (Slide): {slopeSlideVelocity}\n";

            if (climbingScript != null)
            {
                debugText += $"Паркур активен: {climbingScript.IsParkourActive}\n";
            }

            // Имитируем наш сканер пола, чтобы видеть, что находится под ногами
            if (controller != null)
            {
                Vector3 rayStart = transform.position + controller.center;
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, (controller.height / 2f) + 0.5f, obstacleMask))
                {
                    float angle = Vector3.Angle(Vector3.up, hit.normal);
                    debugText += $"Угол пола под ногами: {angle:F1}°\n";

                    if (angle > controller.slopeLimit)
                    {
                        debugText += $"СТАТУС: СКЛОН СЛИШКОМ КРУТОЙ (> {controller.slopeLimit}!)\n";
                    }
                }
                else
                {
                    debugText += $"Угол пола под ногами: ПРОПАСТЬ (Луч не достал)\n";
                }
            }

            // Выводим текст на экран (отступ 20px слева, 100px сверху)
            GUI.Label(new Rect(20, 100, 600, 400), debugText, style);
        }
    }

}