using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class TargetListModelTests
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

        private static TargetRow Row(string name, TargetState state, float distance = 0f, string id = null)
        {
            return new TargetRow(new Stub { Id = id ?? name }, name, state, distance, string.Empty);
        }

        private static List<string> Names(List<TargetRow> rows) => rows.Select(row => row.Name).ToList();

        [TestCase("target 1", "Target 12", true)]
        [TestCase("1 TARGET", "Target 12", true)]
        [TestCase("tar", "Target 12", true)]
        [TestCase("target 3", "Target 12", false)]
        [TestCase("crate target", "Target 12", false)]
        [TestCase("", "Target 12", true)]
        [TestCase("   ", "Target 12", true)]
        public void Matches_NeedsEveryWordInAnyOrderIgnoringCase(string filter, string name, bool expected)
        {
            Assert.AreEqual(expected, TargetListModel.Matches(name, filter));
        }

        [Test]
        public void Build_PutsRowsInTheirGroupsAndCountsEverything()
        {
            TargetListModel.Groups groups = TargetListModel.Build(new[]
            {
                Row("A", TargetState.Passed),
                Row("B", TargetState.Failed),
                Row("C", TargetState.Failed),
                Row("D", TargetState.Injected),
            }, null);

            CollectionAssert.AreEqual(new[] { "A" }, Names(groups.Shown[(int)TargetState.Passed]));
            CollectionAssert.AreEqual(new[] { "B", "C" }, Names(groups.Shown[(int)TargetState.Failed]));
            CollectionAssert.AreEqual(new[] { "D" }, Names(groups.Shown[(int)TargetState.Injected]));
            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, groups.Totals);
        }

        [Test]
        public void Build_FilterChangesWhatIsShownButNotTheTotals()
        {
            TargetListModel.Groups groups = TargetListModel.Build(new[]
            {
                Row("Crate 1", TargetState.Failed),
                Row("Crate 2", TargetState.Failed),
                Row("Truck", TargetState.Failed),
                Row("Crate 3", TargetState.Passed),
            }, "crate");

            Assert.AreEqual(2, groups.Shown[(int)TargetState.Failed].Count);
            Assert.AreEqual(3, groups.Totals[(int)TargetState.Failed]);
            Assert.AreEqual(1, groups.Shown[(int)TargetState.Passed].Count);
        }

        [Test]
        public void Build_SortsNumbersInNamesByValueThenByDistance()
        {
            TargetListModel.Groups groups = TargetListModel.Build(new[]
            {
                Row("Target 10", TargetState.Failed),
                Row("Target 2", TargetState.Failed),
                Row("Target 1", TargetState.Failed),
                Row("same", TargetState.Failed, 30f, "far"),
                Row("same", TargetState.Failed, 5f, "near"),
            }, null);

            List<TargetRow> failed = groups.Shown[(int)TargetState.Failed];
            CollectionAssert.AreEqual(new[] { "same", "same", "Target 1", "Target 2", "Target 10" }, Names(failed));
            Assert.AreEqual(5f, failed[0].Distance, "The nearer one comes first.");
        }

        [TestCase("a2", "a10", -1)]
        [TestCase("a10", "a2", 1)]
        [TestCase("a02", "a2", 0)]
        [TestCase("A", "a", 0)]
        [TestCase("abc", "abd", -1)]
        [TestCase("a", "a1", -1)]
        public void NaturalCompare_ComparesDigitsAsNumbers(string a, string b, int expectedSign)
        {
            Assert.AreEqual(expectedSign, System.Math.Sign(TargetListModel.NaturalCompare(a, b)));
        }

        [Test]
        public void FormatCount_ShowsTheFilteredShareOnlyWhileFiltering()
        {
            Assert.AreEqual("Passed (12)", TargetListModel.FormatCount("Passed", 12, 12, false));
            Assert.AreEqual("Passed (3 of 12)", TargetListModel.FormatCount("Passed", 3, 12, true));
        }

        [Test]
        public void GetName_FakeDetectionsGetAShortIdSuffix()
        {
            var first = new FakeDetectionInstance(Vector3.zero, EntityType.Drone);
            var second = new FakeDetectionInstance(Vector3.zero, EntityType.Drone);

            string name = TargetListModel.GetName(first);

            StringAssert.StartsWith("Fake Drone ", name);
            Assert.AreEqual("Fake Drone ".Length + 4, name.Length);
            Assert.AreNotEqual(name, TargetListModel.GetName(second), "Two fakes need distinguishable names.");
        }

        [Test]
        public void GetName_UsesTheObjectNameForSceneObjects()
        {
            var crate = new GameObject("Crate 7");
            try
            {
                Assert.AreEqual("Crate 7", TargetListModel.GetName(crate.AddComponent<DetectableEntity>()));
            }
            finally
            {
                Object.DestroyImmediate(crate);
            }
        }
    }
}
