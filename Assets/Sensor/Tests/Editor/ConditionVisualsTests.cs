using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class ConditionVisualsTests
    {
        private class Stub : IDetectable
        {
            public Vector3 Position;
            public float Velocity;
            public Bounds Bounds { get; set; }
            public bool IsFake => false;
            public string GetIdentifier() => "stub";
            public Vector3 GetCenterPosition() => Position;
            public string GetDetectionType() => "Stub";
            public float GetVelocity() => Velocity;
        }

        private GameObject _sensor;
        private GameObject _wall;

        [SetUp]
        public void SetUp()
        {
            _sensor = new GameObject("sensor");
        }

        [TearDown]
        public void TearDown()
        {
            if (_sensor != null) Object.DestroyImmediate(_sensor);
            if (_wall != null) Object.DestroyImmediate(_wall);
        }

        private SensorContext Context(ConditionVisuals visuals) => new SensorContext(_sensor.transform, null, 0f, null, visuals);

        [Test]
        public void ClearRaycast_DrawsOneGreenLineToTheTarget()
        {
            var visuals = new ConditionVisuals();
            var raycast = new WithRaycast { ShowVisual = true };

            bool passed = raycast.Evaluate(new Stub { Position = new Vector3(0f, 0f, 10f) }, Context(visuals));

            Assert.IsTrue(passed);
            Assert.AreEqual(1, visuals.Count);
        }

        [Test]
        public void BlockedRaycast_DrawsARedLineToTheHit_AMarkerAndTheRestDimmed()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = new Vector3(0f, 0f, 5f);
            Physics.SyncTransforms();
            var visuals = new ConditionVisuals();

            bool passed = new WithRaycast { ShowVisual = true }.Evaluate(new Stub { Position = new Vector3(0f, 0f, 10f) }, Context(visuals));

            Assert.IsFalse(passed);
            Assert.AreEqual(3, visuals.Count, "Line to the hit, a cross at the hit, and a dim line on to the target.");
        }

        [Test]
        public void WithoutVisuals_ConditionsDrawNothingAndStillWork()
        {
            var target = new Stub { Position = new Vector3(0f, 0f, 10f), Velocity = 5f };

            Assert.IsTrue(new WithRaycast().Evaluate(target, Context(null)));
            Assert.IsTrue(new IsMoving().Evaluate(target, Context(null)));
            Assert.IsTrue(new IsAngleClamped().Evaluate(target, Context(null)));
        }

        [Test]
        public void IsMoving_MarksThePassOrFailColour()
        {
            var visuals = new ConditionVisuals();
            var moving = new IsMoving { MinMovingVelocity = 1f, ShowVisual = true };

            moving.Evaluate(new Stub { Velocity = 5f }, Context(visuals));
            moving.Evaluate(new Stub { Velocity = 0f }, Context(visuals));

            Assert.AreEqual(2, visuals.Count);
        }

        [Test]
        public void Visuals_StopGrowingAtTheirCap_AndCanBeCleared()
        {
            var visuals = new ConditionVisuals();
            for (int i = 0; i < 5000; i++)
            {
                visuals.Line(Vector3.zero, Vector3.one, Color.white);
            }
            Assert.Less(visuals.Count, 5000);

            visuals.Clear();
            Assert.AreEqual(0, visuals.Count);
        }

        [Test]
        public void EachConditionDecidesForItself_ASwitchedOffConditionDrawsNothing()
        {
            var visuals = new ConditionVisuals();
            var target = new Stub { Position = new Vector3(0f, 0f, 10f), Velocity = 5f };
            var raycast = new WithRaycast { ShowVisual = true };
            var moving = new IsMoving { ShowVisual = false };

            raycast.Evaluate(target, Context(visuals));
            moving.Evaluate(target, Context(visuals));

            Assert.AreEqual(1, visuals.Count, "Only the raycast, which is switched on, draws.");
            Assert.IsTrue(raycast.WantsVisuals);
            Assert.IsFalse(moving.WantsVisuals);
            Assert.IsFalse(new Or().WantsVisuals, "Operators never draw.");
        }

        [Test]
        public void VisualsAreOffByDefault()
        {
            Assert.IsFalse(new WithRaycast().ShowVisual);
            Assert.IsFalse(new InAbsoluteRangesWithProbabilities().ShowVisual);
        }

        [Test]
        public void TheCardsVisualSwitch_FollowsTheCondition()
        {
            var condition = new WithRaycast();
            var card = new NodeCard("Raycast", null, "sensor-card--leaf", () => null, null, false);
            card.AddVisualToggle(condition);
            var toggle = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Toggle>(card);
            Assert.IsFalse(toggle.value);

            condition.ShowVisual = true;
            card.Refresh();

            Assert.IsTrue(toggle.value, "A change made elsewhere shows up on the card.");
        }

        [Test]
        public void EveryVisualCondition_HasItsOwnDefaultColor()
        {
            Color[] colors =
            {
                new WithRaycast().VisualColor, new IsAngleClamped().VisualColor, new InViewPort().VisualColor,
                new IsMoving().VisualColor, new InAbsoluteRangesWithProbabilities().VisualColor, new InFrustum().VisualColor,
            };

            Assert.AreEqual(colors.Length, new System.Collections.Generic.HashSet<Color>(colors).Count, "No two share a default color.");
            foreach (Color color in colors)
            {
                Assert.AreEqual(1f, color.a, "Defaults are opaque.");
            }
        }

        [Test]
        public void TheShapesUseTheConditionsColor_AndFailedOnesAreFainter()
        {
            var visuals = new ConditionVisuals();
            var moving = new IsMoving { MinMovingVelocity = 1f, ShowVisual = true, VisualColor = Color.magenta };

            Assert.AreEqual(VisualCondition.FailedAlpha, VisualCondition.Faint(Color.magenta).a, 0.0001f);
            Assert.AreEqual(Color.magenta.r, VisualCondition.Faint(Color.magenta).r, "Only the alpha changes.");
            moving.Evaluate(new Stub { Velocity = 5f }, Context(visuals));
            Assert.AreEqual(1, visuals.Count);
        }

        [Test]
        public void TheFrustumIsAVisualCondition_OffByDefault()
        {
            var frustum = new InFrustum();
            Assert.IsFalse(frustum.ShowVisual);
            Assert.IsFalse(frustum.WantsVisuals);
            frustum.ShowVisual = true;
            Assert.IsTrue(frustum.WantsVisuals);
        }

        [Test]
        public void TheColorField_IsShownOnlyWhileShowVisualIsOn()
        {
            var condition = new WithRaycast();
            ParameterPanel panel = ParameterEditor.Build(condition);
            var color = UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Vector4Field>(panel);
            Assert.IsNotNull(color, "The color still has a control.");
            Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.None, color.style.display.value, "Hidden while Show visual is off.");

            condition.ShowVisual = true;
            panel.Refresh();
            Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.Flex, color.style.display.value);

            condition.ShowVisual = false;
            panel.Refresh();
            Assert.AreEqual(UnityEngine.UIElements.DisplayStyle.None, color.style.display.value);
        }
    }
}
