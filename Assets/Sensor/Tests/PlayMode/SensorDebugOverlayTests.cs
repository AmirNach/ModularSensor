using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SensorSystem.Tests
{
    public class SensorDebugOverlayTests
    {
        // Longer than the collection's 1s scan plus one 0.5s sensor refresh.
        private const float SettleSeconds = 1.8f;
        private static readonly Vector3 InFront = new Vector3(0f, 0f, 20f);

        private readonly List<Object> _created = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            SensorDebugOverlay.AvailabilityCheck = () => true;
        }

        [TearDown]
        public void TearDown()
        {
            SensorDebugOverlay.AvailabilityCheck = () => Debug.isDebugBuild;

            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }
            _created.Clear();

            GameObject eventSystem = GameObject.Find("EventSystem (sensor debug)");
            if (eventSystem != null) Object.DestroyImmediate(eventSystem);
            if (ObjectsProvider.Instance != null) Object.DestroyImmediate(ObjectsProvider.Instance.gameObject);
        }

        private static void SetField(object owner, string field, object value)
        {
            owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        }

        private Sensor CreateSensor(Condition root, Vector3 position = default)
        {
            var settings = ScriptableObject.CreateInstance<SensorSettings>();
            SetField(settings, "_refreshRate", SensorSettings.MinRefreshRate);
            settings.Root = root;
            _created.Add(settings);

            var owner = new GameObject("Test sensor");
            owner.SetActive(false);
            owner.transform.position = position;
            Sensor sensor = owner.AddComponent<Sensor>();
            SetField(sensor, "_settings", settings);
            _created.Add(owner);
            owner.SetActive(true);
            return sensor;
        }

        private GameObject CreateTarget(string name, Vector3 position)
        {
            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = position;
            target.AddComponent<DetectableEntity>();
            _created.Add(target);
            return target;
        }

        private SensorDebugOverlay CreateOverlay()
        {
            var owner = new GameObject("Test overlay");
            owner.SetActive(false);
            SensorDebugOverlay overlay = owner.AddComponent<SensorDebugOverlay>();
            _created.Add(owner);
            owner.SetActive(true);
            return overlay;
        }

        private static IEnumerator WaitForUi(SensorDebugOverlay overlay)
        {
            float giveUp = Time.time + 3f;
            while (overlay.Panel == null && Time.time < giveUp)
            {
                yield return null;
            }
            Assert.IsNotNull(overlay.Panel, "The overlay should have built its UI.");
        }

        private static void Click(Button button)
        {
            // Pressing a button needs pointer events, so run the handler the button holds.
            FieldInfo field = typeof(Clickable)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(candidate => candidate.FieldType == typeof(Action) && candidate.GetValue(button.clickable) != null);
            ((Action)field.GetValue(button.clickable)).Invoke();
        }

        [UnityTest]
        public IEnumerator InAReleaseBuild_TheOverlaySwitchesItselfOff()
        {
            SensorDebugOverlay.AvailabilityCheck = () => false;

            SensorDebugOverlay overlay = CreateOverlay();
            yield return null;
            yield return null;

            Assert.IsFalse(overlay.IsRunning);
            Assert.IsNull(overlay.Panel);
            Assert.IsFalse(SensorDebug.HasReceiver);
            Assert.IsFalse(SensorDebug.Dispatch(new ToggleOverlayCommand()), "Dispatching with no overlay is harmless.");
        }

        [UnityTest]
        public IEnumerator ToggleCommand_OpensAndClosesThePanel()
        {
            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);

            Assert.IsTrue(overlay.IsRunning);
            Assert.IsFalse(overlay.IsOpen);
            Assert.AreEqual(DisplayStyle.None, overlay.Panel.resolvedStyle.display);

            Assert.IsTrue(SensorDebug.Dispatch(new ToggleOverlayCommand()));
            Assert.IsTrue(overlay.IsOpen);
            Assert.AreEqual(DisplayStyle.Flex, overlay.Panel.style.display.value);

            SensorDebug.Dispatch(new ToggleOverlayCommand());
            Assert.IsFalse(overlay.IsOpen);
            Assert.AreEqual(DisplayStyle.None, overlay.Panel.style.display.value);
        }

        [UnityTest]
        public IEnumerator TheOnScreenButton_DispatchesTheSameCommand()
        {
            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);

            Click(overlay.ToggleButton);
            Assert.IsTrue(overlay.IsOpen);

            Click(overlay.ToggleButton);
            Assert.IsFalse(overlay.IsOpen);
        }

        [UnityTest]
        public IEnumerator Disabling_TheOverlay_StopsItListening()
        {
            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);
            Assert.IsTrue(SensorDebug.HasReceiver);

            overlay.enabled = false;

            Assert.IsFalse(SensorDebug.HasReceiver);
            Assert.IsFalse(SensorDebug.Dispatch(new OpenOverlayCommand()));
        }

        [UnityTest]
        public IEnumerator FocusCommands_OpenThePanelOnThatSensorOrDetector()
        {
            Sensor first = CreateSensor(new InFrustum());
            Sensor second = CreateSensor(new IsMoving());
            var detectorOwner = new GameObject("Test detector");
            detectorOwner.SetActive(false);
            Detector detector = detectorOwner.AddComponent<Detector>();
            SetField(detector, "_detectorSettings", ScriptableObject.CreateInstance<DetectorSettings>());
            SetField(detector, "_sensors", new List<Sensor> { first, second });
            _created.Add(detectorOwner);
            detectorOwner.SetActive(true);

            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);

            SensorDebug.Dispatch(new FocusSensorCommand(second));
            Assert.IsTrue(overlay.IsOpen);
            Assert.AreSame(second, overlay.Panel.Selected);
            Assert.IsNotNull(overlay.Root.Q<RuntimeSensorView>(), "A sensor view should be showing.");

            SensorDebug.Dispatch(new FocusDetectorCommand(detector));
            Assert.AreSame(detector, overlay.Panel.Selected);
            Assert.IsNotNull(overlay.Root.Q<RuntimeDetectorView>(), "A detector view should be showing.");
        }

        [UnityTest]
        public IEnumerator OpeningTheOverlay_ShowsTheSensorRightAway_EvenAfterClosingAndReopening()
        {
            Sensor sensor = CreateSensor(new InFrustum());
            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);

            SensorDebug.Dispatch(new OpenOverlayCommand());
            Assert.AreSame(sensor, overlay.Panel.Selected);
            Assert.IsNotNull(overlay.Root.Q<RuntimeSensorView>(), "The first open should show the sensor.");

            SensorDebug.Dispatch(new CloseOverlayCommand());
            Assert.IsNull(overlay.Root.Q<RuntimeSensorView>());

            SensorDebug.Dispatch(new OpenOverlayCommand());
            Assert.AreSame(sensor, overlay.Panel.Selected);
            Assert.IsNotNull(overlay.Root.Q<RuntimeSensorView>(), "Reopening should show the sensor again without switching to another one.");

            // and through the on-screen button, which is how QA does it
            Click(overlay.ToggleButton);
            Click(overlay.ToggleButton);
            Assert.IsNotNull(overlay.Root.Q<RuntimeSensorView>());
        }

        [UnityTest]
        public IEnumerator ClosingThePanel_StopsDebuggingTheSensor()
        {
            CreateTarget("Target", InFront);
            Sensor sensor = CreateSensor(new InFrustum());
            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);

            SensorDebug.Dispatch(new FocusSensorCommand(sensor));
            yield return new WaitForSeconds(SettleSeconds);
            sensor.DebugTarget = sensor.LastCandidates.First();

            SensorDebug.Dispatch(new CloseOverlayCommand());
            yield return null;

            Assert.IsNull(sensor.DebugTarget, "A closed overlay must not leave debug tracking switched on.");
            Assert.AreEqual(0, overlay.Panel.ContentRoot.childCount);
        }

        [UnityTest]
        public IEnumerator EditingAValueInTheOverlay_ChangesWhatTheSensorDetects_AndResetRestoresIt()
        {
            CreateTarget("Target", InFront);
            Sensor sensor = CreateSensor(new InFrustum { MaxDistance = 100f });
            var events = new List<string>();
            sensor.OnNewDetectionEvent += _ => events.Add("new");
            sensor.OnLostDetectionEvent += _ => events.Add("lost");

            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);
            SensorDebug.Dispatch(new FocusSensorCommand(sensor));
            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new" }, events);

            // Type a new range into the control, the way QA would.
            NodeCard card = overlay.Root.Query<NodeCard>().ToList().First(candidate => candidate.Parameters.Paths.Contains("MaxDistance"));
            card.SetExpanded(true);
            FloatField field = card.Query<FloatField>().ToList().First(candidate => candidate.label == "Max Distance");
            field.value = 5f;

            Assert.AreEqual(5f, ((InFrustum)sensor.RuntimeRoot).MaxDistance, "The control should write the sensor's own copy.");
            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new", "lost" }, events);

            SensorDebug.Dispatch(new ResetSensorValuesCommand(sensor));
            Assert.AreEqual(100f, ((InFrustum)sensor.RuntimeRoot).MaxDistance);
            yield return new WaitForSeconds(SettleSeconds);
            CollectionAssert.AreEqual(new[] { "new", "lost", "new" }, events);

            NodeCard rebuilt = overlay.Root.Query<NodeCard>().ToList().First(candidate => candidate.Parameters.Paths.Contains("MaxDistance"));
            Assert.AreEqual(100f, rebuilt.Parameters.Get("MaxDistance"), "The overlay should show the reset value.");
        }

        [UnityTest]
        public IEnumerator AConditionCardShowsWhatHappenedToThePickedObject()
        {
            CreateTarget("Target", InFront);
            Sensor sensor = CreateSensor(new And { Children = new List<Condition> { new IsMoving { MinMovingVelocity = 1000f }, new InFrustum() } });
            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);
            SensorDebug.Dispatch(new FocusSensorCommand(sensor));
            yield return new WaitForSeconds(SettleSeconds);

            sensor.DebugTarget = sensor.LastCandidates.First();
            yield return new WaitForSeconds(SettleSeconds);

            List<string> statuses = overlay.Root.Query<Label>(className: "sensor-status").ToList().Select(label => label.text).ToList();
            Assert.That(statuses.Any(text => text.Contains("Failed")), "The failing IsMoving should say so.");
            Assert.That(statuses.Any(text => text.Contains("Not reached")), "InFrustum is skipped after the failure.");
        }

        [UnityTest]
        public IEnumerator TheOverlayCreatesAnEventSystemWhenTheSceneHasNone()
        {
            Assert.IsNull(GameObject.Find("EventSystem (sensor debug)"));

            SensorDebugOverlay overlay = CreateOverlay();
            yield return WaitForUi(overlay);

            string second = SensorDebugEventSystem.EnsureExists();
            Assert.That(second == "present" || second == "unavailable", $"A second call should find it, but said: {second}");
        }
    }
}
