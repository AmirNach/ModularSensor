using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace SensorSystem.Tests
{
    public class CollapsibleGroupTests
    {
        [Test]
        public void ChildrenAddedToTheGroup_GoIntoItsContent()
        {
            var group = new CollapsibleGroup("Title", true);
            var child = new Label("inside");

            group.Add(child);

            // parent is the logical parent, the group. The real container is in the hierarchy.
            Assert.AreSame(group, child.parent);
            Assert.AreSame(group.contentContainer, child.hierarchy.parent);
            Assert.AreEqual("Title", group.text);
        }

        [Test]
        public void ClosingHidesTheContent_AndTheArrowTurns()
        {
            var group = new CollapsibleGroup("Title", true);
            group.Add(new Label("inside"));

            group.SetValue(false);

            Assert.IsFalse(group.value);
            Assert.AreEqual(DisplayStyle.None, group.contentContainer.style.display.value);
            Assert.IsTrue(group.Arrow.ClassListContains(FoldArrow.CollapsedClass));
            Assert.AreEqual(FoldArrow.Glyph, group.Arrow.text, "The arrow glyph must not change, only its direction.");

            group.SetValue(true);
            Assert.AreEqual(DisplayStyle.Flex, group.contentContainer.style.display.value);
            Assert.IsFalse(group.Arrow.ClassListContains(FoldArrow.CollapsedClass));
        }

        [Test]
        public void SetValue_RaisesTheEventOnlyWhenTheValueChanges_AndSetValueWithoutNotifyNever()
        {
            var group = new CollapsibleGroup("Title", true);
            var raised = new List<bool>();
            group.ValueChanged += raised.Add;

            group.SetValue(true);
            group.SetValue(false);
            group.SetValueWithoutNotify(true);
            group.SetValue(false);

            CollectionAssert.AreEqual(new[] { false, false }, raised);
        }

        [Test]
        public void StartsInTheStateItWasGiven()
        {
            Assert.IsFalse(new CollapsibleGroup("Closed", false).value);
            Assert.AreEqual(DisplayStyle.None, new CollapsibleGroup("Closed", false).contentContainer.style.display.value);
            Assert.IsTrue(new CollapsibleGroup("Open", true).value);
        }

        [Test]
        public void TheArrowIsAlwaysVisible_BecauseAGroupAlwaysHasSomethingToShow()
        {
            var group = new CollapsibleGroup("Title", false);

            Assert.AreEqual(Visibility.Visible, group.Arrow.style.visibility.value);
        }
    }
}
