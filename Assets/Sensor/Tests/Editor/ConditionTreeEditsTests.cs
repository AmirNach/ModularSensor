using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class ConditionTreeEditsTests
    {
        private class HiddenFromMenu : Condition
        {
            public override bool Evaluate(IDetectable target, in SensorContext context) => true;
        }

        private SensorSettings _settings;
        private InFrustum _frustum;
        private IsMoving _moving;
        private WithRaycast _raycast;
        private And _root;

        [SetUp]
        public void SetUp()
        {
            _frustum = new InFrustum();
            _moving = new IsMoving();
            _raycast = new WithRaycast();
            _root = new And { Children = new List<Condition> { _frustum, _moving, _raycast } };
            _settings = ScriptableObject.CreateInstance<SensorSettings>();
            _settings.Root = _root;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void AddChild_OnEmptyTree_SetsRoot()
        {
            _settings.Root = null;
            ConditionTreeEdits.AddChild(_settings, null, _frustum);
            Assert.AreSame(_frustum, _settings.Root);
        }

        [Test]
        public void AddChild_OnNot_SetsItsChild()
        {
            var not = new Not();
            ConditionTreeEdits.AddChild(_settings, not, _frustum);
            Assert.AreSame(_frustum, not.Child);
        }

        [Test]
        public void Remove_TakesNodeOutOfParent()
        {
            ConditionTreeEdits.Remove(_settings, _moving);
            CollectionAssert.AreEqual(new Condition[] { _frustum, _raycast }, _root.Children);
        }

        [Test]
        public void Remove_Root_EmptiesTree()
        {
            ConditionTreeEdits.Remove(_settings, _root);
            Assert.IsNull(_settings.Root);
        }

        [Test]
        public void Wrap_PutsOperatorInNodesPlace()
        {
            var or = new Or();
            ConditionTreeEdits.Wrap(_settings, _moving, or);

            Assert.AreSame(or, _root.Children[1]);
            CollectionAssert.AreEqual(new Condition[] { _moving }, or.Children);
        }

        [Test]
        public void Wrap_Root_MakesOperatorTheRoot()
        {
            var not = new Not();
            ConditionTreeEdits.Wrap(_settings, _root, not);

            Assert.AreSame(not, _settings.Root);
            Assert.AreSame(_root, not.Child);
        }

        [Test]
        public void Unwrap_SplicesChildrenIntoParentList()
        {
            var or = new Or { Children = new List<Condition> { _moving, _raycast } };
            _root.Children = new List<Condition> { _frustum, or };

            Assert.IsTrue(ConditionTreeEdits.CanUnwrap(_settings, or));
            ConditionTreeEdits.Unwrap(_settings, or);

            CollectionAssert.AreEqual(new Condition[] { _frustum, _moving, _raycast }, _root.Children);
        }

        [Test]
        public void Unwrap_RootWithManyChildren_IsRefused()
        {
            Assert.IsFalse(ConditionTreeEdits.CanUnwrap(_settings, _root));
        }

        [Test]
        public void Unwrap_Leaf_IsRefused()
        {
            Assert.IsFalse(ConditionTreeEdits.CanUnwrap(_settings, _frustum));
        }

        [Test]
        public void Duplicate_InsertsAnIndependentCopyAfterNode()
        {
            _frustum.HorizontalFov = 33f;
            ConditionTreeEdits.Duplicate(_settings, _frustum);

            var copy = (InFrustum)_root.Children[1];
            Assert.AreNotSame(_frustum, copy);
            Assert.AreEqual(33f, copy.HorizontalFov);
        }

        [Test]
        public void Move_ShiftsWithinParentAndStopsAtEnds()
        {
            ConditionTreeEdits.Move(_settings, _frustum, 1);
            CollectionAssert.AreEqual(new Condition[] { _moving, _frustum, _raycast }, _root.Children);
            Assert.IsFalse(ConditionTreeEdits.CanMove(_settings, _moving, -1));
        }

        [Test]
        public void MoveTo_AnotherOperator_Reparents()
        {
            var or = new Or();
            _root.Children.Add(or);

            ConditionTreeEdits.MoveTo(_settings, _frustum, or, 0);

            CollectionAssert.AreEqual(new Condition[] { _frustum }, or.Children);
            Assert.IsFalse(_root.Children.Contains(_frustum));
        }

        [Test]
        public void MoveTo_LaterInSameList_LandsWhereDropped()
        {
            ConditionTreeEdits.MoveTo(_settings, _frustum, _root, 2);
            CollectionAssert.AreEqual(new Condition[] { _moving, _frustum, _raycast }, _root.Children);
        }

        [Test]
        public void MoveTo_ItselfOrDescendant_IsRefused()
        {
            var inner = new Or();
            var outer = new And { Children = new List<Condition> { inner } };
            _root.Children.Add(outer);

            Assert.IsFalse(ConditionTreeEdits.CanMoveTo(outer, outer));
            Assert.IsFalse(ConditionTreeEdits.CanMoveTo(outer, inner));
            Assert.IsFalse(ConditionTreeEdits.CanMoveTo(_frustum, _moving));
            Assert.IsFalse(ConditionTreeEdits.CanMoveTo(_frustum, new Not { Child = _moving }));
        }

        [Test]
        public void ChangeType_BetweenOperators_KeepsChildren()
        {
            var or = new Or();
            ConditionTreeEdits.ChangeType(_settings, _root, or);

            Assert.AreSame(or, _settings.Root);
            CollectionAssert.AreEqual(new Condition[] { _frustum, _moving, _raycast }, or.Children);
        }

        [Test]
        public void ChangeType_ManyChildrenToNot_KeepsThemInAnAnd()
        {
            var not = new Not();
            ConditionTreeEdits.ChangeType(_settings, _root, not);

            var and = (And)not.Child;
            CollectionAssert.AreEqual(new Condition[] { _frustum, _moving, _raycast }, and.Children);
        }

        [Test]
        public void Injectors_CanBeAddedMovedDuplicatedRemoved()
        {
            var first = new FakeRandomReflecedEntitiesDetections();
            var second = new FakeRandomDetectionInViewPort();

            ConditionTreeEdits.AddInjector(_settings, first);
            ConditionTreeEdits.AddInjector(_settings, second);
            ConditionTreeEdits.MoveInjector(_settings, 1, -1);
            Assert.AreSame(second, _settings.Injectors[0]);

            ConditionTreeEdits.DuplicateInjector(_settings, 0);
            Assert.IsInstanceOf<FakeRandomDetectionInViewPort>(_settings.Injectors[1]);
            Assert.AreNotSame(second, _settings.Injectors[1]);

            ConditionTreeEdits.RemoveInjector(_settings, 0);
            Assert.AreEqual(2, _settings.Injectors.Count);
        }

        [Test]
        public void Undo_RestoresTheTree()
        {
            Undo.IncrementCurrentGroup();
            ConditionTreeEdits.Remove(_settings, _moving);
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();

            Undo.PerformUndo();

            Assert.AreEqual(3, ((And)_settings.Root).Children.Count);
        }

        [Test]
        public void SerializedPaths_ReachNestedNodes()
        {
            _root.Children.Add(new Not { Child = new IsMoving() });
            var serialized = new SerializedObject(_settings);

            SerializedProperty leaf = serialized.FindProperty("Root.Children.Array.data[0]");
            Assert.IsNotNull(leaf);
            Assert.IsNotNull(leaf.FindPropertyRelative(nameof(InFrustum.HorizontalFov)));

            SerializedProperty nested = serialized.FindProperty("Root.Children.Array.data[3].Child");
            Assert.IsNotNull(nested);
            Assert.IsNotNull(nested.FindPropertyRelative(nameof(IsMoving.MinMovingVelocity)));
        }

        [Test]
        public void SerializedObject_SeesEditsAfterUpdate()
        {
            var serialized = new SerializedObject(_settings);
            ConditionTreeEdits.Wrap(_settings, _frustum, new Not());
            serialized.Update();

            Assert.IsNotNull(serialized.FindProperty("Root.Children.Array.data[0].Child"));
        }

        [Test]
        public void TypeMenu_FindsEverythingWithNoRegistration()
        {
            List<ConditionTypes.Entry> entries = ConditionTypes.GetEntries(typeof(Condition));
            List<System.Type> types = entries.Select(entry => entry.Type).ToList();

            CollectionAssert.Contains(types, typeof(And));
            CollectionAssert.Contains(types, typeof(InFrustum));
            CollectionAssert.DoesNotContain(types, typeof(MultiCondition));
            CollectionAssert.DoesNotContain(types, typeof(HiddenFromMenu));
            StringAssert.StartsWith(ConditionTypes.OperatorsGroup, entries[0].Path);
        }

        [Test]
        public void TypeMenu_ListsInjectors()
        {
            List<System.Type> types = ConditionTypes.GetEntries(typeof(DetectionInjector)).Select(entry => entry.Type).ToList();
            CollectionAssert.Contains(types, typeof(FakeRandomReflecedEntitiesDetections));
            CollectionAssert.Contains(types, typeof(FakeRandomDetectionInViewPort));
        }
    }
}
