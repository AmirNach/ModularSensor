using System;
using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class DetectableSafetyTests
    {
        private class Stub : IDetectable, IEntityTyped
        {
            public string Id = "stub";
            public bool Valid = true;
            public bool Throws;
            public Vector3 Position = new Vector3(1f, 2f, 3f);

            public Bounds Bounds { get; set; } = new Bounds(Vector3.zero, Vector3.one);
            public bool IsFake => false;
            public bool IsValid => Valid;
            public EntityType EntityType => EntityType.Drone;

            public string GetIdentifier() => Throws ? throw new InvalidOperationException() : Id;
            public Vector3 GetCenterPosition() => Throws ? throw new InvalidOperationException() : Position;
            public string GetDetectionType() => Throws ? throw new InvalidOperationException() : "Stub";
            public float GetVelocity() => 2.5f;
        }

        private class PlainStub : IDetectable
        {
            public Bounds Bounds { get; set; }
            public bool IsFake => false;
            public string GetIdentifier() => "plain";
            public Vector3 GetCenterPosition() => Vector3.zero;
            public string GetDetectionType() => "Plain";
            public float GetVelocity() => 0f;
        }

        [Test]
        public void IsAlive_IsFalseForNull_AndForAnObjectThatSaysItIsInvalid()
        {
            Assert.IsFalse(((IDetectable)null).IsAlive());
            Assert.IsFalse(new Stub { Valid = false }.IsAlive());
            Assert.IsTrue(new Stub().IsAlive());
        }

        [Test]
        public void IsAlive_IsTrueForAnImplementationThatDoesNotKnowAboutIsValid()
        {
            Assert.IsTrue(new PlainStub().IsAlive(), "Existing IDetectable implementations keep working.");
        }

        [Test]
        public void IsAlive_IsFalseForADestroyedUnityObject()
        {
            var owner = new GameObject("doomed");
            DetectableEntity entity = owner.AddComponent<DetectableEntity>();
            Assert.IsTrue(entity.IsAlive());

            UnityEngine.Object.DestroyImmediate(owner);

            Assert.IsFalse(entity.IsAlive());
        }

        [Test]
        public void TryGetId_RejectsGoneThrowingAndEmptyIds_WithoutThrowing()
        {
            Assert.IsFalse(((IDetectable)null).TryGetId(out _));
            Assert.IsFalse(new Stub { Valid = false }.TryGetId(out _));
            Assert.IsFalse(new Stub { Throws = true }.TryGetId(out _));
            Assert.IsFalse(new Stub { Id = string.Empty }.TryGetId(out _));
            Assert.IsFalse(new Stub { Id = null }.TryGetId(out _));

            Assert.IsTrue(new Stub { Id = "abc" }.TryGetId(out string id));
            Assert.AreEqual("abc", id);
        }

        [Test]
        public void Snapshot_CopiesTheTargetsValues()
        {
            var live = new Stub { Id = "x", Position = new Vector3(4f, 5f, 6f) };

            DetectableSnapshot snapshot = DetectableSnapshot.Of(live, "x");

            Assert.AreEqual("x", snapshot.GetIdentifier());
            Assert.AreEqual("Stub", snapshot.GetDetectionType());
            Assert.AreEqual(new Vector3(4f, 5f, 6f), snapshot.GetCenterPosition());
            Assert.AreEqual(2.5f, snapshot.GetVelocity());
            Assert.AreEqual(EntityType.Drone, snapshot.EntityType);
            Assert.IsFalse(snapshot.IsFake);
            Assert.IsTrue(snapshot.IsAlive());
        }

        [Test]
        public void Snapshot_KeepsItsOldValues_WhenTheTargetCanNoLongerBeRead()
        {
            var live = new Stub { Position = new Vector3(4f, 5f, 6f) };
            DetectableSnapshot snapshot = DetectableSnapshot.Of(live, "stub");

            live.Throws = true;
            bool updated = snapshot.Update(live);

            Assert.IsFalse(updated);
            Assert.AreEqual(new Vector3(4f, 5f, 6f), snapshot.GetCenterPosition(), "A failed update must not leave half-written values.");
            Assert.AreEqual("stub", snapshot.GetIdentifier());
        }

        [Test]
        public void Snapshot_OfADestroyedUnityObject_DoesNotThrow_AndKeepsTheId()
        {
            var owner = new GameObject("doomed");
            DetectableEntity entity = owner.AddComponent<DetectableEntity>();
            UnityEngine.Object.DestroyImmediate(owner);

            DetectableSnapshot snapshot = null;
            Assert.DoesNotThrow(() => snapshot = DetectableSnapshot.Of(entity, "known-id"));
            Assert.AreEqual("known-id", snapshot.GetIdentifier());
        }

        [Test]
        public void ProbabilityRoll_KeepsItsAttempts_ForANewInstanceWithTheSameId()
        {
            var owner = new GameObject("sensor");
            try
            {
                var condition = new InAbsoluteRangesWithProbabilities
                {
                    HasLimitedDetectionAttempts = true,
                    MaxDetectionAttempts = 2,
                    StopRollingOnceDetected = false,
                    DistanceProbabilities = new System.Collections.Generic.List<DistanceProbability> { new DistanceProbability { Distance = 100f, Probability = 100f } },
                };
                var context = new SensorContext(owner.transform, null, 0f);

                Assert.IsTrue(condition.Evaluate(new Stub { Id = "same" }, context));
                Assert.IsTrue(condition.Evaluate(new Stub { Id = "same" }, context), "A new instance with the same ID is the same target.");
                Assert.IsFalse(condition.Evaluate(new Stub { Id = "same" }, context), "Two attempts are used up, whichever instance asked.");

                condition.OnTargetLost(DetectableSnapshot.Of(new Stub { Id = "same" }, "same"));
                Assert.IsTrue(condition.Evaluate(new Stub { Id = "same" }, context), "Losing it by snapshot clears its attempts.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
