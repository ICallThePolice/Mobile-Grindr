using UnityEngine;

namespace SpellSystem.Core
{
    public class SimpleThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform characterModel;

        [Header("Camera Constraints")]
        [Range(45f, 60f)]
        [SerializeField] private float maxYawAngle = 45f;

        [Header("Camera Smoothness")]
        [Range(0.05f, 1f)][SerializeField] private float yawSmoothCombat = 0.2f;
        [Range(0.05f, 2f)][SerializeField] private float yawSmoothPeace = 0.4f;
        [Range(0.05f, 1f)][SerializeField] private float pitchSmoothTime = 0.15f;
        [Range(0.05f, 1f)][SerializeField] private float zoomSmoothTime = 0.3f;
        [Range(0.05f, 1f)][SerializeField] private float focusSmoothTime = 0.25f;
        [Tooltip("Плавность перелета камеры при смене цели (софт-лок)")]
        [Range(0.05f, 1f)][SerializeField] private float targetSwitchSmoothTime = 0.3f;

        [Header("Combat Zoom Settings")]
        [SerializeField] private float outOfCombatZ = -4f;
        [SerializeField] private float inCombatZ = -5f;

        [Header("Camera Base Settings")]
        [SerializeField] private Vector2 baseXYOffset = new Vector2(0f, 4f);
        [SerializeField] private float followSpeed = 15f;
        [SerializeField] private float manualResetSpeed = 4f;

        [Header("Dynamic Zoom Settings")]
        [SerializeField] private float minZoomDistance = 3.5f;

        [Header("Manual Control")]
        [SerializeField] private float horizontalSpeed = 150f;
        [SerializeField] private float verticalSpeed = 100f;
        [SerializeField] private float minPitch = 15f;
        [SerializeField] private float maxPitch = 50f;

        [Header("Auto-Reset & Dash Settings")]
        [SerializeField] private float defaultPitch = 40f;
        [SerializeField] private float pitchReturnSpeed = 3f;
        [SerializeField] private float dashPitch = 20f;

        [Header("Focus Tweaks")]
        [Range(0f, 1f)][SerializeField] private float focusBias = 0.3f;
        [SerializeField] private float playerFocusHeight = 1.0f;
        [SerializeField] private float enemyFocusHeight = 1.0f;

        private Transform currentTarget;
        public Transform CurrentTarget => currentTarget;

        private bool isHardLocked = false;

        private float manualYawOffset = 0f;
        private float currentYaw = 0f;
        private float currentPitch = 40f;
        private float targetPitch = 40f;
        private float currentBaseZ = -4f;
        private float currentFocusWeight = 0f;

        private float yawVelocity = 0f;
        private float pitchVelocity = 0f;
        private float zVelocity = 0f;
        private float focusWeightVelocity = 0f;
        private Vector3 enemyPositionVelocity = Vector3.zero;

        private Vector2 lookInput;
        private Vector3 currentLookAtPoint;

        // Это и есть наша "виртуальная" цель
        private Vector3 lastEnemyPosition;

        private float dashStartTime = 0f;
        private float dashTotalDuration = 0.1f;
        private float dashEndTime = 0f;
        private Vector3 dashDirection = Vector3.forward;

