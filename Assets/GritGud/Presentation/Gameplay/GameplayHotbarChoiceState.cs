using UnityEngine;

namespace GritGud.Presentation.Gameplay
{
    internal sealed class GameplayHotbarChoiceState
    {
        public bool IsOpen { get; private set; }

        public int SlotNumber { get; private set; }

        public Rect Rectangle { get; private set; }

        public bool Contains(Vector2 point) =>
            IsOpen && Rectangle.Contains(point);

        public void Open(int slotNumber, Rect slotRectangle, float height)
        {
            SlotNumber = slotNumber;
            IsOpen = true;
            Rectangle = new Rect(
                slotRectangle.x,
                Mathf.Max(12f, slotRectangle.y - height - 7f),
                250f,
                height);
        }

        public void ClampToCanvas(float canvasWidth, float canvasHeight)
        {
            if (!IsOpen)
            {
                return;
            }

            Rect rectangle = Rectangle;
            rectangle.x = Mathf.Clamp(
                rectangle.x,
                10f,
                Mathf.Max(10f, canvasWidth - rectangle.width - 10f));
            rectangle.y = Mathf.Clamp(
                rectangle.y,
                10f,
                Mathf.Max(10f, canvasHeight - rectangle.height - 10f));
            Rectangle = rectangle;
        }

        public void Close()
        {
            IsOpen = false;
            SlotNumber = 0;
            Rectangle = default;
        }
    }
}
