using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class MobilePlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeed = 15f;

        [Header("Dash Settings")]
        [SerializeField] private float dashSpeed = 20f;
        [SerializeField] private float dashDuration = 0.25f;
        [SerializeField] private float dashCooldown = 1.5f;

        [Header("UI Dash References")]
        [SerializeField] private Button dashButton;
        [SerializeField] private Image cooldownImage;

        [Header("References")]
        [SerializeField] private SimpleThirdPersonCamera cameraScript;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform characterModel;
        [SerializeField] private Camera mainCamera;

        private CharacterController controller;
        private bool isDashing = false;
        private bool isDashCooldown = false;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraScript == null) cameraScript = FindAnyObjectByType<SimpleThirdPersonCamera>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (characterModel == null && animator != null) characterModel = animator.transform;
            if (mainCamera == null) mainCamera = Camera.main;

            if (cooldownImage != null) cooldownImage.fillAmount = 0f;
            if (dashButton != null) dashButton.onClick.AddListener(PerformDash);
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
        }

        public void PerformDash()
        {
            if (isDashing || isDashCooldown) return;
            StartCoroutine(DashRoutine());
        }

        private IEnumerator DashRoutine()
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

                if (characterModel != null)
                    characterModel.rotation = Quaternion.LookRotation(dashDir);
            }

            // Запускаем кинематографичный эффект камеры (только если не Hard Lock)
            if (cameraScript != null)
            {
                cameraScript.TriggerDashCam(dashDir, dashDuration);
            }

            float startTime = Time.time;

            while (Time.time < startTime + dashDuration)
            {
                controller.Move(dashDir * dashSpeed * Time.deltaTime);
                yield return null;
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