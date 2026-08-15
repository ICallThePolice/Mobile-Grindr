using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSystem.Core
{
    public class MobilePlayerController : MonoBehaviour
    {
        [Header("Movement (Left Stick)")]
        [SerializeField] private float moveSpeed = 5f;

        [Header("Look (Right Stick)")]
        [SerializeField] private float lookSensitivity = 150f;
        [SerializeField] private Transform cameraTransform;

        private float xRotation = 0f;

        private void Start()
        {
            if (cameraTransform == null)
            {
                cameraTransform = GetComponentInChildren<Camera>().transform;
            }
        }

        private void Update()
        {
            // Если виртуальный или реальный геймпад не найден, выходим
            if (Gamepad.current == null) return;

            // Читаем значения с наших настроенных On-Screen Sticks
            Vector2 moveInput = Gamepad.current.leftStick.ReadValue();
            Vector2 lookInput = Gamepad.current.rightStick.ReadValue();

            Move(moveInput);
            Look(lookInput);
        }

        private void Move(Vector2 input)
        {
            // Движение относительно поворота игрока
            Vector3 move = transform.right * input.x + transform.forward * input.y;
            transform.position += move * moveSpeed * Time.deltaTime;
        }

        private void Look(Vector2 input)
        {
            float mouseX = input.x * lookSensitivity * Time.deltaTime;
            float mouseY = input.y * lookSensitivity * Time.deltaTime;

            // Вращение камеры вверх/вниз (по оси X)
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            }

            // Вращение всего тела игрока влево/вправо (по оси Y)
            transform.Rotate(Vector3.up * mouseX);
        }
    }
}