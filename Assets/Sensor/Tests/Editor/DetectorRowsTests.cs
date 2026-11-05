using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class DetectorRowsTests
    {
        private class Stub : IDetectable
        {
            public string Id;
            public Vector3 Position;
            public Bounds Bounds { get; set; }
            public bool IsFake => false;
            public string GetIdentifier() => Id;
            public Vector3 GetCenterPosition() => Position;
            public string GetDetectionType() => "Stub";
            public float GetVelocity() => 0f;
        }

        private static readonly IReadOnlyDictionary<string, int> NoIds = new Dictionary<string, int>();

        private static Stub Target(string id, float x = 10f) => new Stub { Id = id, Position = new Vector3(x, 0f, 0f) };

        private static SensorSnapshot Sensor(string name, IEnumerable<IDetectable> candidates, IEnumerable<IDetectable> detected, IEnumerable<IDetectable> injected = null, float x = 0f)
        {
            return new SensorSnapshot(
                name,
                new Vector3(x, 0f, 0f),
                new HashSet<IDetectable>(candidates),
                new HashSet<IDetectable>(injected ?? new IDetectable[0]),
                new HashSet<IDetectable>(detected));
        }

        private static TargetRow RowFor(List<TargetRow> rows, IDetectable target)
        {
            return rows.Single(row => ReferenceEquals(row.Target, target));
        }

        [Test]
        public void SeenByTwoSensors_OneDetectsIt_IsPassedAndNamesTheDetectingSensor()
        {
            Stub target = Target("t");
            var sensors = new[]
            {
                Sensor("A", new[] { target }, new[] { target }),
                Sensor("B", new[] { target }, new IDetectable[0]),
            };

            List<TargetRow> rows = TargetRows.ForDetector(sensors, NoIds);

            Assert.AreEqual(1, rows.Count, "One row per object, however many sensors look at it.");
            Assert.AreEqual(TargetState.Passed, rows[0].State);
            StringAssert.Contains("A", rows[0].Detail);
            StringAssert.DoesNotContain("B", rows[0].Detail);
        }

        [Test]
        public void SeenButDetectedByNone_IsFailed()
        {
            Stub target = Target("t");
            List<TargetRow> rows = TargetRows.ForDetector(new[]
            {
                Sensor("A", new[] { target }, new IDetectable[0]),
                Sensor("B", new[] { target }, new IDetectable[0]),
            }, NoIds);

            Assert.AreEqual(TargetState.Failed, rows[0].State);
        }

        [Test]
        public void DetectedOnlyBecauseOfInjectors_IsInjected()
        {
            Stub fake = Target("fake");
            List<TargetRow> rows = TargetRows.ForDetector(new[]
            {
                Sensor("A", new IDetectable[0], new[] { fake }, new[] { fake }),
            }, NoIds);

            Assert.AreEqual(TargetState.Injected, rows[0].State);
        }

        [Test]
        public void InjectedByOneSensorButPassedByAnother_IsPassed()
        {
            Stub target = Target("t");
            List<TargetRow> rows = TargetRows.ForDetector(new[]
            {
                Sensor("A", new IDetectable[0], new[] { target }, new[] { target }),
                Sensor("B", new[] { target }, new[] { target }),
            }, NoIds);

            Assert.AreEqual(TargetState.Passed, rows[0].State);
        }

        [Test]
        public void EveryObjectAnySensorLooksAtGetsARow()
        {
            Stub onlyA = Target("a");
            Stub onlyB = Target("b");
            Stub both = Target("both");
            List<TargetRow> rows = TargetRows.ForDetector(new[]
            {
                Sensor("A", new IDetectable[] { onlyA, both }, new IDetectable[0]),
                Sensor("B", new IDetectable[] { onlyB, both }, new IDetectable[0]),
            }, NoIds);

            Assert.AreEqual(3, rows.Count);
            Assert.IsNotNull(RowFor(rows, onlyA));
            Assert.IsNotNull(RowFor(rows, onlyB));
        }

        [Test]
        public void RowDetail_ShowsTheDetectionIdAndTheNearestSensorDistance()
        {
            Stub target = Target("t", 10f);
            var ids = new Dictionary<string, int> { { "t", 42 } };

            List<TargetRow> rows = TargetRows.ForDetector(new[]
            {
                Sensor("Far", new[] { target }, new IDetectable[0], null, -30f),
                Sensor("Near", new[] { target }, new[] { target }, null, 7f),
            }, ids);

            Assert.AreEqual("#42", rows[0].DetectionId);
            Assert.AreEqual("t", rows[0].Identifier);
            Assert.AreEqual("3 m", rows[0].DistanceText);
            Assert.AreEqual(3f, rows[0].Distance, 0.001f);
        }

        [Test]
        public void Distance_IsTheOneMeasuredAtTheSensorsLastRefresh_NotTheCurrentPosition()
        {
            Stub target = Target("t", 10f);
            var measured = new Dictionary<IDetectable, float> { { target, 4f } };
            var sensor = new SensorSnapshot("A", Vector3.zero, new HashSet<IDetectable> { target }, new HashSet<IDetectable>(), new HashSet<IDetectable> { target }, measured);

            target.Position = new Vector3(50f, 0f, 0f); // moves after the refresh

            List<TargetRow> rows = TargetRows.ForDetector(new[] { sensor }, NoIds);

            Assert.AreEqual(4f, rows[0].Distance, 0.001f, "The distance must not change until the sensor refreshes again.");
        }

        [Test]
        public void LongIdentifiers_AreKeptWhole()
        {
            Stub target = Target("1234:5678:9012:3456");
            List<TargetRow> rows = TargetRows.ForDetector(new[] { Sensor("A", new[] { target }, new[] { target }) }, NoIds);

            Assert.AreEqual("1234:5678:9012:3456", rows[0].Identifier);
        }

        [Test]
        public void GetDetectingSensors_ListsOnlyThoseThatDetectTheTarget()
        {
            Stub target = Target("t");
            var sensors = new[]
            {
                Sensor("A", new[] { target }, new[] { target }),
                Sensor("B", new[] { target }, new IDetectable[0]),
                Sensor("C", new[] { target }, new[] { target }),
            };

            CollectionAssert.AreEqual(new[] { "A", "C" }, TargetRows.GetDetectingSensors(target, sensors));
        }
    }
}
