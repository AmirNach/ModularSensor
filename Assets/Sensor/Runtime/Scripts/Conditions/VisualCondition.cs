using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Base for conditions that can draw what they did for each target, such as the raycast line.
    /// Each one has its own "Show visual" switch, off by default, so only the conditions you are debugging draw,
    /// and its own color, so you can tell them apart.
    /// In Evaluate, draw through VisualsFor(context), which is null when the switch is off, and color shapes with ColorOf(passed).
    /// </summary>
    [Serializable]
    public abstract class VisualCondition : Condition
    {
        /// <summary>How much fainter a visual is when the object failed the condition.</summary>
        public const float FailedAlpha = 0.35f;

        [Tooltip("Debug: draw what this condition did for each object. Shown as gizmos, so the Game view needs its Gizmos button on. With an object picked in the QA overlay, only that object is drawn.")]
        public bool ShowVisual;

        [Tooltip("The color of this condition's visual. Objects that failed the condition are drawn fainter.")]
        [ShowIf(nameof(ShowVisual))]
        public Color VisualColor = Color.white;

        public override bool WantsVisuals => ShowVisual;

        /// <summary>Where this condition should draw, or null when its switch is off.</summary>
        protected ConditionVisuals VisualsFor(in SensorContext context) => ShowVisual ? context.Visuals : null;

        /// <summary>The visual's color for an object that passed, or the same color fainter for one that failed.</summary>
        protected Color ColorOf(bool passed) => passed ? VisualColor : Faint(VisualColor);

        /// <summary>The color gizmos of this condition are drawn in.</summary>
        public Color GizmoColor => VisualColor;

        public static Color Faint(Color color)
        {
            color.a *= FailedAlpha;
            return color;
        }
    }
}
