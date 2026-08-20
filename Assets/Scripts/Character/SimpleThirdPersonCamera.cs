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
        public float cameraRadius = 0.3f;
        public float minDistance = 0.5f;

        [Header("Manual Control Constraints")]
        [SerializeField] private float horizontalSpeed = 150f;
        [SerializeField] private float verticalSpeed = 100f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 70f;

        [Header("Look Acceleration (Joystick)")]
        [SerializeField] private float lookThreshold = 0.65f;
        [SerializeField] private float slowLookMultiplier = 0.3f;
        [SerializeField] private float lookAccelerationSpeed = 8f;

        [Header("Camera Smoothness")]
        [Range(0.05f, 1f)][SerializeField] private float yawSmoothCombat = 0.2f;
        [Range(0.05f, 1f)][SerializeField] private float zoomSmoothTime = 0.3f;
        [Range(0.05f, 1f)][SerializeField] private float focusSmoothTime = 0.25f;
        [Range(0.05f, 1f)][SerializeField] private float targetSwitchSmoothTime = 0.3f;
        [Tooltip("Насколько плавно камера облетает острые углы препятствий")]
        [Range(0.05f, 0.5f)][SerializeField] private float collisionSmoothTime = 0.15f;

        [Header("Combat Zoom & Offset")]
        [SerializeField] private float outOfCombatZ = -4f;
        [SerializeField] private float inCombatZ = -5f;
        [SerializeField] private float minZoomDistance = 2.5f;
        [SerializeField] private Vector2 baseXYOffset = new Vector2(0f, 4f);

        [Header("Auto-Reset & Dash Settings")]
        [SerializeField] private float defaultPitch = 40f;
        [SerializeField] private float dashPitch = 20f;

        [Header("Focus Tweaks")]
        [Range(0f, 1f)][SerializeField] private float focusBias = 0.3f;
        [SerializeField] private float playerFocusHeight = 1.0f;
        [SerializeField] private float enemyFocusHeight = 1.0f;

        private Transform currentTarget;
        public Transform CurrentTarget => currentTarget;
        private bool isHardLocked = false;

        private Vector2 lookInput;
        private float currentYaw = 0f;
        private float currentPitch = 40f;
        private float currentBaseZ = -4f;
        private float currentFocusWeight = 0f;
        private float currentLookMultiplier;

        // Переменные для сглаживания коллизии
        private float currentCollisionDistance;
        private float collisionVelocity;

        private float yawVelocity = 0f;
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
            currentLookMultiplier = slowLookMultiplier;

            if (player != null)
            {
                currentYaw = player.eulerAngles.y;
                currentPitch = defaultPitch;
                currentBaseZ = outOfCombatZ;
                currentCollisionDistance = Mathf.Abs(outOfCombatZ);
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

            if (currentTarget != null)
            {
                lastEnemyPosition = Vector3.SmoothDamp(lastEnemyPosition, currentTarget.position, ref enemyPositionVelocity, targetSwitchSmoothTime);
            }

            bool isDashCamActive = Time.time < dashEndTime && currentTarget == null;

            // --- ПЛАВНЫЙ РАЗГОН КАМЕРЫ ---
            float inputMag = lookInput.magnitude;
            float stickMag = Mathf.Clamp01(inputMag);

            float targetLookMultiplier = (stickMag >= lookThreshold) ? 1.0f : slowLookMultiplier;

            if (stickMag > 0.05f)
            {
                currentLookMultiplier = Mathf.Lerp(currentLookMultiplier, targetLookMultiplier, Time.deltaTime * lookAccelerationSpeed);
            }
            else
            {
                currentLookMultiplier = slowLookMultiplier;
            }

            // --- 1 & 2. YAW & PITCH ---
            if (isDashCamActive)
            {
                float targetDashYaw = dashDirection != Vector3.zero ? Quaternion.LookRotation(dashDirection).eulerAngles.y : currentYaw;
                currentYaw = Mathf.SmoothDampAngle(currentYaw, targetDashYaw, ref yawVelocity, 0.1f);
                currentPitch = Mathf.Lerp(currentPitch, dashPitch, Time.deltaTime * 5f);
            }
            else if (currentTarget != null)
            {
                Vector3 dirToTarget = lastEnemyPosition - player.position;
                dirToTarget.y = 0;
                float targetCombatYaw = dirToTarget.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dirToTarget).eulerAngles.y : currentYaw;

                currentYaw = Mathf.SmoothDampAngle(currentYaw, targetCombatYaw, ref yawVelocity, yawSmoothCombat);

                if (Mathf.Abs(lookInput.y) > 0.05f)
                    currentPitch -= lookInput.y * verticalSpeed * currentLookMultiplier * Time.deltaTime;
            }
            else
            {
                if (Mathf.Abs(lookInput.x) > 0.05f)
                    currentYaw += lookInput.x * horizontalSpeed * currentLookMultiplier * Time.deltaTime;

                if (Mathf.Abs(lookInput.y) > 0.05f)
                    currentPitch -= lookInput.y * verticalSpeed * currentLookMultiplier * Time.deltaTime;
            }

            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

            // --- 3. ИДЕАЛЬНАЯ ДИСТАНЦИЯ (ZOOM) ---
            currentBaseZ = Mathf.SmoothDamp(currentBaseZ, currentTarget != null ? inCombatZ : outOfCombatZ, ref zVelocity, zoomSmoothTime);
            float defaultDistance = new Vector3(baseXYOffset.x, baseXYOffset.y, currentBaseZ).magnitude;

            float normalizedPitchDiff = currentPitch > defaultPitch ? (currentPitch - defaultPitch) / (maxPitch - defaultPitch) : (defaultPitch - currentPitch) / (defaultPitch - minPitch);
            float idealDistance = Mathf.Lerp(defaultDistance, minZoomDistance, normalizedPitchDiff);

            // --- 4. КОЛЛИЗИЯ И ПОЗИЦИЯ (ТЕПЕРЬ СО СГЛАЖИВАНИЕМ) ---
            Quaternion cameraRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 pivotPoint = player.position + Vector3.up * playerFocusHeight;
            Vector3 direction = cameraRotation * Vector3.back;

            float actualDistance = idealDistance;
            if (Physics.SphereCast(pivotPoint, cameraRadius, direction, out RaycastHit hit, idealDistance, collisionMask))
            {
                actualDistance = Mathf.Clamp(hit.distance, minDistance, idealDistance);
            }

            // МАГИЯ ЗДЕСЬ: Сглаживаем "прыжки" дистанции при ударах о неровные воксели
            float dynamicSmoothTime = (actualDistance < currentCollisionDistance) ? 0.05f : collisionSmoothTime; // Приближаем быстро, отдаляем плавно
            currentCollisionDistance = Mathf.SmoothDamp(currentCollisionDistance, actualDistance, ref collisionVelocity, dynamicSmoothTime);

            // Теперь камера не использует Lerp позиций, она всегда жестко привязана к оси, но сама дистанция на этой оси плавно меняется
            transform.position = pivotPoint + direction * currentCollisionDistance;

            // --- 5. ФОКУС ВЗГЛЯДА ---
            float currentFocusSmooth = isDashCamActive ? Mathf.Lerp(focusSmoothTime, 0.01f, 0f) : focusSmoothTime;
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