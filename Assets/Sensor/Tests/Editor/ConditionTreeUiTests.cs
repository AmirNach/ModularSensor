using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace SensorSystem.Tests
{
    public class ConditionTreeUiTests
    {
        private SensorSettings _settings;
        private ConditionTreeWindow _window;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<SensorSettings>();
            _settings.name = "Test settings";
        }

        [TearDown]
        public void TearDown()
        {
            if (_window != null) _window.Close();
            Object.DestroyImmediate(_settings);
        }

        private IEnumerator OpenWindow()
        {
            ConditionTreeWindow.Open(_settings);
            _window = EditorWindow.GetWindow<ConditionTreeWindow>();
            // The tree builds a step after attaching to the panel.
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }
        }

        private List<VisualElement> Cards(string kindClass = "sensor-card")
        {
            return _window.rootVisualElement.Query(className: kindClass).ToList();
        }

        [UnityTest]
        public IEnumerator Window_BuildsACardForEveryNode()
        {
            _settings.Root = new And
            {
                Children = new List<Condition> { new InFrustum(), new Or { Children = new List<Condition> { new IsMoving(), new Not { Child = new WithRaycast() } } } },
            };
            yield return OpenWindow();

            Assert.AreEqual(6, Cards().Count);
            Assert.AreEqual(1, Cards("sensor-card--and").Count);
            Assert.AreEqual(1, Cards("sensor-card--or").Count);
            Assert.AreEqual(1, Cards("sensor-card--not").Count);
            Assert.AreEqual(3, Cards("sensor-card--leaf").Count);
        }

        [UnityTest]
        public IEnumerator Window_ShowsTheExpressionPreview()
        {
            _settings.Root = new And { Children = new List<Condition> { new InFrustum(), new IsMoving() } };
            yield return OpenWindow();

            var preview = _window.rootVisualElement.Q<Label>(className: "sensor-preview");
            Assert.AreEqual("In Frustum AND Is Moving", preview.text);
        }

        [UnityTest]
        public IEnumerator Window_EmptyTree_OffersToAddTheFirstCondition()
        {
            yield return OpenWindow();

            Assert.AreEqual(0, Cards().Count);
            Assert.IsNotNull(_window.rootVisualElement.Q(className: "sensor-empty"));
        }

        [UnityTest]
        public IEnumerator Window_ListsInjectors()
        {
            _settings.Injectors.Add(new FakeRandomReflectedEntitiesDetections());
            yield return OpenWindow();

            Assert.AreEqual(1, Cards("sensor-card--injector").Count);
        }

        [UnityTest]
        public IEnumerator Cards_WarnAboutEmptyOperators()
        {
            _settings.Root = new And { Children = new List<Condition> { new Or() } };
            yield return OpenWindow();

            var warnings = _window.rootVisualElement.Query<Label>(className: "sensor-warning-text").ToList();
            Assert.That(warnings.Exists(label => label.text.Contains("always false")));
        }
    }
}
