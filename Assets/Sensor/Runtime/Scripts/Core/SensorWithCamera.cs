using UnityEngine;

namespace SensorSystem
{
    /// <summary>A sensor that sees through a camera. InFrustum uses the camera's frustum, and viewport conditions work.</summary>
    public class SensorWithCamera : Sensor
    {
        [Header("Camera")]
        [SerializeField] private Camera _camera;

        protected override Camera ContextCamera => _camera;

        /// <summary>Sets the rectangle on this sensor's viewport conditions and fake injectors.</summary>
        public void SetViewPortPoints(Vector2 topLeft, Vector2 bottomRight)
        {
            foreach (Condition node in ConditionTree.Flatten(RuntimeRoot))
            {
                if (node is InViewPort viewPort)
                {
                    viewPort.TopLeftCorner = topLeft;
                    viewPort.BottomRightCorner = bottomRight;
                }
            }

            foreach (DetectionInjector injector in RuntimeInjectors)
            {
                if (injector is FakeRandomDetectionInViewPort fake)
                {
                    fake.TopLeftCorner = topLeft;
                    fake.BottomRightCorner = bottomRight;
                }
            }
        }
    }
}
