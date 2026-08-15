using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
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

        [Header("Reticle / Marker UI")]
        [SerializeField] private RectTransform reticleUI;
        [SerializeField] private Image reticleImage;
        [SerializeField] private Sprite freeTargetSprite;
        [SerializeField] private Sprite hardLockSprite;

        [Tooltip("Смещение маркера по высоте, чтобы он висел на теле врага, а не в ногах")]
        [SerializeField] private Vector3 reticleOffset = new Vector3(0f, 1.5f, 0f); // <--- НОВАЯ НАСТРОЙКА

        [Header("Targeting Settings")]
        [SerializeField] private float autoTargetRadius = 15f;
        [SerializeField] private float loseTargetDistance = 25f;
        [SerializeField] private float rotationSpeed = 90f;

        private Transform hardTarget;
        private Transform softTarget;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (spellCaster == null) spellCaster = FindAnyObjectByType<SpellCaster>();
            if (playerTransform == null) playerTransform = transform;
            if (reticleImage == null && reticleUI != null) reticleImage = reticleUI.GetComponent<Image>();
            if (thirdPersonCamera == null) thirdPersonCamera = FindAnyObjectByType<SimpleThirdPersonCamera>();

            SetReticleActive(false);
        }

        private void Update()
        {
            if (mainCamera == null || spellCaster == null) return;

            CheckHardTargetValidity();
            HandleManualLock();
            FindClosestTarget();

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
        }

        private void HandleManualLock()
        {
            var pointer = Pointer.current;
            if (pointer == null || !pointer.press.wasPressedThisFrame) return;

            Ray ray = mainCamera.ScreenPointToRay(pointer.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                DummyTarget enemy = hit.collider.GetComponentInParent<DummyTarget>();
                if (enemy != null)
                {
                    hardTarget = (hardTarget == enemy.transform) ? null : enemy.transform;
                }
                else
                {
                    hardTarget = null;
                }
            }
            else
            {
                hardTarget = null;
            }
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
            if (activeTarget != null && reticleUI != null)
            {
                SetReticleActive(true);

                // ПРИМЕНЯЕМ СМЕЩЕНИЕ ЗДЕСЬ:
                Vector3 targetPos = activeTarget.position + reticleOffset;
                Vector3 screenPos = mainCamera.WorldToScreenPoint(targetPos);

                if (screenPos.z > 0)
                {
                    reticleUI.position = screenPos;
                    if (hardTarget != null)
                    {
                        if (reticleImage != null) reticleImage.sprite = hardLockSprite;
                        reticleUI.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        if (reticleImage != null) reticleImage.sprite = freeTargetSprite;
                        reticleUI.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
                    }
                }
                else SetReticleActive(false);
            }
            else SetReticleActive(false);
        }

        private void SetReticleActive(bool active)
        {
            if (reticleUI != null && reticleUI.gameObject.activeSelf != active)
                reticleUI.gameObject.SetActive(active);
        }
    }
}