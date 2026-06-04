using UnityEngine;
using UnityEngine.InputSystem;

namespace RPGClone.Player
{
    public sealed class MMOInputReader : MonoBehaviour
    {
        [SerializeField] private bool lockCursorWhileMouseLooking;

        public MMOInputState Current { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool leftMouseHeld = mouse != null && mouse.leftButton.isPressed;
            bool rightMouseHeld = mouse != null && mouse.rightButton.isPressed;
            bool bothMouseButtonsHeld = leftMouseHeld && rightMouseHeld;

            float forward = PositiveNegative(keyboard, Key.W, Key.S);
            if (bothMouseButtonsHeld)
            {
                forward = Mathf.Max(forward, 1f);
            }

            float keyboardTurn = PositiveNegative(keyboard, Key.D, Key.A);
            float keyboardStrafe = PositiveNegative(keyboard, Key.E, Key.Q);

            if (rightMouseHeld)
            {
                keyboardStrafe = Mathf.Clamp(keyboardStrafe + keyboardTurn, -1f, 1f);
                keyboardTurn = 0f;
            }

            Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
            float zoomDelta = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            bool jumpPressed = keyboard != null && keyboard.spaceKey.wasPressedThisFrame;

            Current = new MMOInputState(
                forward,
                keyboardStrafe,
                keyboardTurn,
                jumpPressed,
                leftMouseHeld,
                rightMouseHeld,
                mouseDelta,
                zoomDelta);

            UpdateCursor(Current.RightMouseHeld);
        }

        public void SetLockCursorWhileMouseLooking(bool value)
        {
            lockCursorWhileMouseLooking = value;
            if (!value)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private static float PositiveNegative(Keyboard keyboard, Key positive, Key negative)
        {
            if (keyboard == null)
            {
                return 0f;
            }

            float value = 0f;
            if (keyboard[positive].isPressed)
            {
                value += 1f;
            }

            if (keyboard[negative].isPressed)
            {
                value -= 1f;
            }

            return value;
        }

        private void UpdateCursor(bool isMouseLooking)
        {
            if (!lockCursorWhileMouseLooking)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            Cursor.lockState = isMouseLooking ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isMouseLooking;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
