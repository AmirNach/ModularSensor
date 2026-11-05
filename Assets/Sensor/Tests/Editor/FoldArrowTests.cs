using NUnit.Framework;
using UnityEngine.UIElements;

namespace SensorSystem.Tests
{
    public class FoldArrowTests
    {
        [Test]
        public void OpenAndClosed_UseTheSameGlyph_SoTheArrowNeverChangesSize()
        {
            var arrow = new FoldArrow();

            arrow.SetState(true, true);
            string open = arrow.text;
            arrow.SetState(true, false);
            string closed = arrow.text;

            Assert.AreEqual(FoldArrow.Glyph, open);
            Assert.AreEqual(open, closed);
        }

        [Test]
        public void Closed_TurnsTheArrowSideways_ThroughOneClass()
        {
            var arrow = new FoldArrow();

            arrow.SetState(true, false);
            Assert.IsTrue(arrow.ClassListContains(FoldArrow.CollapsedClass));
            Assert.IsFalse(arrow.IsExpanded);

            arrow.SetState(true, true);
            Assert.IsFalse(arrow.ClassListContains(FoldArrow.CollapsedClass));
            Assert.IsTrue(arrow.IsExpanded);
        }

        [Test]
        public void WithNothingToShow_TheArrowIsHiddenButStillTakesItsSpace()
        {
            var arrow = new FoldArrow();

            arrow.SetState(false, true);
            Assert.AreEqual(Visibility.Hidden, arrow.style.visibility.value);

            arrow.SetState(true, true);
            Assert.AreEqual(Visibility.Visible, arrow.style.visibility.value);
        }

        [Test]
        public void ACardKeepsTheSameArrowGlyphWhenOpenedAndClosed()
        {
            var parameters = ParameterEditor.Build(new IsMoving());
            var card = new NodeCard("Is Moving", string.Empty, "sensor-card--leaf", () => null, parameters, false);

            string closed = card.Arrow.text;
            card.SetExpanded(true);
            string open = card.Arrow.text;

            Assert.AreEqual(closed, open);
            Assert.IsFalse(card.Arrow.ClassListContains(FoldArrow.CollapsedClass));

            card.SetExpanded(false);
            Assert.IsTrue(card.Arrow.ClassListContains(FoldArrow.CollapsedClass));
        }

        [Test]
        public void ACardWithNoValuesAndNoChildren_HidesItsArrow()
        {
            var card = new NodeCard("Nothing", string.Empty, "sensor-card--leaf", () => null, ParameterEditor.Build(new And()), false);

            Assert.AreEqual(Visibility.Hidden, card.Arrow.style.visibility.value);
        }
    }
}