        private void Start()
        {
            if (player != null)
            {
                currentYaw = player.eulerAngles.y;
                currentPitch = defaultPitch;
                targetPitch = defaultPitch;
                currentBaseZ = outOfCombatZ;
                lastEnemyPosition = player.position;
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
            // ИСПРАВЛЕНИЕ: Если мы захватываем цель ВПЕРВЫЕ (из пустоты), 
            // мгновенно телепортируем виртуальную точку, чтобы камера плавно сфокусировалась
            // без перелета через всю карту от позиции игрока.
            if (newTarget != null && currentTarget == null)
            {
                lastEnemyPosition = newTarget.position;
            }

            currentTarget = newTarget;
            isHardLocked = hardLock;
        }

        public void TriggerDashCam(Vector3 dir, float duration)
        {
            if (currentTarget != null) return;

            dashDirection = dir;
            dashDirection.y = 0f;

            dashStartTime = Time.time;
            dashTotalDuration = duration > 0 ? duration : 0.25f;
            dashEndTime = Time.time + duration + 0.1f;
        }

        private void LateUpdate()
        {
            if (player == null) return;

            // --- КИНЕМАТОГРАФИЧНАЯ СМЕНА ЦЕЛЕЙ ---
            if (currentTarget != null)
            {
                // Виртуальная точка мягко перелетает от прошлого врага к новому
                lastEnemyPosition = Vector3.SmoothDamp(lastEnemyPosition, currentTarget.position, ref enemyPositionVelocity, targetSwitchSmoothTime);
            }

            bool isDashCamActive = Time.time < dashEndTime && currentTarget == null;
            float dashProgress = isDashCamActive ? Mathf.Clamp01((Time.time - dashStartTime) / dashTotalDuration) : 0f;

            // 1. YAW (ГОРИЗОНТАЛЬ)
            float targetBaseYaw;
            float currentSmoothTime;

            if (isDashCamActive)
            {
                targetBaseYaw = dashDirection != Vector3.zero ? Quaternion.LookRotation(dashDirection).eulerAngles.y : currentYaw;
                float easeInProgress = dashProgress * dashProgress;
                currentSmoothTime = Mathf.Lerp(yawSmoothPeace, 0.02f, easeInProgress);
            }
            else if (currentTarget != null)
            {
                // Используем виртуальную летящую точку для вычисления разворота камеры
                Vector3 dirToTarget = lastEnemyPosition - player.position;
                dirToTarget.y = 0;

                if (dirToTarget.sqrMagnitude > 0.001f)
                {
                    targetBaseYaw = Quaternion.LookRotation(dirToTarget).eulerAngles.y;
                }
                else
                {
                    float charAngle = characterModel != null ? characterModel.eulerAngles.y : player.eulerAngles.y;
                    targetBaseYaw = charAngle;
                }

                currentSmoothTime = yawSmoothCombat;
            }
            else
            {
                targetBaseYaw = characterModel != null ? characterModel.eulerAngles.y : player.eulerAngles.y;
                currentSmoothTime = yawSmoothPeace;
            }

            if (Mathf.Abs(lookInput.x) > 0.05f)
            {
                manualYawOffset += lookInput.x * horizontalSpeed * Time.deltaTime;
            }
            else
            {
                manualYawOffset = Mathf.Lerp(manualYawOffset, 0f, manualResetSpeed * Time.deltaTime);
            }
            manualYawOffset = Mathf.Clamp(manualYawOffset, -maxYawAngle, maxYawAngle);

            float targetYaw = targetBaseYaw + manualYawOffset;
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocity, currentSmoothTime);

            // 2. PITCH (ВЕРТИКАЛЬ)
            if (Mathf.Abs(lookInput.y) > 0.05f)
            {
                targetPitch -= lookInput.y * verticalSpeed * Time.deltaTime;
            }
            else
            {
                float desiredPitch = isDashCamActive ? dashPitch : defaultPitch;
                float currentPitchSpeed = isDashCamActive ? pitchReturnSpeed * 3f : pitchReturnSpeed;
                targetPitch = Mathf.Lerp(targetPitch, desiredPitch, currentPitchSpeed * Time.deltaTime);
            }
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

            float currentPitchSmooth = isDashCamActive ? 0.05f : pitchSmoothTime;
            currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, currentPitchSmooth);

            // 3. ZOOM (ДИСТАНЦИЯ)
            float targetZ = currentTarget != null ? inCombatZ : outOfCombatZ;
            currentBaseZ = Mathf.SmoothDamp(currentBaseZ, targetZ, ref zVelocity, zoomSmoothTime);

            Vector3 dynamicBaseOffset = new Vector3(baseXYOffset.x, baseXYOffset.y, currentBaseZ);
            float defaultDistance = dynamicBaseOffset.magnitude;

            float normalizedPitchDiff = 0f;
            if (currentPitch > defaultPitch)
                normalizedPitchDiff = (currentPitch - defaultPitch) / (maxPitch - defaultPitch);
            else if (currentPitch < defaultPitch)
                normalizedPitchDiff = (defaultPitch - currentPitch) / (defaultPitch - minPitch);

            float currentDistance = Mathf.Lerp(defaultDistance, minZoomDistance, normalizedPitchDiff);

            // 4. ПРИМЕНЕНИЕ КООРДИНАТ КАМЕРЫ
            Quaternion cameraRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 pivotPoint = player.position + Vector3.up * playerFocusHeight;

            Vector3 targetPosition = pivotPoint + cameraRotation * new Vector3(0f, 0f, -currentDistance);

            float currentFollowSpeed = isDashCamActive ? Mathf.Lerp(followSpeed, followSpeed * 2.5f, dashProgress) : followSpeed;
            transform.position = Vector3.Lerp(transform.position, targetPosition, currentFollowSpeed * Time.deltaTime);

            // 5. ФОКУС ВЗГЛЯДА
            float currentFocusSmooth = isDashCamActive ? Mathf.Lerp(focusSmoothTime, 0.01f, dashProgress * dashProgress) : focusSmoothTime;
            float targetFocusWeight = currentTarget != null ? focusBias : 0f;

            currentFocusWeight = Mathf.SmoothDamp(currentFocusWeight, targetFocusWeight, ref focusWeightVelocity, currentFocusSmooth);

            Vector3 eFocus = pivotPoint;
            if (currentFocusWeight > 0.001f)
            {
                // Взгляд камеры также привязан к плавно летящей виртуальной точке
                eFocus = lastEnemyPosition + Vector3.up * enemyFocusHeight;
            }

            currentLookAtPoint = Vector3.Lerp(pivotPoint, eFocus, currentFocusWeight);

            Vector3 lookDirection = currentLookAtPoint - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }
}