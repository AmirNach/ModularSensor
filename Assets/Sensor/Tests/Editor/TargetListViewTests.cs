using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem.Tests
{
    /// <summary>The list view without a window, so these also run in batch mode.</summary>
    public class TargetListViewTests
    {
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

        private const string _key = "Test";

        private MemoryUiStateStore _store;

        [SetUp]
        public void SetUp()
        {
            // A fresh store per test, so nothing leaks between tests or into the Editor.
            _store = new MemoryUiStateStore();
        }

        private static List<TargetRow> MakeRows(int passed, int failed, int injected)
        {
            var rows = new List<TargetRow>();
            for (int i = 1; i <= passed; i++) rows.Add(MakeRow($"Target {i}", TargetState.Passed));
            for (int i = 1; i <= failed; i++) rows.Add(MakeRow($"Crate {i}", TargetState.Failed));
            for (int i = 1; i <= injected; i++) rows.Add(MakeRow($"Fake {i}", TargetState.Injected));
            return rows;
        }

        private static TargetRow MakeRow(string name, TargetState state)
        {
            return new TargetRow(new Stub { Id = name }, name, state, 1f, "1 m");
        }

        [Test]
        public void ShowsThreeGroupsWithTheirCounts()
        {
            var view = new TargetListView(_key, _store);
            view.SetRows(MakeRows(3, 5, 2));

            Assert.AreEqual("Passed (3)", view.GetTitle(TargetState.Passed));
            Assert.AreEqual("Failed (5)", view.GetTitle(TargetState.Failed));
            Assert.AreEqual("Injected (2)", view.GetTitle(TargetState.Injected));
        }

        [Test]
        public void Filter_NarrowsEveryGroupAndShowsTheShare()
        {
            var view = new TargetListView(_key, _store);
            view.SetRows(MakeRows(12, 40, 2));

            view.FilterText = "target 1";

            Assert.AreEqual("Passed (4 of 12)", view.GetTitle(TargetState.Passed));
            Assert.AreEqual("Failed (0 of 40)", view.GetTitle(TargetState.Failed));
            CollectionAssert.AreEqual(new[] { "Target 1", "Target 10", "Target 11", "Target 12" }, Names(view.GetRows(TargetState.Passed)));

            view.FilterText = string.Empty;
            Assert.AreEqual("Passed (12)", view.GetTitle(TargetState.Passed));
        }

        [Test]
        public void Filter_AppliesToRowsThatArriveLater()
        {
            var view = new TargetListView(_key, _store);
            view.FilterText = "crate";

            view.SetRows(MakeRows(2, 6, 0));

            Assert.AreEqual("Failed (6 of 6)", view.GetTitle(TargetState.Failed));
            Assert.AreEqual("Passed (0 of 2)", view.GetTitle(TargetState.Passed));
        }

        [Test]
        public void ExpandAndCollapseAll_OpenOrCloseEveryGroup()
        {
            var view = new TargetListView(_key, _store);

            view.ExpandAll(false);
            foreach (TargetState state in System.Enum.GetValues(typeof(TargetState)))
            {
                Assert.IsFalse(view.IsExpanded(state), state.ToString());
            }

            view.ExpandAll(true);
            foreach (TargetState state in System.Enum.GetValues(typeof(TargetState)))
            {
                Assert.IsTrue(view.IsExpanded(state), state.ToString());
            }
        }

        [Test]
        public void FoldStateAndFilter_SurviveRebuildingTheView()
        {
            var first = new TargetListView(_key, _store);
            first.ExpandAll(false);
            first.FilterText = "crate";

            var second = new TargetListView(_key, _store);

            Assert.IsFalse(second.IsExpanded(TargetState.Failed));
            Assert.AreEqual("crate", second.FilterText);
        }

        [Test]
        public void GroupWithNoRows_StaysVisibleAndSaysNone()
        {
            var view = new TargetListView(_key, _store);
            view.SetRows(MakeRows(1, 0, 0));

            Assert.AreEqual("Failed (0)", view.GetTitle(TargetState.Failed));
            Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.None, view.GetList(TargetState.Failed).style.display.value);
        }

        [Test]
        public void SetSelected_HighlightsTheRowWithoutRaisingAPick()
        {
            var view = new TargetListView(_key, _store);
            List<TargetRow> rows = MakeRows(3, 0, 0);
            int picks = 0;
            view.TargetPicked += _ => picks++;
            view.SetRows(rows);

            view.SetSelected(rows[1].Target);

            Assert.AreSame(rows[1].Target, view.SelectedTarget);
            CollectionAssert.AreEqual(new[] { 1 }, view.GetList(TargetState.Passed).selectedIndices);
            Assert.AreEqual(0, picks);
            Assert.IsEmpty(view.GetList(TargetState.Failed).selectedIndices);
        }

        [Test]
        public void ASelectedTargetHiddenByTheFilterStaysSelected()
        {
            var view = new TargetListView(_key, _store);
            List<TargetRow> rows = MakeRows(3, 0, 0);
            view.SetRows(rows);
            view.SetSelected(rows[0].Target);

            view.FilterText = "target 3";

            Assert.AreSame(rows[0].Target, view.SelectedTarget);
            Assert.IsEmpty(view.GetList(TargetState.Passed).selectedIndices);
        }

        [Test]
        public void AShortListHasNoScrollbar_AndALongOneKeepsIt()
        {
            var view = new TargetListView(_key, _store);

            view.SetRows(MakeRows(3, 0, 0));
            Assert.AreEqual(ScrollerVisibility.Hidden, view.GetList(TargetState.Passed).Q<ScrollView>().verticalScrollerVisibility);

            view.SetRows(MakeRows(TargetListView.MaxVisibleRows, 0, 0));
            Assert.AreEqual(ScrollerVisibility.Hidden, view.GetList(TargetState.Passed).Q<ScrollView>().verticalScrollerVisibility,
                "A list of exactly the visible number of rows fits.");

            view.SetRows(MakeRows(TargetListView.MaxVisibleRows + 1, 0, 0));
            Assert.AreEqual(ScrollerVisibility.Auto, view.GetList(TargetState.Passed).Q<ScrollView>().verticalScrollerVisibility);

            view.SetRows(MakeRows(2, 0, 0));
            Assert.AreEqual(ScrollerVisibility.Hidden, view.GetList(TargetState.Passed).Q<ScrollView>().verticalScrollerVisibility,
                "It should go away again when the list shrinks.");
        }

        [Test]
        public void TheListIsTallEnoughForEveryRowItShows()
        {
            var view = new TargetListView(_key, _store);
            view.SetRows(MakeRows(3, 0, 0));

            float height = view.GetList(TargetState.Passed).style.height.value.value;

            Assert.GreaterOrEqual(height, 3 * TargetListView.RowHeight + 4f, "Room for the rows plus the list's own border.");
        }

        [Test]
        public void ThousandsOfRows_StayQuickAndTheListHeightIsCapped()
        {
            var view = new TargetListView(_key, _store);
            List<TargetRow> rows = MakeRows(0, 3000, 0);

            var timer = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                view.SetRows(rows);
            }
            timer.Stop();

            Assert.AreEqual(3000, view.GetRows(TargetState.Failed).Count);
            float expectedHeight = TargetListView.MaxVisibleRows * TargetListView.RowHeight;
            float height = view.GetList(TargetState.Failed).style.height.value.value;
            Assert.GreaterOrEqual(height, expectedHeight, "Room for the visible rows.");
            Assert.LessOrEqual(height, expectedHeight + 12f, "No taller than the visible rows plus the list's border.");
            Assert.Less(timer.ElapsedMilliseconds, 2000, "Five refreshes of 3000 rows should be far below this.");
        }

        private static List<string> Names(IReadOnlyList<TargetRow> rows)
        {
            var names = new List<string>();
            foreach (TargetRow row in rows) names.Add(row.Name);
            return names;
        }
    }
}
