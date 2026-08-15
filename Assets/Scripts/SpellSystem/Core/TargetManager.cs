using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    public class TargetManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private SpellCaster spellCaster;
        [SerializeField] private Transform playerTransform;

        [Header("Reticle / Marker UI")]
        [Tooltip("UI элемент (Image) маркера цели на Canvas")]
        [SerializeField] private RectTransform reticleUI;
        [SerializeField] private Image reticleImage;

        [Header("Sprites")]
        [Tooltip("Спрайт в свободном режиме (автозахват)")]
        [SerializeField] private Sprite freeTargetSprite;
        [Tooltip("Спрайт при жестком локе (ручной выбор)")]
        [SerializeField] private Sprite hardLockSprite;

        [Header("Settings")]
        [SerializeField] private float autoTargetRadius = 15f;
        [SerializeField] private float loseTargetDistance = 25f;
        [Tooltip("Скорость вращения прицела в свободном режиме (градусов в секунду)")]
        [SerializeField] private float rotationSpeed = 90f;

        private Transform currentTarget;
        private bool isHardLocked = false;

        private void Awake()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (spellCaster == null) spellCaster = FindAnyObjectByType<SpellCaster>();
            if (playerTransform == null) playerTransform = transform;
            if (reticleImage == null && reticleUI != null) reticleImage = reticleUI.GetComponent<Image>();

            SetReticleActive(false);
        }

        private void Update()
        {
            if (mainCamera == null || spellCaster == null) return;

            HandleManualLock();

            if (isHardLocked)
            {
                CheckHardLockValidity();
            }
            else
            {
                FindClosestTarget();
            }

            UpdateReticle();
        }

        // Обработка тапа для жесткого лока или снятия лока
        private void HandleManualLock()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
            {
                Vector2 screenPos = pointer.position.ReadValue();
                Ray ray = mainCamera.ScreenPointToRay(screenPos);

                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                {
                    // Ищем DummyTarget на объекте или его родителях
                    DummyTarget enemy = hit.collider.GetComponentInParent<DummyTarget>();
                    if (enemy != null)
                    {
                        Transform enemyRoot = enemy.transform;

                        if (isHardLocked && currentTarget == enemyRoot)
                        {
                            UnlockTarget();
                            Debug.Log("[TargetManager] Лок снят пользователем. Возврат в свободный режим.");
                        }
                        else
                        {
                            currentTarget = enemyRoot;
                            isHardLocked = true;
                            spellCaster.SetTarget(currentTarget, true);
                            Debug.Log($"[TargetManager] ЖЕСТКИЙ ЛОК на: <color=yellow>{currentTarget.name}</color>");
                        }
                    }
                    else
                    {
                        if (isHardLocked)
                        {
                            UnlockTarget();
                            Debug.Log("[TargetManager] Лок снят тапом в пустоту.");
                        }
                    }
                }
            }
        }

        // Автозахват ближайшего врага
        private void FindClosestTarget()
        {
            Collider[] hits = Physics.OverlapSphere(playerTransform.position, autoTargetRadius);

            Transform closest = null;
            float minDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                // Используем GetComponentInParent для надежного поиска корня врага
                DummyTarget enemy = hit.GetComponentInParent<DummyTarget>();
                if (enemy != null)
                {
                    Transform enemyRoot = enemy.transform;
                    float dist = Vector3.Distance(playerTransform.position, enemyRoot.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closest = enemyRoot;
                    }
                }
            }

            if (currentTarget != closest)
            {
                currentTarget = closest;
                spellCaster.SetTarget(currentTarget, false);
            }
        }

        // Полный сброс лока
        private void UnlockTarget()
        {
            isHardLocked = false;
            currentTarget = null;
            spellCaster.SetTarget(null, false);
        }

        // Проверка валидности жесткого лока
        private void CheckHardLockValidity()
        {
            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                UnlockTarget();
                return;
            }

            float dist = Vector3.Distance(playerTransform.position, currentTarget.position);
            if (dist > loseTargetDistance)
            {
                Debug.Log("[TargetManager] Цель слишком далеко. Лок сброшен.");
                UnlockTarget();
            }
        }

        // Обновление положения, вращения и спрайта маркера на экране
        private void UpdateReticle()
        {
            if (currentTarget != null && reticleUI != null)
            {
                SetReticleActive(true);

                Vector3 screenPos = mainCamera.WorldToScreenPoint(currentTarget.position);

                if (screenPos.z > 0)
                {
                    reticleUI.position = screenPos;

                    if (isHardLocked)
                    {
                        if (reticleImage != null && hardLockSprite != null)
                            reticleImage.sprite = hardLockSprite;

                        reticleUI.localRotation = Quaternion.identity;
                    }
                    else
                    {
                        if (reticleImage != null && freeTargetSprite != null)
                            reticleImage.sprite = freeTargetSprite;

                        reticleUI.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
                    }
                }
                else
                {
                    SetReticleActive(false);
                }
            }
            else
            {
                SetReticleActive(false);
            }
        }

        private void SetReticleActive(bool active)
        {
            if (reticleUI != null && reticleUI.gameObject.activeSelf != active)
            {
                reticleUI.gameObject.SetActive(active);
            }
        }
    }
}