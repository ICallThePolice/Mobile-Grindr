using UnityEngine;
using UnityEngine.InputSystem;

namespace SpellSystem.Core
{
    public class MobileInputReader : MonoBehaviour
    {
        public Vector2 MoveInput { get; private set; }
        public Vector2 LookInput { get; private set; }
        public bool JumpInput { get; private set; } // Новая переменная

        private void Update()
        {
            MoveInput = GetUniversalMoveInput();
            LookInput = GetUniversalLookInput();

            // Читаем пробел (только в момент нажатия)
            JumpInput = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
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
    }
}