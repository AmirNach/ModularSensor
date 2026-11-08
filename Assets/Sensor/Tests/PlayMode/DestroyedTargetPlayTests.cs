using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SensorSystem.Tests
{
    /// <summary>A target that disappears must still produce a complete lost detection, and must never throw.</summary>
    public class DestroyedTargetPlayTests
    {
        private const float RefreshWait = 0.9f;

        [Serializable]
        public class SwitchableCondition : Condition
        {
            public static bool Passes = true;

            public override bool Evaluate(IDetectable target, in SensorContext context) => Passes;
        }

        /// <summary>Stands for an object from another program. Once it is gone every member throws.</summary>
        private class Wrapper : IDetectable
        {
            private readonly string _id;
            private readonly Vector3 _position;

            public bool Gone;

            public Wrapper(string id, Vector3 position)
            {
                _id = id;
                _position = position;
            }

            public bool IsValid => !Gone;
            public bool IsFake => Check(false);
            public Bounds Bounds { get => Check(new Bounds(_position, Vector3.one)); set { } }

            public string GetIdentifier() => Check(_id);
            public Vector3 GetCenterPosition() => Check(_position);
            public string GetDetectionType() => Check("Wrapped");
            public float GetVelocity() => Check(0f);

            private T Check<T>(T value)
            {
                if (Gone) throw new InvalidOperationException("The wrapped object is gone.");
                return value;
            }
        }

        private class ScriptedCollection : IAllObjectsCollection
        {
            public HashSet<IDetectable> AllObjects { get; } = new HashSet<IDetectable>();

            public void SetEnable(bool isEnabled) { }
        }

        private class ScriptedSensor : Sensor
        {
            public ScriptedCollection Source;

            protected override IAllObjectsCollection CreateAllObjectsCollection() => Source;
        }

        private readonly List<Object> _created = new List<Object>();
        private readonly List<Detection> _gained = new List<Detection>();
        private readonly List<Detection> _lost = new List<Detection>();

        [SetUp]
        public void SetUp()
        {
            SwitchableCondition.Passes = true;
            _gained.Clear();
            _lost.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }
            _created.Clear();

            if (ObjectsProvider.Instance != null)
            {
                Object.DestroyImmediate(ObjectsProvider.Instance.gameObject);
            }
        }

        private ScriptedSensor CreateScriptedSensor(ScriptedCollection source)
        {
            var settings = ScriptableObject.CreateInstance<SensorSettings>();
            typeof(SensorSettings).GetField("_refreshRate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, SensorSettings.MinRefreshRate);
            settings.Root = new SwitchableCondition();
            _created.Add(settings);

            var owner = new GameObject("Scripted sensor");
            owner.SetActive(false);
            ScriptedSensor sensor = owner.AddComponent<ScriptedSensor>();
            sensor.Source = source;
            typeof(Sensor).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sensor, settings);
            sensor.OnNewDetectionEvent += _gained.Add;
            sensor.OnLostDetectionEvent += _lost.Add;
            _created.Add(owner);
            owner.SetActive(true);
            return sensor;
        }

        [UnityTest]
        public IEnumerator ATargetThatBecomesInvalid_StillProducesACompleteLostDetection()
        {
            var source = new ScriptedCollection();
            var target = new Wrapper("w1", new Vector3(1f, 2f, 3f));
            source.AllObjects.Add(target);
            CreateScriptedSensor(source);

            yield return new WaitForSeconds(RefreshWait);
            Assert.AreEqual(1, _gained.Count);
            int detectionId = _gained[0].ID;

            target.Gone = true; // from now on, every call on it throws
            yield return new WaitForSeconds(RefreshWait);

            Assert.AreEqual(1, _lost.Count, "The loss must still be reported.");
            Detection lost = _lost[0];
            Assert.AreEqual(detectionId, lost.ID);
            Assert.AreEqual("w1", lost.TargetId);
            Assert.AreEqual(DetectionLostReason.TargetDestroyed, lost.LostReason);
            Assert.IsFalse(lost.IsTargetAlive);
            Assert.AreEqual("w1", lost.Snapshot.GetIdentifier());
            Assert.AreEqual("Wrapped", lost.Snapshot.GetDetectionType());
            Assert.AreEqual(new Vector3(1f, 2f, 3f), lost.Snapshot.GetCenterPosition());
        }

        [UnityTest]
        public IEnumerator ADestroyedUnityObject_ProducesALostDetectionWithItsData()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Doomed cube";
            cube.transform.position = new Vector3(4f, 0f, 0f);
            DetectableEntity entity = cube.AddComponent<DetectableEntity>();
            var source = new ScriptedCollection();
            source.AllObjects.Add(entity);
            CreateScriptedSensor(source);

            yield return new WaitForSeconds(RefreshWait);
            string id = entity.GetIdentifier();
            Assert.AreEqual(1, _gained.Count);

            Object.Destroy(cube);
            yield return new WaitForSeconds(RefreshWait);

            Assert.AreEqual(1, _lost.Count);
            Detection lost = _lost[0];
            Assert.AreEqual(id, lost.Snapshot.Identifier);
            Assert.AreEqual("Doomed cube", lost.Snapshot.Name);
            Assert.AreEqual(4f, lost.Snapshot.Position.x, 0.01f);
            Assert.AreEqual(DetectionLostReason.TargetDestroyed, lost.LostReason);
        }

        [UnityTest]
        public IEnumerator TheSameObjectAsANewInstance_IsNotLostAndGainedAgain()
        {
            var source = new ScriptedCollection();
            source.AllObjects.Add(new Wrapper("same", Vector3.zero));
            ScriptedSensor sensor = CreateScriptedSensor(source);

            yield return new WaitForSeconds(RefreshWait);
            Assert.AreEqual(1, _gained.Count);
            int detectionId = sensor.GetCurrentDetections()[0].ID;

            // The other program rebuilt its wrapper. The ID is the same, so it is the same target.
            source.AllObjects.Clear();
            source.AllObjects.Add(new Wrapper("same", new Vector3(5f, 0f, 0f)));
            yield return new WaitForSeconds(RefreshWait);

            Assert.AreEqual(1, _gained.Count, "No second new detection.");
            Assert.IsEmpty(_lost, "No false loss.");
            Detection detection = sensor.GetCurrentDetections()[0];
            Assert.AreEqual(detectionId, detection.ID, "The detection ID must stay.");
            Assert.AreEqual(5f, detection.Snapshot.Position.x, 0.01f, "The snapshot follows the new instance.");
        }

        [UnityTest]
        public IEnumerator ATargetThatFailsTheConditions_IsLostForThatReason()
        {
            var source = new ScriptedCollection();
            source.AllObjects.Add(new Wrapper("w", Vector3.zero));
            CreateScriptedSensor(source);

            yield return new WaitForSeconds(RefreshWait);
            SwitchableCondition.Passes = false;
            yield return new WaitForSeconds(RefreshWait);

            Assert.AreEqual(1, _lost.Count);
            Assert.AreEqual(DetectionLostReason.ConditionsFailed, _lost[0].LostReason);
            Assert.IsTrue(_lost[0].IsTargetAlive);
        }

        [UnityTest]
        public IEnumerator SwitchingTheSensorOff_LosesWithThatReason_AndKeepsTheSnapshot()
        {
            var source = new ScriptedCollection();
            source.AllObjects.Add(new Wrapper("w", new Vector3(0f, 7f, 0f)));
            ScriptedSensor sensor = CreateScriptedSensor(source);

            yield return new WaitForSeconds(RefreshWait);
            sensor.enabled = false;

            Assert.AreEqual(1, _lost.Count);
            Assert.AreEqual(DetectionLostReason.SensorDisabled, _lost[0].LostReason);
            Assert.AreEqual(7f, _lost[0].Snapshot.Position.y, 0.01f);
        }

        [UnityTest]
        public IEnumerator TwoObjectsWithTheSameId_WarnOnce_AndOnlyTheFirstIsUsed()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("two objects with the identifier"));
            var source = new ScriptedCollection();
            source.AllObjects.Add(new Wrapper("twin", Vector3.zero));
            source.AllObjects.Add(new Wrapper("twin", Vector3.one));
            CreateScriptedSensor(source);

            yield return new WaitForSeconds(RefreshWait);
            yield return new WaitForSeconds(RefreshWait);

            Assert.AreEqual(1, _gained.Count);
        }

        [UnityTest]
        public IEnumerator AnObjectWithNoIdentifier_IsIgnoredWithOneWarning()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no identifier"));
            var source = new ScriptedCollection();
            source.AllObjects.Add(new Wrapper(string.Empty, Vector3.zero));
            CreateScriptedSensor(source);

            yield return new WaitForSeconds(RefreshWait);
            yield return new WaitForSeconds(RefreshWait);

            Assert.IsEmpty(_gained);
        }

        [UnityTest]
        public IEnumerator DetectableEntity_RegistersWhileEnabled_AndTheDefaultCollectionFindsIt()
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(cube);
            DetectableEntity entity = cube.AddComponent<DetectableEntity>();
            Assert.IsTrue(DetectableRegistry.IsRegistered(entity), "Registered when it is enabled.");

            var collection = new AllSimpleObjectsCollection(~0);
            yield return null;
            CollectionAssert.Contains(collection.AllObjects, entity);

            entity.enabled = false;
            Assert.IsFalse(DetectableRegistry.IsRegistered(entity), "Unregistered when it is disabled.");

            Object.Destroy(cube);
            yield return null;
            Assert.IsFalse(DetectableRegistry.IsRegistered(entity));
            collection.SetEnable(false);
        }

        [UnityTest]
        public IEnumerator ADetectableThatRegistersItself_IsFoundByASensor()
        {
            var wrapper = new Wrapper("registered", new Vector3(0f, 0f, 5f));
            DetectableRegistry.Register(wrapper);
            var settings = ScriptableObject.CreateInstance<SensorSettings>();
            typeof(SensorSettings).GetField("_refreshRate", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, SensorSettings.MinRefreshRate);
            settings.Root = new SwitchableCondition();
            _created.Add(settings);

            var owner = new GameObject("Sensor");
            owner.SetActive(false);
            Sensor sensor = owner.AddComponent<Sensor>();
            typeof(Sensor).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(sensor, settings);
            sensor.OnNewDetectionEvent += _gained.Add;
            _created.Add(owner);
            owner.SetActive(true);

            yield return new WaitForSeconds(RefreshWait + 0.6f);
            DetectableRegistry.Unregister(wrapper);

            Assert.AreEqual(1, _gained.Count);
            Assert.AreEqual("registered", _gained[0].TargetId);
        }
    }
}
