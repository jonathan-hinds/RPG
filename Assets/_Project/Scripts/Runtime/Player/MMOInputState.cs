using UnityEngine;

namespace RPGClone.Player
{
    public readonly struct MMOInputState
    {
        public MMOInputState(
            float forward,
            float strafe,
            float turn,
            bool jumpPressed,
            bool leftMouseHeld,
            bool rightMouseHeld,
            Vector2 mouseDelta,
            float zoomDelta)
        {
            Forward = Mathf.Clamp(forward, -1f, 1f);
            Strafe = Mathf.Clamp(strafe, -1f, 1f);
            Turn = Mathf.Clamp(turn, -1f, 1f);
            JumpPressed = jumpPressed;
            LeftMouseHeld = leftMouseHeld;
            RightMouseHeld = rightMouseHeld;
            MouseDelta = mouseDelta;
            ZoomDelta = zoomDelta;
        }

        public float Forward { get; }
        public float Strafe { get; }
        public float Turn { get; }
        public bool JumpPressed { get; }
        public bool LeftMouseHeld { get; }
        public bool RightMouseHeld { get; }
        public bool BothMouseButtonsHeld => LeftMouseHeld && RightMouseHeld;
        public Vector2 MouseDelta { get; }
        public float ZoomDelta { get; }
        public bool IsMouseLooking => LeftMouseHeld || RightMouseHeld;
    }
}
