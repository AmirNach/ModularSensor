using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Needs a SensorWithCamera. Without a camera it never passes.</summary>
    [Serializable, ConditionMenu("Spatial/In viewport")]
    public class InViewPort : Condition
    {
        [Tooltip("Viewport coordinates, 0 to 1.")]
        public Vector2 TopLeftCorner = new Vector2(0f, 1f);
        public Vector2 BottomRightCorner = new Vector2(1f, 0f);

        public override bool RequiresCamera => true;
        public override string Summary => $"X {TopLeftCorner.x:0.##}–{BottomRightCorner.x:0.##}, Y {BottomRightCorner.y:0.##}–{TopLeftCorner.y:0.##}";

        public override bool Evaluate(IDetectable target, in SensorContext context)
        {
            if (context.Camera == null) return false;

            Vector3 point = context.Camera.WorldToViewportPoint(target.GetCenterPosition());
            return point.z > 0f
                && point.x >= TopLeftCorner.x && point.x <= BottomRightCorner.x
                && point.y >= BottomRightCorner.y && point.y <= TopLeftCorner.y;
        }
    }
}
