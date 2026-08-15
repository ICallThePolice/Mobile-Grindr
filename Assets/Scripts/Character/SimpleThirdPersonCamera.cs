using UnityEngine;

namespace SpellSystem.Core
{
    public class SimpleThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;

        [Header("Camera Settings (Isometric ARPG Style)")]
        [SerializeField] private Vector3 baseOffset = new Vector3(0f, 4f, -8f);
        [SerializeField] private float followSpeed = 15f;

        [Header("Manual Control (Free Look)")]
        [SerializeField] private float horizontalSpeed = 150f;
        [SerializeField] private float verticalSpeed = 100f;
        [SerializeField] private float minPitch = 10f;
        [SerializeField] private float maxPitch = 60f;

        [Header("Yaw Limits (Camera Leash & Buffer)")]
        [SerializeField] private Transform characterModel;
        [SerializeField] private float maxYawAngle = 55f;

        [Tooltip("Стартовое ускорение (насколько медленным и тягучим будет начало возврата)")]
        [SerializeField] private float leashRecoveryAcceleration = 150f;
        [Tooltip("Максимальная скорость, которую камера наберет к концу сброса")]
        [SerializeField] private float maxLeashRecoverySpeed = 400f;

        [Header("Auto-Reset Settings")]
        [SerializeField] private float defaultPitch = 40f;
        [SerializeField] private float pitchReturnSpeed = 3f;

        [Header("Lock-On Control & Focus")]
        [SerializeField] private float lockOnRotationSpeed = 8f;
        [Range(0f, 1f)][SerializeField] private float focusBias = 0.3f;
        [SerializeField] private float lookAtSmoothing = 10f;

        [SerializeField] private float playerFocusHeight = 1.0f;
        [SerializeField] private float enemyFocusHeight = 1.0f;

        private Transform currentTarget;
        public Transform CurrentTarget => currentTarget;

        private bool isHardLocked = false;
        private float currentYaw = 0f;
        private float currentPitch = 40f;
        private Vector2 lookInput;
        private Vector3 currentLookAtPoint;

        // Переменная для отслеживания текущей скорости разгона камеры
        private float currentRecoverySpeed = 0f;

        private void Start()
        {
            if (player != null)
            {
                currentYaw = player.eulerAngles.y;
                currentPitch = defaultPitch;
                currentLookAtPoint = player.position + Vector3.up * playerFocusHeight;

                if (characterModel == null)
                {
                    Animator anim = player.GetComponentInChildren<Animator>();
                    if (anim != null) characterModel = anim.transform;
                }
            }
        }

        public void SetLookInput(Vector2 input)
        {
            lookInput = input;
        }

        public void SetTarget(Transform newTarget, bool hardLock)
        {
            currentTarget = newTarget;
            isHardLocked = hardLock;
        }

        private void LateUpdate()
        {
            if (player == null) return;

            // --- 1. ЛОГИКА ОРБИТЫ ПО ГОРИЗОНТАЛИ ---
            if (currentTarget != null)
            {
                Vector3 dirToTarget = currentTarget.position - player.position;
                dirToTarget.y = 0;

                if (dirToTarget != Vector3.zero)
                {
                    float targetYaw = Quaternion.LookRotation(dirToTarget).eulerAngles.y;

                    if (isHardLocked)
                    {
                        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, lockOnRotationSpeed * Time.deltaTime);
                    }
                    else
                    {
                        if (Mathf.Abs(lookInput.x) > 0.05f || Mathf.Abs(lookInput.y) > 0.05f)
                            currentYaw += lookInput.x * horizontalSpeed * Time.deltaTime;
                        else
                            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, (lockOnRotationSpeed * 0.4f) * Time.deltaTime);
                    }
                }
            }
            else
            {
                // Свободная камера
                currentYaw += lookInput.x * horizontalSpeed * Time.deltaTime;
            }

            // --- 2. УНИВЕРСАЛЬНЫЙ ЛИМИТ (С РАЗГОНОМ EASE-IN) ---
            if (characterModel != null)
            {
                float charYaw = characterModel.eulerAngles.y;
                float angleDiff = Mathf.DeltaAngle(charYaw, currentYaw);

                if (angleDiff > maxYawAngle || angleDiff < -maxYawAngle)
                {
                    float limitYaw = angleDiff > maxYawAngle ? charYaw + maxYawAngle : charYaw - maxYawAngle;

                    if (Mathf.Abs(lookInput.x) > 0.05f)
                    {
                        // Если игрок активно крутит стик, сбрасываем разгон, чтобы камера не "вырывалась" из рук
                        currentRecoverySpeed = 0f;
                        currentYaw = Mathf.MoveTowardsAngle(currentYaw, limitYaw, 10f * Time.deltaTime);
                    }
                    else
                    {
                        // Медленный старт -> Быстрый финиш
                        currentRecoverySpeed += leashRecoveryAcceleration * Time.deltaTime;
                        currentRecoverySpeed = Mathf.Min(currentRecoverySpeed, maxLeashRecoverySpeed);

                        // Используем MoveTowardsAngle вместо Lerp для применения нашей скорости
                        currentYaw = Mathf.MoveTowardsAngle(currentYaw, limitYaw, currentRecoverySpeed * Time.deltaTime);
                    }
                }
                else
                {
                    // Как только камера вернулась в разрешенную зону — полностью сбрасываем её разгон
                    currentRecoverySpeed = 0f;
                }
            }

            // --- 3. ЛОГИКА ОРБИТЫ ПО ВЕРТИКАЛИ ---
            if (Mathf.Abs(lookInput.y) > 0.05f)
                currentPitch -= lookInput.y * verticalSpeed * Time.deltaTime;
            else
                currentPitch = Mathf.Lerp(currentPitch, defaultPitch, pitchReturnSpeed * Time.deltaTime);

            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

            // --- 4. ПРИМЕНЕНИЕ ПОЗИЦИИ ---
            Quaternion cameraRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 targetPosition = player.position + cameraRotation * baseOffset;
            transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);

            // --- 5. ЛОГИКА ФОКУСА С ВЫСОТАМИ ---
            Vector3 pFocus = player.position + Vector3.up * playerFocusHeight;
            Vector3 eFocus = currentTarget != null ? currentTarget.position + Vector3.up * enemyFocusHeight : pFocus;
            Vector3 targetLookAtPoint = (currentTarget != null) ? Vector3.Lerp(pFocus, eFocus, focusBias) : pFocus;

            currentLookAtPoint = Vector3.Lerp(currentLookAtPoint, targetLookAtPoint, lookAtSmoothing * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(currentLookAtPoint - transform.position);
        }
    }
}