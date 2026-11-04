using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SensorSystem.Tests
{
    public class ConditionTreeTests
    {
        private class StubTarget : IDetectable, IEntityTyped
        {
            public Vector3 Position;
            public EntityType EntityType { get; set; }
            public Bounds Bounds { get; set; } = new Bounds(Vector3.zero, Vector3.one);
            public bool IsFake => false;
            public string GetIdentifier() => "stub";
            public Vector3 GetCenterPosition() => Position;
            public string GetDetectionType() => EntityType.ToString();
            public float GetVelocity() => 0f;
        }

        private class Fixed : Condition
        {
            public readonly bool Result;
            public int Calls;

            public Fixed(bool result)
            {
                Result = result;
            }

            public override bool Evaluate(IDetectable target, in SensorContext context)
            {
                Calls++;
                return Result;
            }
        }

        private static bool Run(Condition root)
        {
            return Condition.Run(root, new StubTarget(), new SensorContext(null, null, 0f));
        }

        private static And AndOf(params Condition[] children) => new And { Children = new List<Condition>(children) };
        private static Or OrOf(params Condition[] children) => new Or { Children = new List<Condition>(children) };

        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, false)]
        public void And_PassesOnlyWhenAllChildrenPass(bool a, bool b, bool expected)
        {
            Assert.AreEqual(expected, Run(AndOf(new Fixed(a), new Fixed(b))));
        }

        [TestCase(true, true, true)]
        [TestCase(true, false, true)]
        [TestCase(false, true, true)]
        [TestCase(false, false, false)]
        public void Or_PassesWhenAnyChildPasses(bool a, bool b, bool expected)
        {
            Assert.AreEqual(expected, Run(OrOf(new Fixed(a), new Fixed(b))));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void Not_InvertsItsChild(bool child, bool expected)
        {
            Assert.AreEqual(expected, Run(new Not { Child = new Fixed(child) }));
        }

        [Test]
        public void EmptyNodes_AreFalse()
        {
            Assert.IsFalse(Run(null));
            Assert.IsFalse(Run(new And()));
            Assert.IsFalse(Run(new Or()));
            Assert.IsFalse(Run(new Not()));
        }

        [Test]
        public void And_StopsAtFirstFailure()
        {
            var last = new Fixed(true);
            Run(AndOf(new Fixed(false), last));
            Assert.AreEqual(0, last.Calls);
        }

        [Test]
        public void Or_StopsAtFirstPass()
        {
            var last = new Fixed(true);
            Run(OrOf(new Fixed(true), last));
            Assert.AreEqual(0, last.Calls);
        }

        [Test]
        public void Trace_RecordsEvaluatedLeavesOnly()
        {
            var trace = new EvaluationTrace();
            var root = AndOf(new InFrustumStub(false), new IsMovingStub());
            Condition.Run(root, new StubTarget(), new SensorContext(null, null, 0f, trace));

            CollectionAssert.AreEquivalent(new[] { "In Frustum Stub" }, trace.Failed);
            Assert.IsEmpty(trace.Passed);
        }

        private class InFrustumStub : Fixed
        {
            public InFrustumStub(bool result) : base(result) { }
        }

        private class IsMovingStub : Fixed
        {
            public IsMovingStub() : base(true) { }
        }

        [Test]
        public void Describe_ReadsLikeAnExpression()
        {
            var root = AndOf(new InFrustum(), OrOf(new IsMoving(), new Not { Child = new WithRaycast() }));
            Assert.AreEqual("In Frustum AND (Is Moving OR NOT With Raycast)", ConditionTree.Describe(root));
        }

        [Test]
        public void Clone_KeepsStructureAndSettingsButSharesNothing()
        {
            var frustum = new InFrustum { HorizontalFov = 33f };
            var root = AndOf(frustum, new Not { Child = new IsMoving { MinMovingVelocity = 7f } });

            var copy = (And)ConditionTree.Clone(root);

            Assert.AreNotSame(root, copy);
            Assert.AreEqual(2, copy.Children.Count);
            var frustumCopy = (InFrustum)copy.Children[0];
            Assert.AreNotSame(frustum, frustumCopy);
            Assert.AreEqual(33f, frustumCopy.HorizontalFov);
            Assert.AreEqual(7f, ((IsMoving)((Not)copy.Children[1]).Child).MinMovingVelocity);
        }

        [TestCase(0f, 10f, 0f)]
        [TestCase(10f, 0f, 90f)]
        [TestCase(-10f, 0f, -90f)]
        public void GetYaw_IsSignedFromForward(float x, float z, float expected)
        {
            float yaw = AngleUtil.GetYaw(Vector3.zero, Quaternion.identity, new Vector3(x, 5f, z));
            Assert.AreEqual(expected, yaw, 0.001f);
        }

        [TestCase(30f, -45f, 45f, true)]
        [TestCase(60f, -45f, 45f, false)]
        [TestCase(170f, 150f, -150f, true)]
        [TestCase(0f, 150f, -150f, false)]
        public void IsYawWithin_HandlesNormalAndWrappedArcs(float yaw, float min, float max, bool expected)
        {
            Assert.AreEqual(expected, AngleUtil.IsYawWithin(yaw, min, max));
        }

        [TestCase(0f, 0f, 50f, true)]
        [TestCase(0f, 0f, -50f, false)]
        [TestCase(0f, 0f, 500f, false)]
        [TestCase(200f, 0f, 50f, false)]
        public void FrustumPlanes_FaceForwardWithinRange(float x, float y, float z, bool expected)
        {
            var planes = new Plane[FrustumUtil.PlaneCount];
            FrustumUtil.CalculatePlanes(Vector3.zero, Quaternion.identity, 60f, 40f, 0.1f, 100f, planes);
            var bounds = new Bounds(new Vector3(x, y, z), Vector3.one);
            Assert.AreEqual(expected, GeometryUtility.TestPlanesAABB(planes, bounds));
        }

        [Test]
        public void IdGenerator_ReusesReleasedIds()
        {
            var generator = new LocalUniqueIdGenerator(1, 1, false);
            int id = generator.GetAvailableId();
            Assert.AreEqual(1, id);
            generator.Release(id);
            Assert.AreEqual(1, generator.GetAvailableId());
        }

        [Test]
        public void DistanceProbability_UsesNearestEntryRegardlessOfOrder()
        {
            var entries = new List<DistanceProbability>
            {
                new DistanceProbability { Distance = 100f, Probability = 10f },
                new DistanceProbability { Distance = 10f, Probability = 90f },
            };
            Assert.AreEqual(90f, DistanceProbability.Find(entries, 5f));
            Assert.AreEqual(10f, DistanceProbability.Find(entries, 50f));
            Assert.AreEqual(0f, DistanceProbability.Find(entries, 500f));
        }

        [Test]
        public void PerEntity_AllowsExactlyMaxAttempts()
        {
            var transformOwner = new GameObject("sensor");
            try
            {
                var condition = new InAbsoluteRangesWithProbabilitiesPerEntity
                {
                    HasLimitedDetectionAttempts = true,
                    MaxDetectionAttemptsPerEntity = 2,
                    DoesSaveDetectionAttempts = false,
                    EntityTypeToDistanceProbabilities = new List<InAbsoluteRangesWithProbabilitiesPerEntity.EntityRanges>
                    {
                        new InAbsoluteRangesWithProbabilitiesPerEntity.EntityRanges
                        {
                            EntityType = EntityType.Drone,
                            DistanceProbabilities = new List<DistanceProbability> { new DistanceProbability { Distance = 10f, Probability = 100f } },
                        },
                    },
                };
                var target = new StubTarget { EntityType = EntityType.Drone };
                var context = new SensorContext(transformOwner.transform, null, 0f);

                Assert.IsTrue(condition.Evaluate(target, context));
                Assert.IsTrue(condition.Evaluate(target, context));
                Assert.IsFalse(condition.Evaluate(target, context));

                condition.OnTargetLost(target);
                Assert.IsTrue(condition.Evaluate(target, context));
            }
            finally
            {
                Object.DestroyImmediate(transformOwner);
            }
        }

        [Test]
        public void Collection_KeepsTargetUntilLastSensorOfSameTypeLosesIt()
        {
            var owner = new GameObject("sensors");
            var settings = ScriptableObject.CreateInstance<DetectorSettings>();
            try
            {
                Sensor first = owner.AddComponent<Sensor>();
                Sensor second = owner.AddComponent<Sensor>();
                int lostEvents = 0;
                var collection = new DetectionsCollection(settings, _ => { }, _ => lostEvents++);
                var target = new StubTarget();
                var fromFirst = new Detection(target, 1, first, new HashSet<string>(), new HashSet<string>(), 0f);
                var fromSecond = new Detection(target, 1, second, new HashSet<string>(), new HashSet<string>(), 0f);

                collection.AddFromSensor(fromFirst, first);
                collection.AddFromSensor(fromSecond, second);

                collection.RemoveFromSensor(fromFirst, first);
                Assert.AreEqual(0, lostEvents);
                Assert.IsTrue(collection.ContainsKey(target));

                collection.RemoveFromSensor(fromSecond, second);
                Assert.AreEqual(1, lostEvents);
                Assert.IsFalse(collection.ContainsKey(target));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(settings);
            }
        }
    }
}
