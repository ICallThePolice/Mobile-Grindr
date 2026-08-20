using System.Collections;
using UnityEngine;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(CharacterController), typeof(MobileInputReader), typeof(PlayerMovement))]
    public class PlayerClimbing : MonoBehaviour
    {
        [Header("Ledge Detection (Поиск препятствий)")]
        [SerializeField] private float wallCheckDistance = 1.5f;
        [Tooltip("Высота нижнего луча. Должна быть ЧУТЬ ВЫШЕ твоего Step Offset (например, 0.35)")]
        [SerializeField] private float rayHeightFromFeet = 0.35f;

        [Header("Vault & Mantle (Низкие ящики)")]
        [SerializeField] private float obstacleMinHeight = 0.4f;
        [SerializeField] private float vaultMaxHeight = 1.3f;
        [SerializeField] private float vaultOverDistance = 1.5f;

        [SerializeField] private float mantleDuration = 0.7f;
        [SerializeField] private float mantleStandUpDuration = 0.2f;

        [SerializeField] private float vaultDuration = 0.6f;
        [SerializeField] private float vaultRecoveryDuration = 0.3f;
        [SerializeField] private float vaultArcHeight = 0.3f;

        [Header("Hanging & Climbing (Высокие стены)")]
        [SerializeField] private float climbMaxHeight = 3.0f;
        [SerializeField] private float climbAnimationDuration = 1.5f;
        [Range(0f, 0.95f)][SerializeField] private float moveDelayPercent = 0.8f;
        [SerializeField] private float climbStandUpDuration = 0.3f;
        [SerializeField] private float feetToLedgeDistance = 1.0f;
        [SerializeField] private float hangWallOffset = 0.4f;
        [SerializeField] private float topStandInwardStep = 0.6f;

        [Header("Cooldowns (Защита от багов)")]
        [SerializeField] private float parkourCooldown = 0.5f;

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private Transform characterModel;

        private CharacterController controller;
        private MobileInputReader inputReader;
        private PlayerMovement playerMovement;
        private Camera mainCamera;

        public bool IsHanging { get; private set; } = false;
        public bool IsClimbingUp { get; private set; } = false;
        public bool IsMantling { get; private set; } = false;
        public bool IsVaulting { get; private set; } = false;
        public bool IsStandingUp { get; private set; } = false;

        public bool IsParkourActive => IsHanging || IsClimbingUp || IsMantling || IsVaulting || IsStandingUp;

        private Vector3 climbTargetPosition;
        private Vector3 wallNormal;
        private float grabCooldown = 0f;
        private float hangTimer = 0f;

        private bool wasParkourActiveLastFrame;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            inputReader = GetComponent<MobileInputReader>();
            playerMovement = GetComponent<PlayerMovement>();
            mainCamera = Camera.main;
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        private void Update()
        {
            // 1. Таймер блокировки тикает всегда
            if (grabCooldown > 0f) grabCooldown -= Time.deltaTime;

            // 2. Если идут АКТИВНЫЕ анимации перемещения на крышу - блокируем всё остальное
            if (IsClimbingUp || IsMantling || IsVaulting || IsStandingUp)
            {
                wasParkourActiveLastFrame = true; // Запоминаем, что мы двигались
                return;
            }

            // 3. === ИСПРАВЛЕНИЕ: ВИСЕНИЕ НА РУКАХ ===
            // Обрабатываем висение. Здесь мы ждем твою команду, кулдаун сюда не вмешивается!
            if (IsHanging)
            {
                HandleHangingState();
                return;
            }

            // 4. === УМНЫЙ ТАЙМЕР ЗАВЕРШЕНИЯ ===
            // Ловим самый первый кадр, когда активная анимация паркура закончилась, и мы встали на ноги
            if (wasParkourActiveLastFrame)
            {
                wasParkourActiveLastFrame = false;

                // Включаем защиту от цепной реакции на время Parkour Cooldown (0.5 сек из Инспектора)
                grabCooldown = parkourCooldown;
            }

            // Защита от паркура при скольжении вниз по холму
            if (playerMovement.IsSliding)
            {
                grabCooldown = parkourCooldown;
            }

            bool isMoving = inputReader.MoveInput.sqrMagnitude > 0.1f;

            // 5. Запуск нового паркура возможен ТОЛЬКО если таймер на нуле
            if (grabCooldown <= 0f && (!playerMovement.IsGrounded || isMoving))
            {
                TryGrabLedge();
            }
        }

        private void TryGrabLedge()
        {
            float distanceToFeet = (controller.height / 2f) - controller.center.y;
            Vector3 feetPos = transform.position - (Vector3.up * distanceToFeet);
            Vector3 checkDir = characterModel.forward;

            if (inputReader.MoveInput.sqrMagnitude > 0.05f && mainCamera != null)
            {
                Vector3 camFwd = mainCamera.transform.forward; camFwd.y = 0; camFwd.Normalize();
                Vector3 camRt = mainCamera.transform.right; camRt.y = 0; camRt.Normalize();
                checkDir = (camRt * inputReader.MoveInput.x + camFwd * inputReader.MoveInput.y).normalized;
            }

            Vector3 rightOffset = characterModel.right * (controller.radius * 0.8f);

            Vector3 topRayStart = feetPos + Vector3.up * climbMaxHeight;
            Vector3 midRayStart = feetPos + Vector3.up * (vaultMaxHeight + 0.2f);
            Vector3 botRayStart = feetPos + Vector3.up * rayHeightFromFeet;

            bool midHit = CheckWallLayer(midRayStart, checkDir, rightOffset, wallCheckDistance, Color.yellow, out RaycastHit mHit);
            bool botHit = CheckWallLayer(botRayStart, checkDir, rightOffset, wallCheckDistance, Color.green, out RaycastHit bHit);

            // === ЖЕЛАНИЕ КАРАБКАТЬСЯ ===
            // Проверяем приоритет: если средний луч нашел стену - карабкаемся. 
            // Если нет, но сработал нижний - перепрыгиваем.
            if (midHit)
            {
                FindRoofAndExecute(mHit, feetPos, distanceToFeet, isClimbOnly: true);
                return;
            }

            if (botHit)
            {
                FindRoofAndExecute(bHit, feetPos, distanceToFeet, isClimbOnly: false);
                return;
            }
        }

        private bool CheckWallLayer(Vector3 start, Vector3 dir, Vector3 rightOffset, float dist, Color debugColor, out RaycastHit bestHit)
        {
            bestHit = new RaycastHit();
            bool hit = false;
            Vector3[] origins = { start, start + rightOffset, start - rightOffset };

            foreach (var origin in origins)
            {
                Debug.DrawRay(origin, dir * dist, debugColor);
                if (Physics.Raycast(origin, dir, out RaycastHit h, dist, playerMovement.obstacleMask))
                {
                    float surfaceAngle = Vector3.Angle(Vector3.up, h.normal);

                    // === ИСПРАВЛЕНИЕ УГЛА ===
                    // Холмы имеют наклон 20-45 градусов. Стена должна быть круче 50 градусов!
                    if (surfaceAngle > 50f)
                    {
                        hit = true;
                        bestHit = h;
                        break;
                    }
                }
            }
            return hit;
        }

        // Универсальный сброс всех состояний паркура
        public void ResetAllParkourStates()
        {
            IsHanging = false;
            IsClimbingUp = false;
            IsMantling = false;
            IsVaulting = false;
            IsStandingUp = false;

            if (controller != null)
            {
                controller.enabled = true;
            }

            // Врубаем защиту от цепной реакции
            grabCooldown = parkourCooldown;
        }

        private void FindRoofAndExecute(RaycastHit wallHit, Vector3 feetPos, float distanceToFeet, bool isClimbOnly)
        {
            Vector3 inward = -wallHit.normal;
            inward.y = 0;
            if (inward == Vector3.zero) inward = characterModel.forward;
            inward.Normalize();

            float startHeight = isClimbOnly ? climbMaxHeight : vaultMaxHeight;
            Vector3 roofRayStart = wallHit.point + (inward * 0.4f);
            roofRayStart.y = feetPos.y + startHeight + 0.5f;

            if (Physics.SphereCast(roofRayStart, 0.15f, Vector3.down, out RaycastHit roofHit, startHeight + 1.0f, playerMovement.obstacleMask))
            {
                float roofAngle = Vector3.Angle(Vector3.up, roofHit.normal);
                if (roofAngle > 15f) return;

                Vector3 perfectCorner = new Vector3(wallHit.point.x, roofHit.point.y, wallHit.point.z);

                // === ИСПРАВЛЕНИЕ "ФАНТОМНОГО ВИСЕНИЯ" ===
                bool isGrounded = playerMovement.IsGrounded;

                // ПРЕДОХРАНИТЕЛЬ 1: Защита от микро-прыжков. 
                // Если контроллер говорит, что мы в воздухе, но пол прямо под ногами - мы НА ЗЕМЛЕ!
                if (!isGrounded)
                {
                    if (Physics.Raycast(feetPos + Vector3.up * 0.1f, Vector3.down, 0.4f, playerMovement.obstacleMask))
                    {
                        isGrounded = true;
                    }
                }

                float relativeHeightToPlayer = roofHit.point.y - feetPos.y;

                if (relativeHeightToPlayer <= controller.stepOffset + 0.1f) return;
                if (relativeHeightToPlayer < obstacleMinHeight) return;

                Vector3 endPos = perfectCorner + (inward * topStandInwardStep);
                Vector3 exactFloorCheck = new Vector3(endPos.x, roofHit.point.y + 1f, endPos.z);
                if (Physics.Raycast(exactFloorCheck, Vector3.down, out RaycastHit exactHit, 2f, playerMovement.obstacleMask))
                {
                    endPos.y = exactHit.point.y + distanceToFeet + 0.15f;
                }
                else
                {
                    endPos.y = roofHit.point.y + distanceToFeet + 0.15f;
                }

                if (!IsPositionSafe(endPos, inward)) endPos += inward * 0.2f;
                if (!IsPositionSafe(endPos, inward)) return;

                if (isClimbOnly)
                {
                    if (relativeHeightToPlayer <= climbMaxHeight)
                    {
                        // ПРЕДОХРАНИТЕЛЬ 2: Защита от висения на земле.
                        // Если мы стоим на ногах, а препятствие ниже 1.5 метров - мы НЕ виснем на нём!
                        if (isGrounded && relativeHeightToPlayer < 1.5f) return;

                        Vector3 hangPos = perfectCorner - (inward * hangWallOffset) - (Vector3.up * feetToLedgeDistance);
                        wallNormal = -inward;
                        climbTargetPosition = endPos;
                        StartCoroutine(JumpToWallRoutine(hangPos, inward));
                    }
                }
                else
                {
                    if (isGrounded && relativeHeightToPlayer <= vaultMaxHeight)
                    {
                        bool isRunning = inputReader.MoveInput.magnitude >= 0.65f;
                        Vector3 onTopPos = endPos;

                        if (isRunning)
                        {
                            Vector3 farSideCheck = perfectCorner + (inward * vaultOverDistance) + (Vector3.up * 2.0f);
                            if (Physics.Raycast(farSideCheck, Vector3.down, out RaycastHit groundHit, 4.0f, playerMovement.obstacleMask))
                            {
                                float landingAngle = Vector3.Angle(Vector3.up, groundHit.normal);
                                if (landingAngle <= controller.slopeLimit && groundHit.point.y <= feetPos.y + 0.5f)
                                {
                                    Vector3 vaultEndPos = groundHit.point;
                                    vaultEndPos.y += distanceToFeet;

                                    if (IsPositionSafe(vaultEndPos, inward))
                                    {
                                        wallNormal = -inward;
                                        StartCoroutine(VaultRoutine(vaultEndPos, roofHit.point.y + distanceToFeet));
                                        return;
                                    }
                                }
                            }
                        }

                        wallNormal = -inward;
                        StartCoroutine(MantleRoutine(onTopPos));
                    }
                    else if (!isGrounded && relativeHeightToPlayer <= climbMaxHeight)
                    {
                        Vector3 hangPos = perfectCorner - (inward * hangWallOffset) - (Vector3.up * feetToLedgeDistance);
                        wallNormal = -inward;
                        climbTargetPosition = endPos;
                        StartCoroutine(JumpToWallRoutine(hangPos, inward));
                    }
                }
            }
        }

        // === ИСПРАВЛЕННАЯ ПРОВЕРКА БЕЗОПАСНОСТИ ===
        // Добавили второй параметр: Vector3 inward
        private bool IsPositionSafe(Vector3 targetPos, Vector3 inward)
        {
            Vector3 capsuleCenter = targetPos + controller.center;
            float halfHeight = controller.height / 2f;
            Vector3 point1 = capsuleCenter + Vector3.up * (halfHeight - controller.radius);
            Vector3 point2 = capsuleCenter - Vector3.up * (halfHeight - controller.radius);

            bool isBlocked = Physics.CheckCapsule(point1, point2, controller.radius * 0.95f, playerMovement.obstacleMask);
            if (isBlocked) return false;

            // === ИСПРАВЛЕНИЕ ЛОКАЛЬНЫХ КООРДИНАТ ===
            // Вычисляем право и лево относительно стены
            Vector3 right = Vector3.Cross(Vector3.up, inward).normalized;
            Vector3 left = -right;
            Vector3[] directions = { inward, -inward, right, left };

            foreach (var dir in directions)
            {
                Vector3 rayStart = targetPos + Vector3.up * 0.5f + dir * (controller.radius * 0.8f);
                if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 1.5f, playerMovement.obstacleMask))
                {
                    float slopeAngle = Vector3.Angle(Vector3.up, hit.normal);
                    if (slopeAngle > 65f && hit.point.y > targetPos.y + 0.1f)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private IEnumerator JumpToWallRoutine(Vector3 hangPos, Vector3 lookDir)
        {
            IsHanging = true;
            playerMovement.VerticalVelocity = 0f;
            controller.enabled = false;

            lookDir.y = 0;
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            Vector3 startPos = transform.position;
            Quaternion startRot = characterModel.rotation;

            if (animator != null) animator.SetBool("IsHanging", true);

            float jumpDuration = 0.25f;
            float elapsed = 0f;

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / jumpDuration;
                transform.position = Vector3.Lerp(startPos, hangPos, t);
                characterModel.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }

            transform.position = hangPos;
            characterModel.rotation = targetRot;
            controller.enabled = true;
            hangTimer = 0f;
        }

        private void HandleHangingState()
        {
            playerMovement.VerticalVelocity = 0f;
            hangTimer += Time.deltaTime;

            if (hangTimer > 0.3f && inputReader.MoveInput.sqrMagnitude > 0.1f && mainCamera != null)
            {
                Vector3 camFwd = mainCamera.transform.forward; camFwd.y = 0; camFwd.Normalize();
                Vector3 camRt = mainCamera.transform.right; camRt.y = 0; camRt.Normalize();
                Vector3 moveDir = (camRt * inputReader.MoveInput.x + camFwd * inputReader.MoveInput.y).normalized;

                float dot = Vector3.Dot(moveDir, wallNormal);
                if (dot < -0.2f)
                {
                    // === ВОТ ЗДЕСЬ НУЖНО ДОБАВИТЬ СБРОС ===
                    IsHanging = false;
                    StartCoroutine(ClimbUpRoutine());
                }
                else if (dot > 0.2f)
                {
                    DropFromLedge();
                }
            }
        }

        public void DropFromLedge()
        {
            ResetAllParkourStates();
        }

        private IEnumerator ClimbUpRoutine()
        {
            IsClimbingUp = true;
            if (animator != null) animator.SetTrigger("ClimbUp");

            controller.enabled = false;
            Vector3 startPos = transform.position;

            float waitTime = climbAnimationDuration * moveDelayPercent;
            float moveTime = climbAnimationDuration * (1f - moveDelayPercent);

            if (waitTime > 0f) yield return new WaitForSeconds(waitTime);

            if (moveTime > 0f)
            {
                float elapsed = 0f;
                while (elapsed < moveTime)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(startPos, climbTargetPosition, elapsed / moveTime);
                    yield return null;
                }
            }

            // === УБИЛИ КОЗОЧКУ ===
            // Никаких +0.05f. Встаем точно на вычисленную точку!
            transform.position = climbTargetPosition;

            IsClimbingUp = false;
            IsStandingUp = true;
            if (animator != null) animator.SetBool("IsHanging", false);

            if (climbStandUpDuration > 0f) yield return new WaitForSeconds(climbStandUpDuration);

            // Очищаем состояния и отдаем контроль игроку
            ResetAllParkourStates();
        }

        private IEnumerator MantleRoutine(Vector3 targetPos)
        {
            IsMantling = true;
            if (animator != null) animator.SetTrigger("Mantle");

            controller.enabled = false;
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            characterModel.rotation = Quaternion.LookRotation(-wallNormal);

            while (elapsed < mantleDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / mantleDuration;
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * 0.2f;
                transform.position = currentPos;
                yield return null;
            }

            transform.position = targetPos; // Убили козочку

            IsMantling = false;
            IsStandingUp = true;

            if (mantleStandUpDuration > 0f) yield return new WaitForSeconds(mantleStandUpDuration);

            ResetAllParkourStates();
        }

        private IEnumerator VaultRoutine(Vector3 endPos, float obstacleTopY)
        {
            IsVaulting = true;
            if (animator != null) animator.SetTrigger("Vault");

            controller.enabled = false;
            Vector3 startPos = transform.position;
            float elapsed = 0f;

            characterModel.rotation = Quaternion.LookRotation(-wallNormal);

            while (elapsed < vaultDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / vaultDuration;

                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t);
                float arc = Mathf.Sin(t * Mathf.PI) * vaultArcHeight;
                float baseY = Mathf.Lerp(startPos.y, endPos.y, t);

                currentPos.y = Mathf.Max(baseY + arc, obstacleTopY + Mathf.Sin(t * Mathf.PI) * 0.1f);

                transform.position = currentPos;
                yield return null;
            }

            transform.position = endPos;

            IsVaulting = false;
            IsStandingUp = true;

            if (vaultRecoveryDuration > 0f) yield return new WaitForSeconds(vaultRecoveryDuration);

            ResetAllParkourStates();
        }
    }
}