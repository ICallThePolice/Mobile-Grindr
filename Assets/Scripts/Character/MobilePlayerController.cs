using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SpellSystem.Data;
using SpellSystem.Testing;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class MobilePlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeed = 15f;

        [Header("Dash Settings (Base)")]
        [SerializeField] private float dashSpeed = 20f;
        [SerializeField] private float dashDuration = 0.25f;
        [SerializeField] private float dashCooldown = 1.5f;

        [Header("Elemental Dash Settings")]
        [SerializeField] private EnergyDataSO currentDashEnergy;

        [Header("- Vital (Life)")]
        [SerializeField] private float vitalBaseMultiplier = 1.3f;
        [SerializeField] private float vitalEnhancedMultiplier = 1.8f;
        [SerializeField] private float vitalBoostDuration = 1.5f;

        [Header("- Ereb (Darkness)")]
        [SerializeField] private Material erebGhostMaterial; // <-- МАТЕРИАЛ ДЛЯ ФОРМЫ ЭРЕБА
        [SerializeField] private float erebSpeedMultiplier = 1.3f;
        [SerializeField] private float erebBoostDuration = 2f;
        [SerializeField] private float erebRadius = 3f;
        [SerializeField] private float erebDamagePerTick = 3f;
        [SerializeField] private float erebTickInterval = 0.5f; // <-- ЧАСТОТА ТИКОВ (Раз в 0.5 сек)

        [Header("- Psy (Teleport & Clone)")]
        [SerializeField] private Material psyCloneMaterial;
        [SerializeField] private GameObject psyAoEPrefab;
        [SerializeField] private float psyExplosionRadius = 4f;
        [SerializeField] private float psyBaseDamage = 15f;
        [SerializeField] private float maxChargeTime = 1.5f;
        [SerializeField] private float maxChargeMultiplier = 2.5f;

        [Header("UI Dash References")]
        [SerializeField] private Button dashButton;
        [SerializeField] private Image cooldownImage;

        [Header("References")]
        [SerializeField] private SimpleThirdPersonCamera cameraScript;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform characterModel;
        [SerializeField] private Camera mainCamera;

        private CharacterController controller;
        private Renderer[] playerRenderers;
        private bool isDashing = false;
        private bool isDashCooldown = false;
        private float originalMoveSpeed;

        private bool isChargingDash = false;
        private float currentChargeTime = 0f;

        private Coroutine erebRoutine;
        private Coroutine vitalRoutine;

        // Словарь для хранения оригинальных материалов персонажа
        private Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            originalMoveSpeed = moveSpeed;

            if (cameraScript == null) cameraScript = FindAnyObjectByType<SimpleThirdPersonCamera>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (characterModel == null && animator != null) characterModel = animator.transform;
            if (mainCamera == null) mainCamera = Camera.main;

            if (characterModel != null) playerRenderers = characterModel.GetComponentsInChildren<Renderer>();

            if (cooldownImage != null) cooldownImage.fillAmount = 0f;

            SetupDashButtonEvents();

            if (currentDashEnergy != null) SetDashEnergy(currentDashEnergy);
        }

        private void Update()
        {
            Vector2 moveInput = GetUniversalMoveInput();
            Vector2 lookInput = GetUniversalLookInput();

            if (!isDashing)
            {
                Move(moveInput);
            }

            if (cameraScript != null)
            {
                cameraScript.SetLookInput(lookInput);
            }

            if (isChargingDash)
            {
                currentChargeTime += Time.deltaTime;
                currentChargeTime = Mathf.Clamp(currentChargeTime, 0f, maxChargeTime);
            }
        }

        private void SetupDashButtonEvents()
        {
            if (dashButton != null)
            {
                dashButton.onClick.RemoveAllListeners();

                EventTrigger trigger = dashButton.gameObject.GetComponent<EventTrigger>();
                if (trigger == null) trigger = dashButton.gameObject.AddComponent<EventTrigger>();

                EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
                pointerDown.callback.AddListener((data) => { OnDashButtonDown(); });
                trigger.triggers.Add(pointerDown);

                EventTrigger.Entry pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
                pointerUp.callback.AddListener((data) => { OnDashButtonUp(); });
                trigger.triggers.Add(pointerUp);
            }
        }

        public void SetDashEnergy(EnergyDataSO newEnergy)
        {
            currentDashEnergy = newEnergy;
            if (dashButton != null && newEnergy != null)
            {
                Color btnColor = newEnergy.primaryColor;
                btnColor.a = 1f;
                dashButton.image.color = btnColor;
            }
        }

        private void OnDashButtonDown()
        {
            if (isDashing || isDashCooldown) return;

            string eName = GetCurrentEnergyName();

            if (eName.Contains("psy"))
            {
                isChargingDash = true;
                currentChargeTime = 0f;
            }
            else
            {
                StartCoroutine(DashRoutine(1f));
            }
        }

        private void OnDashButtonUp()
        {
            if (isChargingDash)
            {
                isChargingDash = false;
                float chargeRatio = currentChargeTime / maxChargeTime;
                float finalMultiplier = Mathf.Lerp(1f, maxChargeMultiplier, chargeRatio);

                StartCoroutine(DashRoutine(finalMultiplier));
            }
        }

        private string GetCurrentEnergyName()
        {
            if (currentDashEnergy == null) return "";
            string eName = !string.IsNullOrEmpty(currentDashEnergy.energyName) ? currentDashEnergy.energyName : currentDashEnergy.name;
            return eName.ToLower();
        }

        private IEnumerator DashRoutine(float chargeMultiplier)
        {
            isDashing = true;
            isDashCooldown = true;

            if (dashButton != null) dashButton.interactable = false;
            if (cooldownImage != null) cooldownImage.fillAmount = 1f;

            Vector3 dashDir = characterModel.forward;
            Vector2 input = GetUniversalMoveInput();

            if (input.sqrMagnitude > 0.05f && mainCamera != null)
            {
                Vector3 camForward = mainCamera.transform.forward;
                Vector3 camRight = mainCamera.transform.right;
                camForward.y = 0; camRight.y = 0;
                dashDir = (camRight * input.x + camForward * input.y).normalized;

                if (characterModel != null) characterModel.rotation = Quaternion.LookRotation(dashDir);
            }

            if (cameraScript != null) cameraScript.TriggerDashCam(dashDir, dashDuration);

            string eName = GetCurrentEnergyName();

            // --- 1. ЛОГИКА PSY (ТЕЛЕПОРТ + КЛОН + ВЗРЫВЫ) ---
            if (eName.Contains("psy"))
            {
                Vector3 startPos = transform.position;
                Quaternion startRot = characterModel.rotation;

                float maxDist = (dashSpeed * dashDuration) * (1f + (chargeMultiplier - 1f) * 0.5f);
                Vector3 targetPos = startPos + dashDir * maxDist;

                if (Physics.SphereCast(startPos, controller.radius, dashDir, out RaycastHit hit, maxDist))
                {
                    if (!hit.collider.isTrigger) targetPos = hit.point - dashDir * (controller.radius + 0.1f);
                }

                SpawnPsyExplosion(startPos, psyExplosionRadius, psyBaseDamage, chargeMultiplier);
                SpawnPsyClone(startPos, startRot, chargeMultiplier);

                controller.enabled = false;
                transform.position = targetPos;
                controller.enabled = true;

                SpawnPsyExplosion(targetPos, psyExplosionRadius, psyBaseDamage, chargeMultiplier);

                yield return new WaitForSeconds(dashDuration);
            }
            // --- 2. ЛОГИКА EREB И VITAL (ОБЫЧНЫЙ ФИЗИЧЕСКИЙ РЫВОК) ---
            else
            {
                if (eName.Contains("ereb"))
                {
                    if (erebRoutine != null) StopCoroutine(erebRoutine);
                    erebRoutine = StartCoroutine(ErebBoostRoutine());
                }

                float startTime = Time.time;
                while (Time.time < startTime + dashDuration)
                {
                    controller.Move(dashDir * dashSpeed * Time.deltaTime);
                    yield return null;
                }

                if (eName.Contains("vital"))
                {
                    if (vitalRoutine != null) StopCoroutine(vitalRoutine);
                    vitalRoutine = StartCoroutine(VitalBoostRoutine());
                }
            }

            isDashing = false;

            float cooldownTimer = dashCooldown;
            while (cooldownTimer > 0)
            {
                cooldownTimer -= Time.deltaTime;
                if (cooldownImage != null) cooldownImage.fillAmount = cooldownTimer / dashCooldown;
                yield return null;
            }

            isDashCooldown = false;
            if (dashButton != null) dashButton.interactable = true;
        }

        // --- МЕХАНИКИ ПОСТ-РЫВКА ---

        private IEnumerator VitalBoostRoutine()
        {
            bool isEnemyNear = false;
            Collider[] hits = Physics.OverlapSphere(transform.position, 15f);
            foreach (var hit in hits)
            {
                if (hit.GetComponentInParent<DummyTarget>() != null)
                {
                    isEnemyNear = true;
                    break;
                }
            }

            float mult = isEnemyNear ? vitalEnhancedMultiplier : vitalBaseMultiplier;
            moveSpeed = originalMoveSpeed * mult;

            yield return new WaitForSeconds(vitalBoostDuration);
            moveSpeed = originalMoveSpeed;
        }

        // ИСПРАВЛЕНИЕ EREB: Замена материала и использование настраиваемого интервала тиков
        private IEnumerator ErebBoostRoutine()
        {
            moveSpeed = originalMoveSpeed * erebSpeedMultiplier;
            SetErebMaterial(true);

            float startTime = Time.time;
            float nextErebTick = 0f;

            while (Time.time < startTime + erebBoostDuration)
            {
                if (Time.time >= nextErebTick)
                {
                    ApplyErebDamage();
                    nextErebTick = Time.time + erebTickInterval; // Используем настройку вместо жесткого 0.1
                }
                yield return null;
            }

            SetErebMaterial(false);
            moveSpeed = originalMoveSpeed;
        }

        private void ApplyErebDamage()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, erebRadius);
            foreach (var hit in hits)
            {
                DummyTarget target = hit.GetComponentInParent<DummyTarget>();
                if (target != null)
                {
                    target.TakeDamage(erebDamagePerTick, GetCurrentEnergyName(), currentDashEnergy.primaryColor);
                }
            }
        }

        // ИСПРАВЛЕНИЕ EREB: Умная замена материалов
        private void SetErebMaterial(bool isActive)
        {
            if (playerRenderers == null) return;

            if (isActive && erebGhostMaterial != null)
            {
                originalMaterials.Clear();
                foreach (var rnd in playerRenderers)
                {
                    // Запоминаем родные материалы
                    originalMaterials[rnd] = rnd.materials;

                    // Создаем массив с материалом призрака
                    Material[] ghostMats = new Material[rnd.materials.Length];
                    for (int i = 0; i < ghostMats.Length; i++)
                    {
                        ghostMats[i] = erebGhostMaterial;
                    }
                    rnd.materials = ghostMats;
                }
            }
            else
            {
                // Возвращаем родные материалы
                foreach (var kvp in originalMaterials)
                {
                    if (kvp.Key != null)
                    {
                        kvp.Key.materials = kvp.Value;
                    }
                }
                originalMaterials.Clear();
            }
        }

        // --- АРХИТЕКТУРА КЛОНА И АОЕ (PSY) ---

        private void SpawnPsyClone(Vector3 pos, Quaternion rot, float chargeMult)
        {
            GameObject cloneObj = new GameObject("PsyClone");
            cloneObj.transform.position = pos;
            cloneObj.transform.rotation = rot;

            List<Mesh> bakedMeshes = new List<Mesh>();

            SkinnedMeshRenderer[] smrs = characterModel.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in smrs)
            {
                Mesh bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh);
                bakedMeshes.Add(bakedMesh);

                GameObject meshObj = new GameObject(smr.gameObject.name + "_Clone");
                meshObj.transform.SetParent(cloneObj.transform);
                meshObj.transform.localPosition = smr.transform.localPosition;
                meshObj.transform.localRotation = smr.transform.localRotation;
                meshObj.transform.localScale = smr.transform.localScale;

                MeshFilter mf = meshObj.AddComponent<MeshFilter>();
                mf.mesh = bakedMesh;

                MeshRenderer mr = meshObj.AddComponent<MeshRenderer>();
                mr.material = psyCloneMaterial != null ? psyCloneMaterial : smr.material;
            }

            StartCoroutine(PsyCloneTickRoutine(cloneObj, chargeMult, bakedMeshes));
        }

        private IEnumerator PsyCloneTickRoutine(GameObject clone, float chargeMult, List<Mesh> bakedMeshes)
        {
            float finalDamage = psyBaseDamage * chargeMult;
            float finalRadius = psyExplosionRadius * chargeMult;

            for (int i = 0; i < 3; i++)
            {
                yield return new WaitForSeconds(1f);
                if (clone == null) break;

                SpawnPsyExplosion(clone.transform.position, finalRadius, finalDamage, chargeMult);
            }

            if (clone != null) Destroy(clone);
            foreach (var m in bakedMeshes)
            {
                if (m != null) Destroy(m);
            }
        }

        private void SpawnPsyExplosion(Vector3 pos, float radius, float damage, float chargeMult)
        {
            if (psyAoEPrefab != null)
            {
                GameObject aoe = Instantiate(psyAoEPrefab, pos, Quaternion.identity);

                SpellAoE spellAoE = aoe.GetComponent<SpellAoE>();
                if (spellAoE != null && currentDashEnergy != null)
                {
                    spellAoE.Initialize(damage, radius, currentDashEnergy, chargeMult, 1);
                }
            }

            Collider[] hits = Physics.OverlapSphere(pos, radius);
            foreach (var hit in hits)
            {
                DummyTarget target = hit.GetComponentInParent<DummyTarget>();
                if (target != null)
                {
                    string enName = currentDashEnergy != null ? currentDashEnergy.name : "Psy";
                    Color enColor = currentDashEnergy != null ? currentDashEnergy.primaryColor : Color.white;
                    target.TakeDamage(damage, enName, enColor);
                }
            }
        }

        // ---- СТАНДАРТНОЕ УПРАВЛЕНИЕ ----
        private Vector2 GetUniversalMoveInput()
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed) input.y -= 1f;
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            }
            if (Gamepad.current != null)
            {
                Vector2 joystickInput = Gamepad.current.leftStick.ReadValue();
                if (joystickInput.sqrMagnitude > 0.05f) input = joystickInput;
            }
            if (input.magnitude > 1f) input.Normalize();
            return input;
        }

        private Vector2 GetUniversalLookInput()
        {
            Vector2 input = Vector2.zero;
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                input = Mouse.current.delta.ReadValue() * 0.05f;

            if (Gamepad.current != null)
            {
                Vector2 joystickInput = Gamepad.current.rightStick.ReadValue();
                if (joystickInput.sqrMagnitude > 0.05f) input = joystickInput;
            }
            return input;
        }

        private void Move(Vector2 input)
        {
            if (mainCamera == null) return;

            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            Vector3 moveDir = camRight * input.x + camForward * input.y;
            controller.Move(moveDir * moveSpeed * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat("Speed", input.magnitude);
            }

            if (moveDir.sqrMagnitude > 0.01f && characterModel != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                characterModel.rotation = Quaternion.Slerp(characterModel.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }
    }
}