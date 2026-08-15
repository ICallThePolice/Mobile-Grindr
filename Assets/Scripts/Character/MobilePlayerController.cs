using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSystem.Core
{
    [RequireComponent(typeof(CharacterController))]
    public class MobilePlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float turnSpeed = 15f;

        [Header("References")]
        [SerializeField] private SimpleThirdPersonCamera cameraScript;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform characterModel;
        [SerializeField] private Camera mainCamera;

        private CharacterController controller;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (cameraScript == null) cameraScript = FindAnyObjectByType<SimpleThirdPersonCamera>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (characterModel == null && animator != null) characterModel = animator.transform;
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Update()
        {
            Vector2 moveInput = GetUniversalMoveInput();
            Vector2 lookInput = GetUniversalLookInput();

            Move(moveInput);

            if (cameraScript != null)
            {
                cameraScript.SetLookInput(lookInput);
            }
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
            {
                input = Mouse.current.delta.ReadValue() * 0.05f;
            }
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

            // 1. Векторы движения от камеры
            Vector3 camForward = mainCamera.transform.forward;
            Vector3 camRight = mainCamera.transform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();

            // 2. Двигаем капсулу
            Vector3 moveDir = camRight * input.x + camForward * input.y;
            controller.Move(moveDir * moveSpeed * Time.deltaTime);

            if (animator != null)
            {
                animator.SetFloat("Speed", input.magnitude);
            }

            // 3. БОЕВАЯ СТОЙКА И РАЗВОРОТ МЕША
            Transform activeTarget = cameraScript != null ? cameraScript.CurrentTarget : null;

            if (activeTarget != null && characterModel != null)
            {
                // В БОЮ: Персонаж всегда смотрит лицом на врага (стрейфится), независимо от того, куда бежит
                Vector3 dirToEnemy = activeTarget.position - transform.position;
                dirToEnemy.y = 0;

                if (dirToEnemy != Vector3.zero)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(dirToEnemy);
                    characterModel.rotation = Quaternion.Slerp(characterModel.rotation, targetRotation, turnSpeed * Time.deltaTime);
                }
            }
            else if (moveDir.sqrMagnitude > 0.01f && characterModel != null)
            {
                // ВНЕ БОЯ: Обычный бег лицом вперед
                Quaternion targetRotation = Quaternion.LookRotation(moveDir);
                characterModel.rotation = Quaternion.Slerp(characterModel.rotation, targetRotation, turnSpeed * Time.deltaTime);
            }
        }
    }
}