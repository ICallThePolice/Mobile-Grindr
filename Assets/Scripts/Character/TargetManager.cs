using UnityEngine;
using UnityEngine.UI;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    public class TargetManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private SimpleThirdPersonCamera thirdPersonCamera;

        [Header("3D Reticle / Marker")]
        [SerializeField] private Transform reticle3D;
        [SerializeField] private SpriteRenderer reticleSpriteRenderer;
        [SerializeField] private Image reticleImage3D;

        [SerializeField] private Sprite freeTargetSprite;
        [SerializeField] private Sprite hardLockSprite;
        [SerializeField] private Vector3 reticleOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Reticle Smoothness")]
        [SerializeField] private float reticleMoveSpeed = 25f;

        [Header("Lock Button UI")]
        [SerializeField] private Button lockButton;
        [SerializeField] private Image lockButtonImage;
        [SerializeField] private Sprite lockIcon;
        [SerializeField] private Sprite unlockIcon;

        [Header("Targeting Settings")]
        [SerializeField] private float autoTargetRadius = 15f;
        [SerializeField] private float loseTargetDistance = 25f;
        [SerializeField] private float rotationSpeed = 90f;

        [Header("Cooldown Settings")]
        [Tooltip("Время блокировки поиска новой цели после сброса (в секундах)")]
        [SerializeField] private float targetCooldownDuration = 2f;

        private Transform hardTarget;
        private Transform softTarget;
        private Transform previousSoftTarget; // Память о прошлой цели
        private float currentReticleRotation = 0f;
        private float cooldownTimer = 0f;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (spellCaster == null) spellCaster = FindAnyObjectByType<SpellCaster>();
            if (playerTransform == null) playerTransform = transform;
            if (thirdPersonCamera == null) thirdPersonCamera = FindAnyObjectByType<SimpleThirdPersonCamera>();

            if (lockButton != null)
            {
                lockButton.onClick.AddListener(ToggleTargetLock);
                if (lockButtonImage == null) lockButtonImage = lockButton.GetComponent<Image>();
            }

            SetReticleActive(false);
            UpdateButtonState();
        }

        private void OnDestroy()
        {
            if (lockButton != null) lockButton.onClick.RemoveListener(ToggleTargetLock);
        }

        private void Update()
        {
            if (mainCamera == null || spellCaster == null) return;

            CheckHardTargetValidity();

            // Логика блокировки (кулдауна) свободного таргета
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
                softTarget = null; // Принудительно отключаем свободную цель
            }
            else
            {
                FindClosestTarget();
            }

            // Если мы только что потеряли цель (и не в хард-локе) - запускаем таймер на 2 секунды
            if (previousSoftTarget != null && softTarget == null && hardTarget == null && cooldownTimer <= 0f)
            {
                cooldownTimer = targetCooldownDuration;
            }
            previousSoftTarget = softTarget;

            Transform activeTarget = hardTarget != null ? hardTarget : softTarget;
            bool isHardLocked = (hardTarget != null);

            if (activeTarget != null)
            {
                spellCaster.SetTarget(activeTarget, isHardLocked);
                if (thirdPersonCamera != null) thirdPersonCamera.SetTarget(activeTarget, isHardLocked);
            }
            else
            {
                spellCaster.SetTarget(null, false);
                if (thirdPersonCamera != null) thirdPersonCamera.SetTarget(null, false);
            }

            UpdateReticle(activeTarget);
            UpdateButtonState();
        }

        public void ToggleTargetLock()
        {
            if (hardTarget != null) hardTarget = null;
            else if (softTarget != null) hardTarget = softTarget;
        }

        private void FindClosestTarget()
        {
            Collider[] hits = Physics.OverlapSphere(playerTransform.position, autoTargetRadius);
            float minDistance = float.MaxValue;
            softTarget = null;

            foreach (var hit in hits)
            {
                DummyTarget enemy = hit.GetComponentInParent<DummyTarget>();
                if (enemy != null)
                {
                    float dist = Vector3.Distance(playerTransform.position, enemy.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        softTarget = enemy.transform;
                    }
                }
            }
        }

        private void CheckHardTargetValidity()
        {
            if (hardTarget == null) return;
            if (!hardTarget.gameObject.activeInHierarchy || Vector3.Distance(playerTransform.position, hardTarget.position) > loseTargetDistance)
            {
                hardTarget = null;
            }
        }

        private void UpdateReticle(Transform activeTarget)
        {
            if (activeTarget != null && reticle3D != null)
            {
                Vector3 targetPos = activeTarget.position + reticleOffset;

                if (!reticle3D.gameObject.activeSelf)
                {
                    reticle3D.position = targetPos;
                    SetReticleActive(true);
                }
                else
                {
                    reticle3D.position = Vector3.Lerp(reticle3D.position, targetPos, Time.deltaTime * reticleMoveSpeed);
                }

                reticle3D.rotation = mainCamera.transform.rotation;

                if (hardTarget != null)
                {
                    if (reticleSpriteRenderer != null) reticleSpriteRenderer.sprite = hardLockSprite;
                    if (reticleImage3D != null) reticleImage3D.sprite = hardLockSprite;
                    currentReticleRotation = 0f;
                }
                else
                {
                    if (reticleSpriteRenderer != null) reticleSpriteRenderer.sprite = freeTargetSprite;
                    if (reticleImage3D != null) reticleImage3D.sprite = freeTargetSprite;
                    currentReticleRotation += rotationSpeed * Time.deltaTime;
                }

                reticle3D.rotation *= Quaternion.Euler(0f, 0f, currentReticleRotation);
            }
            else
            {
                SetReticleActive(false);
            }
        }

        private void UpdateButtonState()
        {
            if (lockButton == null) return;

            if (hardTarget != null)
            {
                if (!lockButton.gameObject.activeSelf) lockButton.gameObject.SetActive(true);
                if (lockButtonImage != null && unlockIcon != null) lockButtonImage.sprite = unlockIcon;
            }
            else if (softTarget != null)
            {
                if (!lockButton.gameObject.activeSelf) lockButton.gameObject.SetActive(true);
                if (lockButtonImage != null && lockIcon != null) lockButtonImage.sprite = lockIcon;
            }
            else
            {
                if (lockButton.gameObject.activeSelf) lockButton.gameObject.SetActive(false);
            }
        }

        private void SetReticleActive(bool active)
        {
            if (reticle3D != null && reticle3D.gameObject.activeSelf != active)
                reticle3D.gameObject.SetActive(active);
        }
    }
}