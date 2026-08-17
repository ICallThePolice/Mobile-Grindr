using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using SpellSystem.Data;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(CharacterController), typeof(MobileInputReader), typeof(PlayerMovement))]
    public class PlayerDashSystem : MonoBehaviour
    {
        [Header("Dash Settings")]
        [SerializeField] private float dashSpeed = 20f;
        [SerializeField] private float dashDuration = 0.25f;
        [SerializeField] private float dashCooldown = 1.5f;
        [SerializeField] private float maxChargeTime = 1.5f;
        [SerializeField] private float maxChargeMultiplier = 2.5f;

        [Header("References")]
        [SerializeField] private Button dashButton;
        [SerializeField] private Image cooldownImage;
        [SerializeField] private SimpleThirdPersonCamera cameraScript;
        [SerializeField] private Transform characterModel;

        public EnergyDataSO CurrentDashEnergy { get; private set; }

        private CharacterController controller;
        private MobileInputReader inputReader;
        private PlayerMovement playerMovement;
        private ElementalDashEffects elementalEffects;
        private Camera mainCamera;

        private bool isDashing = false;
        private bool isDashCooldown = false;
        private bool isChargingDash = false;
        private float currentChargeTime = 0f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            inputReader = GetComponent<MobileInputReader>();
            playerMovement = GetComponent<PlayerMovement>();
            elementalEffects = GetComponent<ElementalDashEffects>();
            mainCamera = Camera.main;

            if (cooldownImage != null) cooldownImage.fillAmount = 0f;
            SetupDashButtonEvents();
        }

        private void Update()
        {
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
            CurrentDashEnergy = newEnergy;
            if (dashButton != null && newEnergy != null)
            {
                Color btnColor = newEnergy.primaryColor;
                btnColor.a = 1f;
                dashButton.image.color = btnColor;
            }
        }

        private string GetCurrentEnergyName()
        {
            if (CurrentDashEnergy == null) return "";
            return (!string.IsNullOrEmpty(CurrentDashEnergy.energyName) ? CurrentDashEnergy.energyName : CurrentDashEnergy.name).ToLower();
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
            else StartCoroutine(DashRoutine(1f));
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

        private IEnumerator DashRoutine(float chargeMultiplier)
        {
            isDashing = true;
            isDashCooldown = true;
            playerMovement.CanMove = false; // Блокируем обычное движение на время рывка

            if (dashButton != null) dashButton.interactable = false;
            if (cooldownImage != null) cooldownImage.fillAmount = 1f;

            Vector3 dashDir = characterModel.forward;
            Vector2 input = inputReader.MoveInput;

            if (input.sqrMagnitude > 0.05f && mainCamera != null)
            {
                Vector3 camForward = mainCamera.transform.forward;
                Vector3 camRight = mainCamera.transform.right;
                camForward.y = 0; camRight.y = 0;
                camForward.Normalize(); camRight.Normalize();

                dashDir = (camRight * input.x + camForward * input.y).normalized;

                if (characterModel != null)
                {
                    Vector3 lookDir = dashDir;
                    if (playerMovement.currentTarget != null)
                    {
                        Vector3 dirToTarget = (playerMovement.currentTarget.position - transform.position).normalized;
                        dirToTarget.y = 0;
                        if (Vector3.Angle(dirToTarget, dashDir) < 120f) lookDir = dirToTarget;
                    }
                    characterModel.rotation = Quaternion.LookRotation(lookDir);
                }
            }

            if (cameraScript != null) cameraScript.TriggerDashCam(dashDir, dashDuration);

            string eName = GetCurrentEnergyName();

            // Вызываем визуальные эффекты
            if (elementalEffects != null)
            {
                elementalEffects.TriggerDashEffect(eName, chargeMultiplier, dashDir, dashDuration, dashSpeed);
            }

            // Физическое перемещение (Psy делает телепорт внутри ElementalEffects, поэтому его не двигаем)
            if (!eName.Contains("psy"))
            {
                float startTime = Time.time;
                while (Time.time < startTime + dashDuration)
                {
                    controller.Move(dashDir * dashSpeed * Time.deltaTime);
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(dashDuration);
            }

            // Пост-эффекты (например, ускорение)
            if (elementalEffects != null)
            {
                elementalEffects.TriggerPostDashEffect(eName);
            }

            playerMovement.CanMove = true;
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
    }
}