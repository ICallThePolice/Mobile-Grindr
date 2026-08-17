using UnityEngine;

namespace SpellSystem.Core
{
    public class SimpleThirdPersonCamera : MonoBehaviour
    {
        [Header("Target & References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform characterModel;

        [Header("Collision Settings (Physics)")]
        [Tooltip("Слои, с которыми камера будет сталкиваться. Поставь Default!")]
        public LayerMask collisionMask = ~0;
        [Tooltip("Размер физической сферы камеры. Защищает от проваливания в стены.")]
        public float cameraRadius = 0.3f;
        [Tooltip("Минимальная дистанция, на которую камера может подъехать к игроку при ударе о стену.")]
        public float minDistance = 0.5f;

        [Header("Manual Control Constraints")]
        [SerializeField] private float horizontalSpeed = 150f;
        [SerializeField] private float verticalSpeed = 100f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 70f;
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

        [Header("Combat Zoom & Offset")]
        [SerializeField] private float outOfCombatZ = -4f;
        [SerializeField] private float inCombatZ = -5f;
        [SerializeField] private float minZoomDistance = 2.5f;
        [SerializeField] private Vector2 baseXYOffset = new Vector2(0f, 4f);
        [SerializeField] private float followSpeed = 15f;
        [SerializeField] private float manualResetSpeed = 4f;

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

        private Vector2 lookInput;
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
        private Vector3 currentLookAtPoint;
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
            // Если мы захватываем цель ВПЕРВЫЕ (из пустоты), мгновенно телепортируем виртуальную точку
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

            // --- 0. КИНЕМАТОГРАФИЧНАЯ СМЕНА ЦЕЛЕЙ ---
            if (currentTarget != null)
            {
                lastEnemyPosition = Vector3.SmoothDamp(lastEnemyPosition, currentTarget.position, ref enemyPositionVelocity, targetSwitchSmoothTime);
            }

            bool isDashCamActive = Time.time < dashEndTime && currentTarget == null;
            float dashProgress = isDashCamActive ? Mathf.Clamp01((Time.time - dashStartTime) / dashTotalDuration) : 0f;

            // --- 1. YAW (ГОРИЗОНТАЛЬ) ---
            float targetBaseYaw;
            float currentSmoothTime;

            if (isDashCamActive)
            {
                targetBaseYaw = dashDirection != Vector3.zero ? Quaternion.LookRotation(dashDirection).eulerAngles.y : currentYaw;
                currentSmoothTime = Mathf.Lerp(yawSmoothPeace, 0.02f, dashProgress * dashProgress);
            }
            else if (currentTarget != null)
            {
                Vector3 dirToTarget = lastEnemyPosition - player.position;
                dirToTarget.y = 0;
                targetBaseYaw = dirToTarget.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dirToTarget).eulerAngles.y : (characterModel != null ? characterModel.eulerAngles.y : player.eulerAngles.y);
                currentSmoothTime = yawSmoothCombat;
            }
            else
            {
                targetBaseYaw = characterModel != null ? characterModel.eulerAngles.y : player.eulerAngles.y;
                currentSmoothTime = yawSmoothPeace;
            }

            if (Mathf.Abs(lookInput.x) > 0.05f) manualYawOffset += lookInput.x * horizontalSpeed * Time.deltaTime;
            else manualYawOffset = Mathf.Lerp(manualYawOffset, 0f, manualResetSpeed * Time.deltaTime);

            manualYawOffset = Mathf.Clamp(manualYawOffset, -maxYawAngle, maxYawAngle);
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetBaseYaw + manualYawOffset, ref yawVelocity, currentSmoothTime);

            // --- 2. PITCH (ВЕРТИКАЛЬ) ---
            if (Mathf.Abs(lookInput.y) > 0.05f) targetPitch -= lookInput.y * verticalSpeed * Time.deltaTime;
            else
            {
                float desiredPitch = isDashCamActive ? dashPitch : defaultPitch;
                targetPitch = Mathf.Lerp(targetPitch, desiredPitch, (isDashCamActive ? pitchReturnSpeed * 3f : pitchReturnSpeed) * Time.deltaTime);
            }
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, isDashCamActive ? 0.05f : pitchSmoothTime);

            // --- 3. ИДЕАЛЬНАЯ ДИСТАНЦИЯ (ZOOM) ---
            currentBaseZ = Mathf.SmoothDamp(currentBaseZ, currentTarget != null ? inCombatZ : outOfCombatZ, ref zVelocity, zoomSmoothTime);
            float defaultDistance = new Vector3(baseXYOffset.x, baseXYOffset.y, currentBaseZ).magnitude;

            float normalizedPitchDiff = currentPitch > defaultPitch ? (currentPitch - defaultPitch) / (maxPitch - defaultPitch) : (defaultPitch - currentPitch) / (defaultPitch - minPitch);
            float idealDistance = Mathf.Lerp(defaultDistance, minZoomDistance, normalizedPitchDiff);

            // --- 4. КОЛЛИЗИЯ И ПОЗИЦИЯ (SphereCast) ---
            Quaternion cameraRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 pivotPoint = player.position + Vector3.up * playerFocusHeight;
            Vector3 direction = cameraRotation * Vector3.back;

            // Пускаем физический луч от игрока назад к камере
            float actualDistance = idealDistance;
            if (Physics.SphereCast(pivotPoint, cameraRadius, direction, out RaycastHit hit, idealDistance, collisionMask))
            {
                // Врезались в гору! Сокращаем дистанцию
                actualDistance = Mathf.Clamp(hit.distance, minDistance, idealDistance);
            }

            Vector3 targetPosition = pivotPoint + direction * actualDistance;
            float currentFollowSpeed = isDashCamActive ? Mathf.Lerp(followSpeed, followSpeed * 2.5f, dashProgress) : followSpeed;

            // Плавно двигаем камеру на итоговую безопасную позицию
            transform.position = Vector3.Lerp(transform.position, targetPosition, currentFollowSpeed * Time.deltaTime);

            // --- 5. ФОКУС ВЗГЛЯДА ---
            float currentFocusSmooth = isDashCamActive ? Mathf.Lerp(focusSmoothTime, 0.01f, dashProgress * dashProgress) : focusSmoothTime;
            currentFocusWeight = Mathf.SmoothDamp(currentFocusWeight, currentTarget != null ? focusBias : 0f, ref focusWeightVelocity, currentFocusSmooth);

            Vector3 eFocus = currentFocusWeight > 0.001f ? (lastEnemyPosition + Vector3.up * enemyFocusHeight) : pivotPoint;
            currentLookAtPoint = Vector3.Lerp(pivotPoint, eFocus, currentFocusWeight);

            Vector3 lookDirection = currentLookAtPoint - transform.position;
            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }
    }
}