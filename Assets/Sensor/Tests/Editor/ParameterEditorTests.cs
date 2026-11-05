using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem.Tests
{
    public class ParameterEditorTests
    {
        private enum Mode { One, Two, Three }

        [Flags]
        private enum Permissions { None = 0, Read = 1, Write = 2, Execute = 4, All = 7 }

        [Serializable]
        private class Inner
        {
            public float Value = 1f;
            public string Name = "inner";
        }

        [Serializable]
        private struct Pair
        {
            public int A;
            public int B;
        }

        private class BaseSample : Condition
        {
            public int FromBase = 1;

            public override bool Evaluate(IDetectable target, in SensorContext context) => true;
        }

        /// <summary>A condition written the way a developer would, with no editor support of any kind.</summary>
        private class Sample : BaseSample
        {
            public bool Flag = true;
            public int Count = 3;
            [Min(0.5f)] public float Minimum = 2f;
            [UnityEngine.Range(0f, 10f)] public float Ranged = 5f;
            [UnityEngine.Range(1, 4)] public int RangedInt = 2;
            public double Precise = 1.5;
            public string Title = "hello";
            public Mode Mode = Mode.Two;
            public Permissions Permissions = Permissions.Read;
            public Vector2 V2 = new Vector2(1, 2);
            public Vector3 V3 = new Vector3(1, 2, 3);
            public Color Tint = Color.red;
            public LayerMask Layers = 1;
            [Tooltip("Some numbers")] public List<float> Numbers = new List<float> { 1f, 2f };
            public List<Inner> Inners = new List<Inner> { new Inner { Value = 4f } };
            public Pair[] Pairs = { new Pair { A = 1, B = 2 } };
            public Inner Nested = new Inner();
            [SerializeField] private int _privateButSerialized = 7;

            private int _notSerialized = 9;
            [NonSerialized] public int Skipped = 1;
            [HideInInspector] public int Hidden = 1;
            [SerializeReference] public Condition Child;
            public static int StaticField = 1;
            public readonly int ReadOnlyField = 1;
            public UnityEngine.Object Reference;
            public Dictionary<string, int> Unsupported = new Dictionary<string, int>();

            public int PrivateButSerialized => _privateButSerialized;
            public int NotSerialized => _notSerialized;
        }

        private static void Click(Button button)
        {
            // Pressing a button needs a panel and pointer events, so run the handler it holds.
            FieldInfo field = typeof(Clickable)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(candidate => candidate.FieldType == typeof(Action) && candidate.GetValue(button.clickable) != null);
            ((Action)field.GetValue(button.clickable)).Invoke();
        }

        private static Button FindButton(VisualElement root, string text)
        {
            return root.Query<Button>().ToList().First(button => button.text == text);
        }

        [Test]
        public void CreatesAControlForEveryEditableField_AndNothingElse()
        {
            ParameterPanel panel = ParameterEditor.Build(new Sample());
            HashSet<string> paths = panel.Paths.ToHashSet();

            foreach (string expected in new[]
            {
                "FromBase", "Flag", "Count", "Minimum", "Ranged", "RangedInt", "Precise", "Title", "Mode", "Permissions",
                "V2", "V3", "Tint", "Layers", "Numbers[0]", "Numbers[1]", "Inners[0].Value", "Inners[0].Name",
                "Pairs[0].A", "Pairs[0].B", "Nested.Value", "Nested.Name", "_privateButSerialized",
            })
            {
                Assert.Contains(expected, paths.ToList(), expected);
            }

            foreach (string unexpected in new[] { "_notSerialized", "Skipped", "Hidden", "Child", "StaticField", "ReadOnlyField", "Reference", "Unsupported" })
            {
                Assert.IsFalse(paths.Contains(unexpected), $"{unexpected} should not get a control.");
            }
        }

        [Test]
        public void EachTypeGetsItsOwnKindOfControl()
        {
            ParameterPanel panel = ParameterEditor.Build(new Sample());

            Assert.IsNotNull(panel.Q<Toggle>(), "bool");
            Assert.IsNotNull(panel.Q<IntegerField>(), "int");
            Assert.IsNotNull(panel.Q<FloatField>(), "float");
            Assert.IsNotNull(panel.Q<Slider>(), "float with [Range]");
            Assert.IsNotNull(panel.Q<SliderInt>(), "int with [Range]");
            Assert.IsNotNull(panel.Q<DoubleField>(), "double");
            Assert.IsNotNull(panel.Q<TextField>(), "string");
            Assert.IsNotNull(panel.Q<EnumField>(), "enum");
            Assert.IsNotNull(panel.Q<Vector2Field>(), "Vector2");
            Assert.IsNotNull(panel.Q<Vector3Field>(), "Vector3");
            Assert.IsNotNull(panel.Q<Vector4Field>(), "Color, as four numbers");
        }

        [Test]
        public void UnsupportedAndSceneReferenceFields_BecomeLabels()
        {
            ParameterPanel panel = ParameterEditor.Build(new Sample());
            List<string> labels = panel.Query<Label>().ToList().Select(label => label.text).ToList();

            Assert.That(labels.Any(text => text.Contains("not editable here")), "Dictionary should be a label.");
            Assert.That(labels.Any(text => text.StartsWith("Reference: None")), "An object reference should be shown, not edited.");
        }

        [Test]
        public void EditingAControl_WritesTheField_AndTellsTheOwner()
        {
            var sample = new Sample();
            int changes = 0;
            ParameterPanel panel = ParameterEditor.Build(sample, () => changes++);

            Assert.IsTrue(panel.TrySet("Count", 9));
            Assert.IsTrue(panel.TrySet("Flag", false));
            Assert.IsTrue(panel.TrySet("Title", "changed"));
            Assert.IsTrue(panel.TrySet("Precise", 2.5));
            Assert.IsTrue(panel.TrySet("Mode", Mode.Three));
            Assert.IsTrue(panel.TrySet("V3", new Vector3(7, 8, 9)));
            Assert.IsTrue(panel.TrySet("_privateButSerialized", 70));

            Assert.AreEqual(9, sample.Count);
            Assert.IsFalse(sample.Flag);
            Assert.AreEqual("changed", sample.Title);
            Assert.AreEqual(2.5, sample.Precise);
            Assert.AreEqual(Mode.Three, sample.Mode);
            Assert.AreEqual(new Vector3(7, 8, 9), sample.V3);
            Assert.AreEqual(70, sample.PrivateButSerialized);
            Assert.AreEqual(7, changes);
            Assert.IsFalse(panel.TrySet("NoSuchField", 1));
        }

        [Test]
        public void MinAndRange_ClampWhatIsEntered_AndTheControlShowsTheClampedValue()
        {
            var sample = new Sample();
            ParameterPanel panel = ParameterEditor.Build(sample);

            panel.TrySet("Minimum", 0.1f);
            panel.TrySet("Ranged", 99f);
            panel.TrySet("RangedInt", 9);

            Assert.AreEqual(0.5f, sample.Minimum);
            Assert.AreEqual(10f, sample.Ranged);
            Assert.AreEqual(4, sample.RangedInt);
            Assert.AreEqual(10f, panel.Q<Slider>().value, "The slider should show the clamped value.");

            panel.TrySet("Ranged", -5f);
            Assert.AreEqual(0f, sample.Ranged);
        }

        [Test]
        public void FlagsColorAndLayerMask_CanBeEdited()
        {
            var sample = new Sample();
            ParameterPanel panel = ParameterEditor.Build(sample);

            panel.TrySet("Permissions", Permissions.Read | Permissions.Execute);
            panel.TrySet("Tint", new Vector4(0f, 1f, 0f, 1f));
            panel.TrySet("Layers", 6);

            Assert.AreEqual(Permissions.Read | Permissions.Execute, sample.Permissions);
            Assert.AreEqual(Color.green, sample.Tint);
            Assert.AreEqual(6, sample.Layers.value);
        }

        [Test]
        public void ChangingAFieldOfAStructInAnArray_WritesTheStructBack()
        {
            var sample = new Sample();
            ParameterPanel panel = ParameterEditor.Build(sample);

            panel.TrySet("Pairs[0].A", 5);

            Assert.AreEqual(5, sample.Pairs[0].A);
            Assert.AreEqual(2, sample.Pairs[0].B, "The other field must be kept.");
        }

        [Test]
        public void ChangingAFieldOfAClassInAList_ChangesThatItem()
        {
            var sample = new Sample();
            ParameterPanel panel = ParameterEditor.Build(sample);

            panel.TrySet("Inners[0].Value", 12f);
            panel.TrySet("Nested.Name", "renamed");

            Assert.AreEqual(12f, sample.Inners[0].Value);
            Assert.AreEqual("renamed", sample.Nested.Name);
        }

        [Test]
        public void Refresh_ShowsValuesChangedByCode()
        {
            var sample = new Sample();
            ParameterPanel panel = ParameterEditor.Build(sample);

            sample.Count = 50;
            sample.Title = "from code";
            panel.Refresh();

            Assert.AreEqual(50, panel.Query<IntegerField>().ToList().First(field => field.label == "Count").value);
            Assert.AreEqual("from code", panel.Query<TextField>().ToList().First(field => field.label == "Title").value);
        }

        [Test]
        public void Refresh_RebuildsAListWhenItsCountChangedByCode()
        {
            var sample = new Sample();
            ParameterPanel panel = ParameterEditor.Build(sample);
            Assert.IsFalse(panel.Paths.Contains("Numbers[2]"));

            sample.Numbers.Add(3f);
            panel.Refresh();
            Assert.Contains("Numbers[2]", panel.Paths.ToList());

            sample.Numbers.Clear();
            panel.Refresh();
            Assert.IsFalse(panel.Paths.Contains("Numbers[0]"));
        }

        [Test]
        public void ListButtons_AddAndRemoveItems_ForListsAndArrays()
        {
            var sample = new Sample();
            int changes = 0;
            ParameterPanel panel = ParameterEditor.Build(sample, () => changes++);

            CollapsibleGroup numbers = panel.Query<CollapsibleGroup>().ToList().First(foldout => foldout.text.StartsWith("Numbers"));
            Click(FindButton(numbers, "+ Add"));
            Assert.AreEqual(3, sample.Numbers.Count);
            Assert.AreEqual(0f, sample.Numbers[2]);
            Assert.Contains("Numbers[2]", panel.Paths.ToList());

            Click(FindButton(numbers, "✕"));
            Assert.AreEqual(2, sample.Numbers.Count);
            Assert.AreEqual(2f, sample.Numbers[0], "Removing the first item should leave the second.");
            Assert.AreEqual(2, changes);

            CollapsibleGroup pairs = panel.Query<CollapsibleGroup>().ToList().First(foldout => foldout.text.StartsWith("Pairs"));
            Click(FindButton(pairs, "+ Add"));
            Assert.AreEqual(2, sample.Pairs.Length, "Arrays are replaced with a longer array.");
            Click(FindButton(pairs, "✕"));
            Click(FindButton(pairs, "✕"));
            Assert.AreEqual(0, sample.Pairs.Length);
        }

        [Test]
        public void TheTooltipAttributeBecomesTheTooltip()
        {
            ParameterPanel panel = ParameterEditor.Build(new Sample());
            CollapsibleGroup numbers = panel.Query<CollapsibleGroup>().ToList().First(foldout => foldout.text.StartsWith("Numbers"));

            Assert.AreEqual("Some numbers", numbers.tooltip);
        }

        [TestCase("MinMovingVelocity", "Min Moving Velocity")]
        [TestCase("_refreshRate", "Refresh Rate")]
        [TestCase("m_Size", "Size")]
        [TestCase("<Radius>k__BackingField", "Radius")]
        [TestCase("HTTPServer", "HTTP Server")]
        [TestCase("x", "X")]
        [TestCase("", "")]
        public void Nicify_TurnsFieldNamesIntoLabels(string name, string expected)
        {
            Assert.AreEqual(expected, ParameterEditor.Nicify(name));
        }

        [Test]
        public void GetEditableFields_ListsBaseClassFieldsFirst()
        {
            string[] names = ParameterEditor.GetEditableFields(typeof(Sample)).Select(field => field.Name).ToArray();

            Assert.AreEqual("FromBase", names[0]);
            Assert.AreEqual("Flag", names[1]);
        }

        [Test]
        public void ABuiltInConditionWithNestedLists_IsFullyEditable_AndRespectsItsRanges()
        {
            var condition = new InAbsoluteRangesWithProbabilitiesPerEntity
            {
                EntityTypeToDistanceProbabilities = new List<InAbsoluteRangesWithProbabilitiesPerEntity.EntityRanges>
                {
                    new InAbsoluteRangesWithProbabilitiesPerEntity.EntityRanges
                    {
                        EntityType = EntityType.Drone,
                        DistanceProbabilities = new List<DistanceProbability> { new DistanceProbability { Distance = 10f, Probability = 50f } },
                    },
                },
            };
            ParameterPanel panel = ParameterEditor.Build(condition);

            panel.TrySet("EntityTypeToDistanceProbabilities[0].DistanceProbabilities[0].Probability", 150f);
            panel.TrySet("EntityTypeToDistanceProbabilities[0].EntityType", EntityType.Person);
            panel.TrySet("MaxDetectionAttempts", -3);

            Assert.AreEqual(100f, condition.EntityTypeToDistanceProbabilities[0].DistanceProbabilities[0].Probability, "[Range(0, 100)] should clamp.");
            Assert.AreEqual(EntityType.Person, condition.EntityTypeToDistanceProbabilities[0].EntityType);
            Assert.AreEqual(0, condition.MaxDetectionAttempts, "[Min(0)] should clamp.");
        }

        [Test]
        public void EveryBuiltInConditionAndInjector_BuildsWithoutErrors()
        {
            object[] everything =
            {
                new And(), new Or(), new Not(), new InFrustum(), new InViewPort(), new IsAngleClamped(), new WithRaycast(),
                new IsMoving(), new InAbsoluteRangesWithProbabilities(), new InHorizontalRangesWithProbabilities(),
                new InAbsoluteRangesWithProbabilitiesPerEntity(), new FakeRandomReflectedEntitiesDetections(), new FakeRandomDetectionInViewPort(),
            };

            foreach (object instance in everything)
            {
                ParameterPanel panel = null;
                Assert.DoesNotThrow(() => panel = ParameterEditor.Build(instance), instance.GetType().Name);
                Assert.DoesNotThrow(() => panel.Refresh(), instance.GetType().Name);
            }

            // Operators have no values of their own, since their children are shown as nodes.
            Assert.IsEmpty(ParameterEditor.Build(new And()).Paths);
            Assert.IsEmpty(ParameterEditor.Build(new Not()).Paths);
            CollectionAssert.IsSupersetOf(ParameterEditor.Build(new InFrustum()).Paths, new[] { "HorizontalFov", "VerticalFov", "MinDistance", "MaxDistance" });
            Assert.Contains("MinMovingVelocity", ParameterEditor.Build(new IsMoving()).Paths.ToList());
        }

        [Test]
        public void NullInstance_GivesAnEmptyPanel()
        {
            Assert.IsEmpty(ParameterEditor.Build(null).Paths);
        }
    }
}
