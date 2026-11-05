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
    public class SensorPlayTests
    {
        // Longer than the collection's 1s scan plus one 0.5s sensor refresh.
        private const float SettleSeconds = 1.8f;
        private static readonly Vector3 InFront = new Vector3(0f, 0f, 20f);
        private static readonly Vector3 Behind = new Vector3(0f, 0f, -20f);

        private readonly List<Object> _created = new List<Object>();
        private readonly List<string> _events = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
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

        private static void SetField(object owner, string field, object value)
        {
            owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        }

        private SensorSettings CreateSettings(Condition root)
        {
            var settings = ScriptableObject.CreateInstance<SensorSettings>();
            SetField(settings, "_refreshRate", SensorSettings.MinRefreshRate);
            settings.Root = root;
            _created.Add(settings);
            return settings;
        }

        private GameObject CreateTarget(Vector3 position)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.transform.position = position;
            target.AddComponent<DetectableEntity>();
            _created.Add(target);
            return target;
        }

        private Sensor CreateSensor(SensorSettings settings, Vector3 position = default)
        {
            var owner = new GameObject("Sensor");
            owner.SetActive(false);
            owner.transform.position = position;
            Sensor sensor = owner.AddComponent<Sensor>();
            SetField(sensor, "_settings", settings);
            sensor.OnNewDetectionEvent += _ => _events.Add("new");
            sensor.OnLostDetectionEvent += _ => _events.Add("lost");
            _created.Add(owner);
            owner.SetActive(true);
            return sensor;
        }

        [UnityTest]
        public IEnumerator Sensor_GainsAndLosesTargetAsItMoves()
        {
            GameObject target = CreateTarget(InFront);
            CreateSensor(CreateSettings(new InFrustum()));

            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new" }, _events);

            target.transform.position = Behind;
            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new", "lost" }, _events);
        }

        [UnityTest]
        public IEnumerator Sensor_IntersectingConditions_NoFalseDetectionAtStartup()
        {
            // Passes the first child and fails the second, which used to fire new then lost on start.
            CreateTarget(InFront);
            CreateSensor(CreateSettings(new And
            {
                Children = new List<Condition> { new InFrustum(), new IsMoving { MinMovingVelocity = 1000f } },
            }));

            yield return new WaitForSeconds(SettleSeconds);
            Assert.IsEmpty(_events);
        }

        [UnityTest]
        public IEnumerator Sensor_ReEnabled_ResumesDetecting()
        {
            CreateTarget(InFront);
            Sensor sensor = CreateSensor(CreateSettings(new InFrustum()));

            yield return new WaitForSeconds(SettleSeconds);
            sensor.enabled = false;
            CollectionAssert.AreEqual(new[] { "new", "lost" }, _events);

            sensor.enabled = true;
            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new", "lost", "new" }, _events);
        }

        [UnityTest]
        public IEnumerator Sensor_IgnoresItself()
        {
            var settings = CreateSettings(new InAbsoluteRangesWithProbabilities
            {
                DistanceProbabilities = new List<DistanceProbability> { new DistanceProbability { Distance = 100f, Probability = 100f } },
            });

            GameObject self = CreateTarget(Vector3.zero);
            self.SetActive(false);
            Sensor sensor = self.AddComponent<Sensor>();
            SetField(sensor, "_settings", settings);
            sensor.OnNewDetectionEvent += _ => _events.Add("new");
            self.SetActive(true);

            yield return new WaitForSeconds(SettleSeconds);
            Assert.IsEmpty(_events);
        }

        [UnityTest]
        public IEnumerator Sensor_SurvivesDestroyedTarget()
        {
            GameObject target = CreateTarget(InFront);
            CreateSensor(CreateSettings(new InFrustum()));

            yield return new WaitForSeconds(SettleSeconds);
            Object.Destroy(target);
            yield return new WaitForSeconds(SettleSeconds);

            CollectionAssert.AreEqual(new[] { "new", "lost" }, _events);
        }

        [UnityTest]
        public IEnumerator Sensors_SharingSettings_DoNotShareTree()
        {
            SensorSettings settings = CreateSettings(new InFrustum());
            Sensor first = CreateSensor(settings);
            Sensor second = CreateSensor(settings);

            yield return null;

            Assert.AreNotSame(first.RuntimeRoot, second.RuntimeRoot);
            Assert.AreNotSame(settings.Root, first.RuntimeRoot);
        }

        [UnityTest]
        public IEnumerator Detector_DisablingWithActiveDetections_ReportsLossWithoutErrors()
        {
            CreateTarget(InFront);
            Sensor sensor = CreateSensor(CreateSettings(new InFrustum()));
            var detectorEvents = new List<string>();
            Detector detector = CreateDetector(sensor, detectorEvents);

            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new" }, detectorEvents);

            // Any exception logged here fails the test.
            detector.enabled = false;
            CollectionAssert.AreEqual(new[] { "new", "lost" }, detectorEvents);
            Assert.IsFalse(sensor.enabled);
        }

        [UnityTest]
        public IEnumerator Detector_StartedAfterSensor_PicksUpExistingDetections()
        {
            CreateTarget(InFront);
            Sensor sensor = CreateSensor(CreateSettings(new InFrustum()));
            yield return new WaitForSeconds(SettleSeconds);

            var detectorEvents = new List<string>();
            CreateDetector(sensor, detectorEvents);
            yield return new WaitForSeconds(SettleSeconds);

            CollectionAssert.AreEqual(new[] { "new" }, detectorEvents);
        }

        [Serializable]
        private class ListInjector : DetectionInjector
        {
            [NonSerialized] public List<IDetectable> Items = new List<IDetectable>();

            public override void Collect(ICollection<IDetectable> detections)
            {
                if (Items == null) return;

                foreach (IDetectable item in Items)
                {
                    detections.Add(item);
                }
            }
        }

        [Serializable]
        private class CountingCondition : Condition
        {
            [NonSerialized] public int Calls;

            public override bool Evaluate(IDetectable target, in SensorContext context)
            {
                Calls++;
                return true;
            }
        }

        private static ListInjector GetInjector(Sensor sensor)
        {
            var injector = (ListInjector)sensor.RuntimeInjectors[0];
            injector.Items ??= new List<IDetectable>();
            return injector;
        }

        [UnityTest]
        public IEnumerator Injected_IsDetectedEvenWhenTheTreeRejectsEverything()
        {
            SensorSettings settings = CreateSettings(new IsMoving { MinMovingVelocity = 1000f });
            settings.Injectors.Add(new ListInjector());
            Sensor sensor = CreateSensor(settings);
            yield return null;

            var fake = new FakeDetectionInstance(InFront, EntityType.Drone);
            GetInjector(sensor).Items.Add(fake);
            yield return new WaitForSeconds(SettleSeconds);

            CollectionAssert.AreEqual(new[] { "new" }, _events);
            Detection detection = sensor.GetCurrentDetections()[0];
            Assert.AreSame(fake, detection.DetectionComponent);
            CollectionAssert.Contains(detection.AllPassedConditions, "List Injector");
        }

        [UnityTest]
        public IEnumerator Injected_WorksWithAnEmptyTree_AndIsLostWhenItStops()
        {
            SensorSettings settings = CreateSettings(null);
            settings.Injectors.Add(new ListInjector());
            Sensor sensor = CreateSensor(settings);
            yield return null;

            var fake = new FakeDetectionInstance(InFront, EntityType.Drone);
            GetInjector(sensor).Items.Add(fake);
            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new" }, _events);

            GetInjector(sensor).Items.Clear();
            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new", "lost" }, _events);
        }

        [UnityTest]
        public IEnumerator DebugTarget_RecordsReachedNodesOnly()
        {
            GameObject target = CreateTarget(InFront);
            Sensor sensor = CreateSensor(CreateSettings(new And
            {
                Children = new List<Condition> { new IsMoving { MinMovingVelocity = 1000f }, new InFrustum() },
            }));
            sensor.DebugTarget = target.GetComponent<DetectableEntity>();

            yield return new WaitForSeconds(SettleSeconds);

            var root = (And)sensor.RuntimeRoot;
            Assert.IsFalse(sensor.DebugResults[root]);
            Assert.IsFalse(sensor.DebugResults[root.Children[0]]);
            Assert.IsFalse(sensor.DebugResults.ContainsKey(root.Children[1]));
        }

        [UnityTest]
        public IEnumerator DebugTarget_DoesNotEvaluateAnyExtraTimes()
        {
            GameObject target = CreateTarget(InFront);
            Sensor watched = CreateSensor(CreateSettings(new CountingCondition()));
            Sensor plain = CreateSensor(CreateSettings(new CountingCondition()));
            watched.DebugTarget = target.GetComponent<DetectableEntity>();

            yield return new WaitForSeconds(SettleSeconds);

            int watchedCalls = ((CountingCondition)watched.RuntimeRoot).Calls;
            int plainCalls = ((CountingCondition)plain.RuntimeRoot).Calls;
            Assert.Greater(plainCalls, 0);
            Assert.AreEqual(plainCalls, watchedCalls);
        }

        [UnityTest]
        public IEnumerator EditingTheRunningTree_ChangesDetections_AndResetToSettingsRestoresThem()
        {
            CreateTarget(InFront);
            var frustum = new InFrustum { MaxDistance = 100f };
            SensorSettings settings = CreateSettings(frustum);
            Sensor sensor = CreateSensor(settings);

            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new" }, _events);

            // Shorten the range on the running copy, the way the QA overlay does.
            Condition before = sensor.RuntimeRoot;
            ((InFrustum)before).MaxDistance = 5f;
            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new", "lost" }, _events);
            Assert.AreEqual(100f, frustum.MaxDistance, "The settings asset must not change.");

            sensor.ResetToSettings();
            Assert.AreNotSame(before, sensor.RuntimeRoot);
            Assert.AreEqual(100f, ((InFrustum)sensor.RuntimeRoot).MaxDistance);

            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new", "lost", "new" }, _events);
        }

        [UnityTest]
        public IEnumerator Detector_ExposesItsSensorsAndWhatItDetects()
        {
            GameObject target = CreateTarget(InFront);
            Sensor sensor = CreateSensor(CreateSettings(new InFrustum()));
            Detector detector = CreateDetector(sensor, new List<string>());

            CollectionAssert.AreEqual(new[] { sensor }, detector.Sensors);

            yield return new WaitForSeconds(SettleSeconds);
            List<Detection> detections = detector.GetCurrentDetections();
            Assert.AreEqual(1, detections.Count);
            Assert.AreSame(target.GetComponent<DetectableEntity>(), detections[0].DetectionComponent);

            target.transform.position = Behind;
            yield return new WaitForSeconds(SettleSeconds);
            Assert.AreEqual(0, detector.GetCurrentDetections().Count);
        }

        private Detector CreateDetector(Sensor sensor, List<string> events)
        {
            var owner = new GameObject("Detector");
            owner.SetActive(false);
            Detector detector = owner.AddComponent<Detector>();
            var detectorSettings = ScriptableObject.CreateInstance<DetectorSettings>();
            _created.Add(detectorSettings);
            _created.Add(owner);
            SetField(detector, "_detectorSettings", detectorSettings);
            SetField(detector, "_sensors", new List<Sensor> { sensor });
            detector.OnNewDetectionFromDetectorEvent += _ => events.Add("new");
            detector.OnLostDetectionFromDetectorEvent += _ => events.Add("lost");
            owner.SetActive(true);
            return detector;
        }
    }
}
