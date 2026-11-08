using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class DetectableRegistryTests
    {
        private class Stub : IDetectable
        {
            public string Id = "stub";
            public bool Valid = true;
            public Bounds Bounds { get; set; }
            public bool IsFake => false;
            public bool IsValid => Valid;
            public string GetIdentifier() => Id;
            public Vector3 GetCenterPosition() => Vector3.zero;
            public string GetDetectionType() => "Stub";
            public float GetVelocity() => 0f;
        }

        private class LayeredStub : Stub, IDetectableLayer
        {
            public int Layer;
            public int DetectionLayer => Layer;
        }

        private readonly List<IDetectable> _added = new List<IDetectable>();

        private T Add<T>(T detectable) where T : IDetectable
        {
            DetectableRegistry.Register(detectable);
            _added.Add(detectable);
            return detectable;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (IDetectable detectable in _added) DetectableRegistry.Unregister(detectable);
            _added.Clear();
        }

        [Test]
        public void RegisterAndUnregister_ReportWhetherAnythingChanged_AndBumpTheVersion()
        {
            var stub = new Stub();
            int version = DetectableRegistry.Version;

            Assert.IsTrue(Add(stub) != null);
            Assert.IsFalse(DetectableRegistry.Register(stub), "Registering twice changes nothing.");
            Assert.AreEqual(version + 1, DetectableRegistry.Version);
            Assert.IsTrue(DetectableRegistry.IsRegistered(stub));

            Assert.IsTrue(DetectableRegistry.Unregister(stub));
            Assert.IsFalse(DetectableRegistry.Unregister(stub));
            Assert.IsFalse(DetectableRegistry.IsRegistered(stub));
            Assert.AreEqual(version + 2, DetectableRegistry.Version);
        }

        [Test]
        public void Null_IsIgnored()
        {
            Assert.IsFalse(DetectableRegistry.Register(null));
            Assert.IsFalse(DetectableRegistry.Unregister(null));
        }

        [Test]
        public void CollectInto_SkipsObjectsThatAreNoLongerAlive()
        {
            var alive = Add(new Stub { Id = "alive" });
            var gone = Add(new Stub { Id = "gone", Valid = false });
            var result = new HashSet<IDetectable>();

            DetectableRegistry.CollectInto(result, ~0);

            CollectionAssert.Contains(result, alive);
            CollectionAssert.DoesNotContain(result, gone);
        }

        [Test]
        public void CollectInto_FiltersByTheObjectsLayer_AndKeepsObjectsWithoutOne()
        {
            var onLayer3 = Add(new LayeredStub { Id = "a", Layer = 3 });
            var onLayer5 = Add(new LayeredStub { Id = "b", Layer = 5 });
            var plain = Add(new Stub { Id = "c" });
            var result = new HashSet<IDetectable>();

            DetectableRegistry.CollectInto(result, 1 << 3);

            CollectionAssert.Contains(result, onLayer3);
            CollectionAssert.DoesNotContain(result, onLayer5);
            CollectionAssert.Contains(result, plain, "A wrapper with no layer is always included.");
        }

        [Test]
        public void CollectInto_UsesAComponentsGameObjectLayer()
        {
            var owner = new GameObject("on layer 6") { layer = 6 };
            try
            {
                DetectableEntity entity = owner.AddComponent<DetectableEntity>();
                Add(entity);
                var result = new HashSet<IDetectable>();

                DetectableRegistry.CollectInto(result, 1 << 6);
                CollectionAssert.Contains(result, entity);

                result.Clear();
                DetectableRegistry.CollectInto(result, 1 << 7);
                CollectionAssert.DoesNotContain(result, entity);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TheDetectionType_IsTheSameStringEveryTime()
        {
            var owner = new GameObject("typed");
            try
            {
                DetectableEntity entity = owner.AddComponent<DetectableEntity>();
                Assert.AreSame(entity.GetDetectionType(), entity.GetDetectionType(), "No new string per call.");
                Assert.AreEqual("Unknown", entity.GetDetectionType());
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
