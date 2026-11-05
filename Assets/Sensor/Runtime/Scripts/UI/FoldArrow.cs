using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>
    /// The arrow in front of a collapsible card, in the Editor and in the overlay. It is one glyph that is
    /// turned sideways when closed, so it keeps the same size whether the card is open or closed.
    /// </summary>
    public sealed class FoldArrow : Button
    {
        public const string Glyph = "▼";
        public const string CollapsedClass = "sensor-arrow--collapsed";

        public bool IsExpanded { get; private set; } = true;

        public FoldArrow()
        {
            text = Glyph;
            tooltip = "Expand or collapse";
            focusable = false;
            AddToClassList("sensor-arrow");
        }

        /// <summary>Hidden when the card has nothing to show, which keeps the titles of all cards lined up.</summary>
        public void SetState(bool hasContent, bool expanded)
        {
            IsExpanded = expanded;
            style.visibility = hasContent ? Visibility.Visible : Visibility.Hidden;
            EnableInClassList(CollapsedClass, !expanded);
        }
    }
}
