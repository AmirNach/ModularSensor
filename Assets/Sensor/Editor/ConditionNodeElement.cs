using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>One condition in the tree, with its settings and children.</summary>
    public class ConditionNodeElement : TreeCard
    {
        private const string ChildrenField = nameof(MultiCondition.Children);
        private const string ChildField = nameof(Not.Child);
        private const string EmptyOperatorWarning = "Empty, always false";

        public Condition Node { get; }
        public Condition ParentNode { get; }
        public bool AcceptsChildren => Node is MultiCondition || Node is Not;

        private readonly ConditionTreeView _view;
        private readonly string _path;
        private readonly List<ConditionNodeElement> _childNodes = new List<ConditionNodeElement>();

        // How many shapes an area condition had at the last refresh, to name the ones the list's + button adds.
        private int _shapeCount;

        public ConditionNodeElement(ConditionTreeView view, Condition node, Condition parentNode, string path)
        {
            _view = view;
            Node = node;
            ParentNode = parentNode;
            _path = path;
            view.Register(this);

            StyleByKind();

            SerializedProperty property = view.SerializedSettings.FindProperty(path);
            if (property != null)
            {
                SensorEditorStyles.AddFields(Body, property, ChildrenField, ChildField);
            }

            AddChildren();
            RefreshArrow();

            if (parentNode != null)
            {
                Handle.AddManipulator(new NodeDragManipulator(view, this));
            }
            else
            {
                Handle.style.visibility = Visibility.Hidden;
            }

            SetExpanded(view.IsExpanded(path, node), false);
            ExpandedChanged += expanded => view.SetExpandedState(path, expanded);
            _shapeCount = (node as AreaCondition)?.ShapeCount ?? 0;
            RefreshTexts();
        }

        public void RefreshTexts()
        {
            if (Node is AreaCondition area) NameAddedShapes(area);

            SetSummary(Node.Summary);
            SetWarning(GetWarning());

            foreach (ConditionNodeElement child in _childNodes)
            {
                child.RefreshTexts();
            }
        }

        public void SetExpandedRecursive(bool expanded)
        {
            SetExpanded(expanded, true);
            foreach (ConditionNodeElement child in _childNodes)
            {
                child.SetExpandedRecursive(expanded);
            }
        }

        /// <summary>Marks matches, dims the rest and opens the way to anything found. An empty filter restores the view.</summary>
        public bool ApplyFilter(string filter)
        {
            bool isFiltering = !string.IsNullOrEmpty(filter);
            bool selfMatches = isFiltering && Node.DisplayName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

            bool childMatches = false;
            foreach (ConditionNodeElement child in _childNodes)
            {
                childMatches |= child.ApplyFilter(filter);
            }

            EnableInClassList("sensor-card--match", selfMatches);
            EnableInClassList("sensor-card--dimmed", isFiltering && !selfMatches && !childMatches);
            SetExpanded(isFiltering ? childMatches || (selfMatches && Expanded) : _view.IsExpanded(_path, Node), false);

            return selfMatches || childMatches;
        }

        protected override List<CardMenuEntry> BuildMenu()
        {
            SensorSettings settings = _view.Settings;
            return new List<CardMenuEntry>
            {
                new CardMenuEntry("Change type...", ChangeType),
                new CardMenuEntry("Wrap in AND", () => _view.Edit(() => ConditionTreeEdits.Wrap(settings, Node, new And()))),
                new CardMenuEntry("Wrap in OR", () => _view.Edit(() => ConditionTreeEdits.Wrap(settings, Node, new Or()))),
                new CardMenuEntry("Wrap in NOT", () => _view.Edit(() => ConditionTreeEdits.Wrap(settings, Node, new Not()))),
                new CardMenuEntry("Unwrap", () => _view.Edit(() => ConditionTreeEdits.Unwrap(settings, Node)), ConditionTreeEdits.CanUnwrap(settings, Node)),
                new CardMenuEntry("Duplicate", () => _view.Edit(() => ConditionTreeEdits.Duplicate(settings, Node)), ConditionTreeEdits.CanDuplicate(settings, Node)),
                new CardMenuEntry("Move up", () => _view.Edit(() => ConditionTreeEdits.Move(settings, Node, -1)), ConditionTreeEdits.CanMove(settings, Node, -1)),
                new CardMenuEntry("Move down", () => _view.Edit(() => ConditionTreeEdits.Move(settings, Node, 1)), ConditionTreeEdits.CanMove(settings, Node, 1)),
                new CardMenuEntry("Delete", ConfirmDelete),
            };
        }

        private void StyleByKind()
        {
            switch (Node)
            {
                case And _:
                    SetKind("sensor-card--and", "AND", "All must pass");
                    break;
                case Or _:
                    SetKind("sensor-card--or", "OR", "Any must pass");
                    break;
                case Not _:
                    SetKind("sensor-card--not", "NOT", "Must not pass");
                    break;
                case MultiCondition _:
                    SetKind("sensor-card--and", "GROUP", Node.DisplayName);
                    break;
                default:
                    SetKind("sensor-card--leaf", string.Empty, Node.DisplayName);
                    Badge.style.display = DisplayStyle.None;
                    break;
            }
        }

        private void SetKind(string kindClass, string badge, string title)
        {
            AddToClassList(kindClass);
            Badge.text = badge;
            TitleLabel.text = title;
        }

        private void AddChildren()
        {
            switch (Node)
            {
                case MultiCondition multi:
                    for (int i = 0; i < multi.Children.Count; i++)
                    {
                        AddChildAt(multi, i);
                    }
                    ChildArea.Add(CreateAddButton());
                    break;
                case Not not when not.Child != null:
                    AddChildNode(not.Child, $"{_path}.{ChildField}");
                    break;
                case Not _:
                    ChildArea.Add(CreateAddButton());
                    break;
            }
        }

        private void AddChildAt(MultiCondition multi, int index)
        {
            Condition child = multi.Children[index];
            if (child != null)
            {
                AddChildNode(child, $"{_path}.{ChildrenField}.Array.data[{index}]");
                return;
            }

            // The class was renamed or deleted, so the entry can only be removed.
            var row = new VisualElement();
            row.AddToClassList("sensor-missing");
            row.Add(new Label("Missing condition: its class was renamed or deleted"));
            row.Add(new Button(() => _view.Edit(() => ConditionTreeEdits.RemoveMissingChild(_view.Settings, multi, index))) { text = "Remove" });
            ChildArea.Add(row);
        }

        private void AddChildNode(Condition child, string path)
        {
            var element = new ConditionNodeElement(_view, child, Node, path);
            _childNodes.Add(element);
            ChildArea.Add(element);
        }

        private Button CreateAddButton()
        {
            var button = new Button { text = "+ Add condition" };
            button.AddToClassList("sensor-add-button");
            button.clicked += () => TypePickerDropdown.Show(button.worldBound, typeof(Condition), "Add condition", type =>
            {
                var child = (Condition)Activator.CreateInstance(type);
                _view.ExpandWhenBuilt(child);
                _view.SetExpandedState(_path, true);
                _view.Edit(() => ConditionTreeEdits.AddChild(_view.Settings, Node, child));
            });
            return button;
        }

        private void ChangeType()
        {
            TypePickerDropdown.Show(Header.worldBound, typeof(Condition), "Change type", type =>
            {
                var replacement = (Condition)Activator.CreateInstance(type);
                if (ConditionTreeEdits.IsOperator(Node) && !ConditionTreeEdits.IsOperator(replacement) && HasChildren()
                    && !EditorUtility.DisplayDialog("Change type", "This removes everything under this operator. Continue?", "Change type", "Cancel"))
                {
                    return;
                }
                _view.Edit(() => ConditionTreeEdits.ChangeType(_view.Settings, Node, replacement));
            });
        }

        private void ConfirmDelete()
        {
            if (HasChildren() && !EditorUtility.DisplayDialog("Delete", "This also deletes everything under this operator.", "Delete", "Cancel"))
            {
                return;
            }
            _view.Edit(() => ConditionTreeEdits.Remove(_view.Settings, Node));
        }

        private bool HasChildren()
        {
            var children = new List<Condition>();
            Node.CollectChildren(children);
            return children.Count > 0;
        }

        private string GetWarning()
        {
            if (AcceptsChildren && !HasChildren()) return EmptyOperatorWarning;
            if (Node.RequiresCamera && _view.WarnAboutCamera) return "Needs a SensorWithCamera";
            if (Node is AreaCondition area) return AreaCondition.FindNameClash(_view.Settings.Root, area) ?? area.FindLabelClash();
            return null;
        }

        /// <summary>
        /// The list's + button copies the last shape, label included, so a shape just added gets a unique label right away
        /// ("Polygon 1" copied becomes "Polygon 2", "Parking" becomes "Parking (2)"). Labels typed by hand are left alone and warned about.
        /// </summary>
        private void NameAddedShapes(AreaCondition area)
        {
            int previous = _shapeCount;
            _shapeCount = area.ShapeCount;
            if (_shapeCount <= previous) return;

            Undo.RecordObject(_view.Settings, "Name added shape");
            if (area.MakeLabelsUnique(previous) == 0) return;

            EditorUtility.SetDirty(_view.Settings);
            _view.SerializedSettings.Update();
        }
    }

    /// <summary>Drags a card by its handle and reports the pointer to the tree view.</summary>
    public class NodeDragManipulator : PointerManipulator
    {
        private readonly ConditionTreeView _view;
        private readonly ConditionNodeElement _node;
        private bool _isDragging;

        public NodeDragManipulator(ConditionTreeView view, ConditionNodeElement node)
        {
            _view = view;
            _node = node;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp);
            target.RegisterCallback<PointerCaptureOutEvent>(OnCaptureLost);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnCaptureLost);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0) return;

            _isDragging = true;
            target.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_isDragging)
            {
                _view.UpdateDrop(_node, evt.position);
            }
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging) return;

            _isDragging = false;
            target.ReleasePointer(evt.pointerId);
            _view.CommitDrop(_node);
            evt.StopPropagation();
        }

        private void OnCaptureLost(PointerCaptureOutEvent evt)
        {
            _isDragging = false;
            _view.CancelDrop();
        }
    }
}
