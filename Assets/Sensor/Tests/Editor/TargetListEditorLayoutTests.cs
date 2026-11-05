using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SensorSystem.Tests
{
    /// <summary>Measures the real editor layout, which a plain unit test cannot see.</summary>
    public class TargetListEditorLayoutTests
    {
        private class HostWindow : EditorWindow
        {
            public TargetListView View;

            private void CreateGUI()
            {
                View = new TargetListView("LayoutTest", new MemoryUiStateStore());
                rootVisualElement.Add(View);
            }
        }

        private class Stub : IDetectable
        {
            public string Id;
            public Bounds Bounds { get; set; }
            public bool IsFake => false;
            public string GetIdentifier() => Id;
            public Vector3 GetCenterPosition() => Vector3.zero;
            public string GetDetectionType() => "Stub";
            public float GetVelocity() => 0f;
        }

        private HostWindow _window;

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
        }

        private IEnumerator OpenWindow()
        {
            _window = EditorWindow.CreateInstance<HostWindow>();
            _window.position = new Rect(100, 100, 600, 500);
            _window.Show();

            // The view is created a step after the window opens, and layout takes a couple more.
            for (int i = 0; i < 6; i++)
            {
                yield return null;
            }

            var rows = new List<TargetRow>();
            for (int i = 1; i <= 3; i++) rows.Add(new TargetRow(new Stub { Id = "p" + i }, "Target " + i, TargetState.Passed, i, "1 m"));
            for (int i = 1; i <= 2; i++) rows.Add(new TargetRow(new Stub { Id = "f" + i }, "Crate " + i, TargetState.Failed, i, "1 m"));
            _window.View.SetRows(rows);

            for (int i = 0; i < 6; i++)
            {
                yield return null;
            }
        }

        private List<CollapsibleGroup> Groups()
        {
            return _window.rootVisualElement.Query<CollapsibleGroup>(className: "sensor-target-group").ToList();
        }

        [UnityTest]
        public IEnumerator TheGroupArrows_SitClearOfTheColoredBar()
        {
            yield return OpenWindow();

            List<CollapsibleGroup> groups = Groups();
            Assert.AreEqual(3, groups.Count);

            foreach (CollapsibleGroup group in groups)
            {
                float barRight = group.worldBound.xMin + group.resolvedStyle.borderLeftWidth;
                float gap = group.Arrow.worldBound.xMin - barRight;
                Debug.Log($"LAYOUT {group.text}: bar right edge {barRight:0.0}, arrow {group.Arrow.worldBound.xMin:0.0} to {group.Arrow.worldBound.xMax:0.0}, gap {gap:0.0}");

                Assert.GreaterOrEqual(gap, 4f, $"The arrow of \"{group.text}\" is too close to the colored bar (gap {gap:0.0}px).");
            }
        }

        [UnityTest]
        public IEnumerator TheTitleComesAfterTheArrow_AndTheListIndentsUnderTheTitle()
        {
            yield return OpenWindow();

            CollapsibleGroup passed = Groups().First();
            Label title = passed.Q<Label>(className: "sensor-group-title");

            Assert.GreaterOrEqual(title.worldBound.xMin, passed.Arrow.worldBound.xMax, "The title should come after the arrow.");

            VisualElement list = passed.Q(className: "sensor-target-items");
            Assert.Greater(list.worldBound.xMin, passed.Arrow.worldBound.xMin, "The list should be indented in from the arrow, not flush with the bar.");
            Assert.LessOrEqual(list.worldBound.xMin, title.worldBound.xMin + 1f, "The list should not be indented further than the title.");
            Assert.LessOrEqual(passed.worldBound.xMax, _window.rootVisualElement.worldBound.xMax + 1f, "Nothing should spill out of the window.");
        }

        [UnityTest]
        public IEnumerator TheArrowKeepsItsSize_WhenAGroupIsClosedAndOpened()
        {
            yield return OpenWindow();

            CollapsibleGroup passed = Groups().First();
            Rect open = passed.Arrow.worldBound;

            passed.SetValue(false);
            yield return null;
            yield return null;
            Rect closed = passed.Arrow.worldBound;

            passed.SetValue(true);
            yield return null;
            yield return null;

            Assert.AreEqual(open.width, closed.width, 0.5f, "The arrow box must not change width.");
            Assert.AreEqual(open.height, closed.height, 0.5f, "The arrow box must not change height.");
            Assert.AreEqual(open.center.x, closed.center.x, 0.5f, "Turning the arrow must keep its center in place.");
            Assert.AreEqual(open.center.y, closed.center.y, 0.5f);
        }
    }
}
