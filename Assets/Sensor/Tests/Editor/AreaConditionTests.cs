using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class AreaConditionTests
    {
        private class Stub : IDetectable
        {
            public Vector3 Position;
            public Bounds Bounds { get; set; }
            public bool IsFake => false;
            public string GetIdentifier() => "stub";
            public Vector3 GetCenterPosition() => Position;
            public string GetDetectionType() => "Stub";
            public float GetVelocity() => 0f;
        }

        private GameObject _sensor;

        [SetUp]
        public void SetUp()
        {
            _sensor = new GameObject("sensor");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sensor);
            SensorAreas.Clear();
        }

        private bool Passes(Condition condition, Vector3 targetPosition)
        {
            var context = new SensorContext(_sensor.transform, null, 0f);
            condition.Prepare(context);
            return condition.Evaluate(new Stub { Position = targetPosition }, context);
        }

        [Test]
        public void Polygon_SquareContainsInsideAndNotOutside()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));

            Assert.IsTrue(Passes(area, new Vector3(5, 0, 5)));
            Assert.IsFalse(Passes(area, new Vector3(15, 0, 5)));
            Assert.IsFalse(Passes(area, new Vector3(5, 0, -1)));
        }

        [Test]
        public void Polygon_IgnoresHeight()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));

            Assert.IsTrue(Passes(area, new Vector3(5, 500, 5)));
            Assert.IsTrue(Passes(area, new Vector3(5, -500, 5)));
        }

        [Test]
        public void Polygon_Triangle()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10));

            Assert.IsTrue(Passes(area, new Vector3(2, 0, 2)));
            Assert.IsFalse(Passes(area, new Vector3(8, 0, 8)));
        }

        [Test]
        public void Polygon_ConcaveNotchIsOutside()
        {
            // A U shape: the notch between the arms is outside.
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(
                new Vector2(0, 0), new Vector2(9, 0), new Vector2(9, 9), new Vector2(6, 9),
                new Vector2(6, 3), new Vector2(3, 3), new Vector2(3, 9), new Vector2(0, 9));

            Assert.IsTrue(Passes(area, new Vector3(1.5f, 0, 7)));
            Assert.IsTrue(Passes(area, new Vector3(7.5f, 0, 7)));
            Assert.IsFalse(Passes(area, new Vector3(4.5f, 0, 7)));
        }

        [Test]
        public void Polygon_FewerThanThreePointsPassesNothing()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 10));

            Assert.IsFalse(Passes(area, new Vector3(5, 0, 5)));
        }

        [Test]
        public void Polygon_NewPointsAreUsedFromTheNextRefresh()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));
            Assert.IsTrue(Passes(area, new Vector3(5, 0, 5)));

            area.SetPolygon(new Vector2(20, 20), new Vector2(30, 20), new Vector2(30, 30), new Vector2(20, 30));
            Assert.IsFalse(Passes(area, new Vector3(5, 0, 5)));
            Assert.IsTrue(Passes(area, new Vector3(25, 0, 25)));
        }

        [Test]
        public void Polygon_RelativeToSensorMovesAndTurnsWithIt()
        {
            // A box 2 m wide from 5 to 10 m in front of the sensor.
            var area = new InPolygonArea { DefineInInspector = true, RelativeToSensor = true };
            area.SetPolygon(new Vector2(-1, 5), new Vector2(1, 5), new Vector2(1, 10), new Vector2(-1, 10));

            _sensor.transform.SetPositionAndRotation(new Vector3(100, 3, 0), Quaternion.identity);
            Assert.IsTrue(Passes(area, new Vector3(100, 0, 7)));
            Assert.IsFalse(Passes(area, new Vector3(0, 0, 7)));

            // Turned to face +X: the box is now along X.
            _sensor.transform.rotation = Quaternion.Euler(0, 90, 0);
            Assert.IsTrue(Passes(area, new Vector3(107, 0, 0)));
            Assert.IsFalse(Passes(area, new Vector3(100, 0, 7)));
        }

        [Test]
        public void Polygon_RelativeIgnoresPitch()
        {
            var area = new InPolygonArea { DefineInInspector = true, RelativeToSensor = true };
            area.SetPolygon(new Vector2(-1, 5), new Vector2(1, 5), new Vector2(1, 10), new Vector2(-1, 10));

            _sensor.transform.rotation = Quaternion.Euler(30, 0, 0);
            Assert.IsTrue(Passes(area, new Vector3(0, 0, 7)));
        }

        [Test]
        public void Circle_ContainsInsideAndEdge()
        {
            var area = new InCircleArea { DefineInInspector = true };
            area.SetCircle(new Vector2(10, 10), 5f);

            Assert.IsTrue(Passes(area, new Vector3(10, 0, 10)));
            Assert.IsTrue(Passes(area, new Vector3(15, 0, 10)));
            Assert.IsFalse(Passes(area, new Vector3(15.1f, 0, 10)));
            Assert.IsTrue(Passes(area, new Vector3(10, 999, 13)));
        }

        [Test]
        public void Circle_RelativeToSensor()
        {
            var area = new InCircleArea { DefineInInspector = true, RelativeToSensor = true };
            area.SetCircle(new Vector2(0, 10), 2f);

            _sensor.transform.SetPositionAndRotation(new Vector3(50, 0, 50), Quaternion.Euler(0, 180, 0));
            Assert.IsTrue(Passes(area, new Vector3(50, 0, 40)));
            Assert.IsFalse(Passes(area, new Vector3(50, 0, 60)));
        }

        [Test]
        public void Defaults_AreWorldPointsSetFromCode()
        {
            Assert.IsFalse(new InPolygonArea().RelativeToSensor);
            Assert.IsFalse(new InCircleArea().RelativeToSensor);
            Assert.IsFalse(new InPolygonArea().DefineInInspector);
            Assert.IsFalse(new InCircleArea().DefineInInspector);
        }

        [Test]
        public void Polygon_FourPointsInTheInspectorThenThreeFromCode()
        {
            // A square from the inspector: (8, 8) is inside it.
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));
            Assert.IsTrue(Passes(area, new Vector3(8, 0, 8)));

            // Changed later to a triangle that cuts that corner off.
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10));
            Assert.IsFalse(Passes(area, new Vector3(8, 0, 8)));
            Assert.IsTrue(Passes(area, new Vector3(2, 0, 2)));

            // And back to more points.
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 5), new Vector2(10, 10), new Vector2(0, 10));
            Assert.IsTrue(Passes(area, new Vector3(8, 0, 8)));
        }

        [Test]
        public void Polygon_FromCode_UsesTheSharedPointsByName()
        {
            var area = new InPolygonArea { DefineInInspector = false, AreaName = "Zone A" };
            Assert.IsFalse(Passes(area, new Vector3(5, 0, 5)), "Nothing set yet, so nothing passes.");

            SensorAreas.SetPolygon("Zone A", new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));
            Assert.IsTrue(Passes(area, new Vector3(8, 0, 8)));

            SensorAreas.SetPolygon("Zone A", new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10));
            Assert.IsFalse(Passes(area, new Vector3(8, 0, 8)));
            Assert.IsTrue(Passes(area, new Vector3(2, 0, 2)));

            SensorAreas.Remove("Zone A");
            Assert.IsFalse(Passes(area, new Vector3(2, 0, 2)));
        }

        [Test]
        public void Polygon_FromCode_IgnoresTheInspectorPoints()
        {
            var area = new InPolygonArea { DefineInInspector = false, AreaName = "Zone A" };
            area.SetPolygon(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));
            SensorAreas.SetPolygon("Zone A", new Vector2(20, 20), new Vector2(30, 20), new Vector2(30, 30));

            Assert.IsFalse(Passes(area, new Vector3(5, 0, 5)));
            Assert.IsTrue(Passes(area, new Vector3(28, 0, 22)));
        }

        [Test]
        public void Polygon_FromCode_SharedByEveryConditionWithTheName()
        {
            var first = new InPolygonArea { DefineInInspector = false, AreaName = "Zone A" };
            var second = new InPolygonArea { DefineInInspector = false, AreaName = "Zone A" };
            SensorAreas.SetPolygon("Zone A", new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10));

            Assert.IsTrue(Passes(first, new Vector3(5, 0, 5)));
            Assert.IsTrue(Passes(second, new Vector3(5, 0, 5)));
        }

        [Test]
        public void Polygon_FromCode_ChangingTheCallersListLaterDoesNothing()
        {
            var points = new System.Collections.Generic.List<Vector2> { new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 10) };
            SensorAreas.SetPolygon("Zone A", points);
            points.Clear();

            var area = new InPolygonArea { DefineInInspector = false, AreaName = "Zone A" };
            Assert.IsTrue(Passes(area, new Vector3(2, 0, 2)));
        }

        [Test]
        public void Circle_FromCode()
        {
            var area = new InCircleArea { DefineInInspector = false, AreaName = "Danger" };
            Assert.IsFalse(Passes(area, new Vector3(0, 0, 0)), "Nothing set yet, so nothing passes.");

            SensorAreas.SetCircle("Danger", new Vector2(10, 5), 3f);
            Assert.IsTrue(Passes(area, new Vector3(12, 0, 5)));
            Assert.IsFalse(Passes(area, new Vector3(0, 0, 0)));
        }

        [Test]
        public void SameNameCanHoldAPolygonAndACircle()
        {
            SensorAreas.SetCircle("Zone", new Vector2(100, 100), 1f);
            var polygon = new InPolygonArea { DefineInInspector = false, AreaName = "Zone" };
            Assert.IsFalse(Passes(polygon, new Vector3(100, 0, 100)), "A circle is not a polygon.");
        }

        private static And Tree(params Condition[] children)
        {
            var root = new And();
            root.Children.AddRange(children);
            return root;
        }

        [Test]
        public void NameClash_TwoInspectorAreasWithTheSameName()
        {
            var first = new InPolygonArea { DefineInInspector = true, AreaName = "Zone A" };
            var second = new InPolygonArea { DefineInInspector = true, AreaName = "Zone A" };
            And root = Tree(first, second);

            StringAssert.Contains("Zone A", AreaCondition.FindNameClash(root, first));
            StringAssert.Contains("Zone A", AreaCondition.FindNameClash(root, second));
        }

        [Test]
        public void NameClash_InspectorAndFromCodeWithTheSameName()
        {
            var here = new InPolygonArea { DefineInInspector = true, AreaName = "Zone A" };
            var fromCode = new InPolygonArea { AreaName = "Zone A", DefineInInspector = false };
            And root = Tree(here, fromCode);

            StringAssert.Contains("set from code", AreaCondition.FindNameClash(root, here));
            StringAssert.Contains("defined in the inspector", AreaCondition.FindNameClash(root, fromCode));
        }

        [Test]
        public void NameClash_NoneForTwoFromCodeAreasSharingAName()
        {
            var first = new InPolygonArea { AreaName = "Zone A", DefineInInspector = false };
            var second = new InPolygonArea { AreaName = "Zone A", DefineInInspector = false };

            Assert.IsNull(AreaCondition.FindNameClash(Tree(first, second), first));
        }

        [Test]
        public void NameClash_NoneForDifferentNamesKindsOrNoName()
        {
            var polygon = new InPolygonArea { DefineInInspector = true, AreaName = "Zone A" };
            var circle = new InCircleArea { DefineInInspector = true, AreaName = "Zone A" };
            var other = new InPolygonArea { DefineInInspector = true, AreaName = "Zone B" };
            var unnamedA = new InPolygonArea();
            var unnamedB = new InPolygonArea();
            And root = Tree(polygon, circle, other, unnamedA, unnamedB);

            Assert.IsNull(AreaCondition.FindNameClash(root, polygon));
            Assert.IsNull(AreaCondition.FindNameClash(root, circle));
            Assert.IsNull(AreaCondition.FindNameClash(root, unnamedA));
        }

        [Test]
        public void NameClash_FoundInsideNestedOperators()
        {
            var first = new InCircleArea { DefineInInspector = true, AreaName = "Danger" };
            var second = new InCircleArea { DefineInInspector = true, AreaName = "Danger" };
            And root = Tree(first, new Not { Child = Tree(second) });

            Assert.IsNotNull(AreaCondition.FindNameClash(root, first));
        }

        private static readonly Vector2[] SquareAtOrigin = { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) };
        private static readonly Vector2[] SquareFarAway = { new Vector2(50, 50), new Vector2(60, 50), new Vector2(60, 60), new Vector2(50, 60) };

        [Test]
        public void Polygons_InsideAnyPasses()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(SquareAtOrigin);
            area.AddPolygon(SquareFarAway);

            Assert.IsTrue(Passes(area, new Vector3(5, 0, 5)));
            Assert.IsTrue(Passes(area, new Vector3(55, 0, 55)));
            Assert.IsFalse(Passes(area, new Vector3(30, 0, 30)), "Between the two squares is outside both.");
        }

        [Test]
        public void Polygons_OverlappingPartIsStillInside()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(SquareAtOrigin);
            area.AddPolygon(new Vector2(5, 5), new Vector2(15, 5), new Vector2(15, 15), new Vector2(5, 15));

            Assert.IsTrue(Passes(area, new Vector3(7, 0, 7)), "A union: inside both is inside.");
        }

        [Test]
        public void Polygons_OneWithTooFewPointsIsIgnored()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(new Vector2(0, 0), new Vector2(1, 1));
            area.AddPolygon(SquareFarAway);

            Assert.IsTrue(Passes(area, new Vector3(55, 0, 55)));
            Assert.IsFalse(Passes(area, new Vector3(0.5f, 0, 0.5f)));
            StringAssert.Contains("1 ignored", area.Summary);
        }

        [Test]
        public void Polygons_SetPolygonReplacesAllAndClearPassesNothing()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygons(new[] { SquareAtOrigin, SquareFarAway });
            Assert.IsTrue(Passes(area, new Vector3(55, 0, 55)));

            area.SetPolygon(SquareAtOrigin);
            Assert.IsFalse(Passes(area, new Vector3(55, 0, 55)));

            area.ClearPolygons();
            Assert.IsFalse(Passes(area, new Vector3(5, 0, 5)));
        }

        [Test]
        public void Polygons_FromCode_AddAndSet()
        {
            var area = new InPolygonArea { AreaName = "Zones" };
            SensorAreas.SetPolygon("Zones", SquareAtOrigin);
            SensorAreas.AddPolygon("Zones", SquareFarAway);
            Assert.AreEqual(2, SensorAreas.GetPolygonCount("Zones"));
            Assert.IsTrue(Passes(area, new Vector3(5, 0, 5)));
            Assert.IsTrue(Passes(area, new Vector3(55, 0, 55)));

            // Back to one: the far square is gone.
            SensorAreas.SetPolygon("Zones", SquareAtOrigin);
            Assert.AreEqual(1, SensorAreas.GetPolygonCount("Zones"));
            Assert.IsFalse(Passes(area, new Vector3(55, 0, 55)));

            // Set to none: still set, so no warning, but nothing is inside.
            SensorAreas.SetPolygons("Zones", new Vector2[0][]);
            Assert.IsTrue(SensorAreas.HasPolygons("Zones"));
            Assert.IsFalse(Passes(area, new Vector3(5, 0, 5)));
        }

        [Test]
        public void Circles_InsideAnyPasses()
        {
            var area = new InCircleArea { DefineInInspector = true };
            area.SetCircle(new Vector2(0, 0), 2f);
            area.AddCircle(new Vector2(20, 0), 3f);

            Assert.IsTrue(Passes(area, new Vector3(1, 0, 0)));
            Assert.IsTrue(Passes(area, new Vector3(22, 0, 0)));
            Assert.IsFalse(Passes(area, new Vector3(10, 0, 0)));
        }

        [Test]
        public void Circles_FromCode_AddSetAndRemove()
        {
            var area = new InCircleArea { AreaName = "Danger" };
            SensorAreas.SetCircle("Danger", new Vector2(0, 0), 2f);
            SensorAreas.AddCircle("Danger", new Vector2(20, 0), 3f);
            Assert.AreEqual(2, SensorAreas.GetCircleCount("Danger"));
            Assert.IsTrue(Passes(area, new Vector3(22, 0, 0)));

            SensorAreas.SetCircles("Danger", new[] { new AreaCircle(new Vector2(100, 0), 1f) });
            Assert.IsFalse(Passes(area, new Vector3(22, 0, 0)));
            Assert.IsTrue(Passes(area, new Vector3(100, 0, 0)));

            SensorAreas.RemoveCircles("Danger");
            Assert.IsFalse(Passes(area, new Vector3(100, 0, 0)));
        }

        [Test]
        public void SetCircles_CopiesTheCallersCircles()
        {
            var mine = new AreaCircle(new Vector2(0, 0), 2f);
            SensorAreas.SetCircles("Danger", new[] { mine });
            mine.Center = new Vector2(500, 500);

            var area = new InCircleArea { AreaName = "Danger" };
            Assert.IsTrue(Passes(area, new Vector3(1, 0, 0)));
        }

        private static readonly Vector2[] SquareFurther = { new Vector2(100, 100), new Vector2(110, 100), new Vector2(110, 110), new Vector2(100, 110) };

        [Test]
        public void Polygons_FromCode_RemoveTheSecond()
        {
            var area = new InPolygonArea { AreaName = "Zones" };
            SensorAreas.SetPolygon("Zones", SquareAtOrigin);
            SensorAreas.AddPolygon("Zones", SquareFarAway);
            SensorAreas.AddPolygon("Zones", SquareFurther);

            Assert.IsTrue(SensorAreas.RemovePolygonAt("Zones", 1));
            Assert.AreEqual(2, SensorAreas.GetPolygonCount("Zones"));
            Assert.IsTrue(Passes(area, new Vector3(5, 0, 5)));
            Assert.IsFalse(Passes(area, new Vector3(55, 0, 55)), "The second polygon is gone.");
            Assert.IsTrue(Passes(area, new Vector3(105, 0, 105)), "The third moved down to index 1.");

            // Adding again after a removal reuses the freed list and still gives the right shape.
            SensorAreas.AddPolygon("Zones", SquareFarAway);
            Assert.IsTrue(Passes(area, new Vector3(55, 0, 55)));

            Assert.IsFalse(SensorAreas.RemovePolygonAt("Zones", 5));
            Assert.IsFalse(SensorAreas.RemovePolygonAt("Nobody", 0));
        }

        [Test]
        public void Polygons_InInspector_RemoveTheSecond()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(SquareAtOrigin);
            area.AddPolygon(SquareFarAway);

            Assert.IsTrue(area.RemovePolygonAt(1));
            Assert.IsFalse(Passes(area, new Vector3(55, 0, 55)));
            Assert.IsTrue(Passes(area, new Vector3(5, 0, 5)));
            Assert.IsFalse(area.RemovePolygonAt(1));
        }

        [Test]
        public void Circles_RemoveTheSecond()
        {
            SensorAreas.SetCircle("Danger", new Vector2(0, 0), 2f);
            SensorAreas.AddCircle("Danger", new Vector2(20, 0), 3f);
            var fromCode = new InCircleArea { AreaName = "Danger" };

            Assert.IsTrue(SensorAreas.RemoveCircleAt("Danger", 1));
            Assert.IsFalse(Passes(fromCode, new Vector3(20, 0, 0)));
            Assert.IsTrue(Passes(fromCode, new Vector3(0, 0, 0)));

            var own = new InCircleArea { DefineInInspector = true };
            own.SetCircle(new Vector2(0, 0), 2f);
            own.AddCircle(new Vector2(20, 0), 3f);
            Assert.IsTrue(own.RemoveCircleAt(1));
            Assert.IsFalse(Passes(own, new Vector3(20, 0, 0)));
        }

        [Test]
        public void Polygons_FromCode_ByLabel()
        {
            var area = new InPolygonArea { AreaName = "Zones" };
            SensorAreas.AddPolygon("Zones", "Home", SquareAtOrigin);
            SensorAreas.AddPolygon("Zones", "Far", SquareFarAway);
            SensorAreas.AddPolygon("Zones", "Further", SquareFurther);

            // Change "Far" in place: it moves onto the third square's spot and keeps its index.
            SensorAreas.UpdatePolygon("Zones", "Far", SquareFurther);
            Assert.AreEqual(1, SensorAreas.IndexOfPolygon("Zones", "Far"));
            Assert.IsFalse(Passes(area, new Vector3(55, 0, 55)));

            Assert.IsTrue(SensorAreas.RemovePolygon("Zones", "Home"));
            Assert.IsFalse(Passes(area, new Vector3(5, 0, 5)));
            Assert.AreEqual(0, SensorAreas.IndexOfPolygon("Zones", "Far"), "The ones after it move down.");
            Assert.AreEqual(-1, SensorAreas.IndexOfPolygon("Zones", "Home"));
            Assert.IsFalse(SensorAreas.RemovePolygon("Zones", "Home"));
            Assert.IsFalse(SensorAreas.RemovePolygon("Nobody", "Home"));

            // Updating a label that is not there adds it.
            SensorAreas.UpdatePolygon("Zones", "Home", SquareAtOrigin);
            Assert.IsTrue(Passes(area, new Vector3(5, 0, 5)));
            Assert.AreEqual(3, SensorAreas.GetPolygonCount("Zones"));

            var copy = new List<Vector2>();
            Assert.IsTrue(SensorAreas.TryGetPolygon("Zones", "Home", copy));
            Assert.AreEqual(SquareAtOrigin.Length, copy.Count);
        }

        [Test]
        public void Polygons_FromCode_LabelsAreClearedWhenTheSetIsReplaced()
        {
            SensorAreas.AddPolygon("Zones", "Home", SquareAtOrigin);
            SensorAreas.RemovePolygonAt("Zones", 0);
            SensorAreas.AddPolygon("Zones", SquareFarAway); // reuses the freed slot, without a label
            Assert.AreEqual(-1, SensorAreas.IndexOfPolygon("Zones", "Home"));

            SensorAreas.AddPolygon("Zones", "Home", SquareAtOrigin);
            SensorAreas.SetPolygon("Zones", SquareFarAway);
            Assert.AreEqual(-1, SensorAreas.IndexOfPolygon("Zones", "Home"));
        }

        [Test]
        public void Polygons_InInspector_ByLabel()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.ClearPolygons();
            area.AddPolygon("Home", SquareAtOrigin);
            area.AddPolygon("Far", SquareFarAway);

            area.UpdatePolygon("Far", SquareFurther);
            Assert.IsFalse(Passes(area, new Vector3(55, 0, 55)));
            Assert.IsTrue(Passes(area, new Vector3(105, 0, 105)));

            // Points of a found polygon can be changed directly.
            area.FindPolygon("Far").Points.RemoveAt(0);
            Assert.AreEqual(3, area.FindPolygon("Far").Points.Count);

            Assert.IsTrue(area.RemovePolygon("Home"));
            Assert.IsFalse(Passes(area, new Vector3(5, 0, 5)));
            Assert.IsNull(area.FindPolygon("Home"));
            Assert.IsFalse(area.RemovePolygon("Home"));
        }

        [Test]
        public void Polygons_InInspector_AutoLabelsStayUnique()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.SetPolygon(SquareAtOrigin);   // Polygon 1
            area.AddPolygon(SquareFarAway);    // Polygon 2
            area.RemovePolygonAt(0);
            area.AddPolygon(SquareFurther);    // Polygon 2 is taken, so Polygon 3

            Assert.AreEqual("Polygon 2", area.Polygons[0].Label);
            Assert.AreEqual("Polygon 3", area.Polygons[1].Label);
        }

        [Test]
        public void Labels_FromCode_ATakenLabelGetsANewOne()
        {
            Assert.AreEqual("Parking", SensorAreas.AddPolygon("Zones", "Parking", SquareAtOrigin));
            Assert.AreEqual("Parking (2)", SensorAreas.AddPolygon("Zones", "Parking", SquareFarAway));
            Assert.AreEqual("Parking (3)", SensorAreas.AddPolygon("Zones", "Parking", SquareFurther));
            Assert.AreEqual("Polygon 4", SensorAreas.AddPolygon("Zones", SquareAtOrigin));
            Assert.AreEqual("Polygon 5", SensorAreas.AddPolygon("Zones", "Polygon 4", SquareAtOrigin), "A taken automatic label gets the next number.");
            Assert.AreEqual("Polygon 6", SensorAreas.AddPolygon("Zones", "  ", SquareAtOrigin), "A blank label counts as none.");

            Assert.IsTrue(SensorAreas.RemovePolygon("Zones", "Parking (2)"));
            Assert.AreEqual(1, SensorAreas.IndexOfPolygon("Zones", "Parking (3)"));

            Assert.AreEqual("Circle 1", SensorAreas.SetCircle("Danger", Vector2.zero, 1f));
            Assert.AreEqual("Fire", SensorAreas.AddCircle("Danger", "Fire", Vector2.zero, 1f));
            Assert.AreEqual("Fire (2)", SensorAreas.AddCircle("Danger", "Fire", Vector2.zero, 1f));

            SensorAreas.SetCircles("Danger", new[] { new AreaCircle(Vector2.zero, 1f, "Gas"), new AreaCircle(Vector2.zero, 2f, "Gas"), new AreaCircle(Vector2.zero, 3f) });
            Assert.AreEqual(0, SensorAreas.IndexOfCircle("Danger", "Gas"));
            Assert.AreEqual(1, SensorAreas.IndexOfCircle("Danger", "Gas (2)"));
            Assert.AreEqual(2, SensorAreas.IndexOfCircle("Danger", "Circle 3"));

            Assert.IsNull(SensorAreas.AddPolygon("", "Parking", SquareAtOrigin), "No area name, nothing added.");
        }

        [Test]
        public void Labels_OnTheCondition_ATakenLabelGetsANewOne()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            Assert.AreEqual("Polygon 1", area.SetPolygon(SquareAtOrigin));
            Assert.AreEqual("Polygon 2", area.AddPolygon("Polygon 1", SquareFarAway));
            Assert.AreEqual("Home", area.AddPolygon("Home", SquareFurther));
            Assert.AreEqual("Home (2)", area.AddPolygon("Home", SquareFurther));

            var circles = new InCircleArea { DefineInInspector = true };
            circles.ClearCircles();
            Assert.AreEqual("Fire", circles.AddCircle("Fire", Vector2.zero, 1f));
            Assert.AreEqual("Fire (2)", circles.AddCircle("Fire", Vector2.zero, 1f));
            circles.SetCircles(new[] { new AreaCircle(Vector2.zero, 1f, "Gas"), new AreaCircle(Vector2.zero, 2f, "Gas") });
            Assert.AreEqual("Gas (2)", circles.Circles[1].Label);
        }

        [Test]
        public void Labels_TypedCopies_AreFoundAndRenamed()
        {
            var area = new InPolygonArea { DefineInInspector = true };
            area.Polygons = new List<AreaPolygon>
            {
                new AreaPolygon(SquareAtOrigin, "A"),
                new AreaPolygon(SquareFarAway, "A"),
                new AreaPolygon(SquareFurther, ""),
                new AreaPolygon(SquareAtOrigin, "B"),
                null,
            };

            string clash = area.FindLabelClash();
            StringAssert.Contains("\"A\"", clash);
            StringAssert.Contains("\"A (2)\"", clash);

            Assert.AreEqual(2, area.MakeLabelsUnique());
            Assert.AreEqual("A", area.Polygons[0].Label, "The first keeps its label.");
            Assert.AreEqual("A (2)", area.Polygons[1].Label);
            Assert.AreEqual("Polygon 3", area.Polygons[2].Label, "An empty one gets its own number.");
            Assert.AreEqual("B", area.Polygons[3].Label);
            Assert.IsNull(area.FindLabelClash());
            Assert.AreEqual(0, area.MakeLabelsUnique());

            // The list is not used with Define In Inspector off, so it is not checked.
            area.Polygons[1].Label = "A";
            area.DefineInInspector = false;
            Assert.IsNull(area.FindLabelClash());
        }

        [Test]
        public void Labels_PlusButtonCopy_GetsTheNextName()
        {
            // The inspector's + button copies the last entry, label included; only the added ones are renamed.
            var area = new InCircleArea { DefineInInspector = true };
            area.Circles = new List<AreaCircle>
            {
                new AreaCircle(Vector2.zero, 1f, "Circle 1"),
                new AreaCircle(Vector2.zero, 1f, "Circle 1"),
            };
            Assert.AreEqual(1, area.MakeLabelsUnique(1));
            Assert.AreEqual("Circle 2", area.Circles[1].Label);

            area.Circles.Add(new AreaCircle(Vector2.zero, 1f, "Fire"));
            area.Circles.Add(new AreaCircle(Vector2.zero, 1f, "Fire"));
            Assert.AreEqual(1, area.MakeLabelsUnique(3));
            Assert.AreEqual("Fire (2)", area.Circles[3].Label);
        }

        [Test]
        public void Circles_ByLabel()
        {
            var fromCode = new InCircleArea { AreaName = "Danger" };
            SensorAreas.AddCircle("Danger", "Fire", new Vector2(0, 0), 2f);
            SensorAreas.AddCircle("Danger", "Gas", new Vector2(20, 0), 3f);

            SensorAreas.UpdateCircle("Danger", "Gas", new Vector2(40, 0), 3f);
            Assert.IsFalse(Passes(fromCode, new Vector3(20, 0, 0)));
            Assert.IsTrue(Passes(fromCode, new Vector3(40, 0, 0)));

            Assert.IsTrue(SensorAreas.SetCircleRadius("Danger", "Fire", 10f));
            Assert.IsTrue(Passes(fromCode, new Vector3(8, 0, 0)));
            Assert.IsTrue(SensorAreas.SetCircleCenter("Danger", "Fire", new Vector2(-100, 0)));
            Assert.IsFalse(Passes(fromCode, new Vector3(8, 0, 0)));
            Assert.IsFalse(SensorAreas.SetCircleRadius("Danger", "Nobody", 1f));

            Assert.IsTrue(SensorAreas.TryGetCircle("Danger", "Fire", out Vector2 center, out float radius));
            Assert.AreEqual(new Vector2(-100, 0), center);
            Assert.AreEqual(10f, radius);

            Assert.IsTrue(SensorAreas.RemoveCircle("Danger", "Gas"));
            Assert.IsFalse(Passes(fromCode, new Vector3(40, 0, 0)));
            Assert.IsFalse(SensorAreas.RemoveCircle("Danger", "Gas"));

            var own = new InCircleArea { DefineInInspector = true };
            own.ClearCircles();
            own.AddCircle("Fire", new Vector2(0, 0), 2f);
            own.UpdateCircle("Gas", new Vector2(20, 0), 3f); // not there, so added
            Assert.IsTrue(Passes(own, new Vector3(20, 0, 0)));

            own.FindCircle("Gas").Radius = 0.5f;
            Assert.IsFalse(Passes(own, new Vector3(21, 0, 0)));

            Assert.IsTrue(own.RemoveCircle("Fire"));
            Assert.IsFalse(Passes(own, new Vector3(0, 0, 0)));
        }

        [Test]
        public void ShowVisual_DrawsACrossOnTheTarget()
        {
            var area = new InCircleArea { DefineInInspector = true, ShowVisual = true };
            area.SetCircle(Vector2.zero, 5f);
            var visuals = new ConditionVisuals();
            var context = new SensorContext(_sensor.transform, null, 0f, null, visuals);

            area.Prepare(context);
            area.Evaluate(new Stub { Position = new Vector3(1, 0, 1) }, context);

            Assert.Greater(visuals.Count, 0);
        }
    }
}
