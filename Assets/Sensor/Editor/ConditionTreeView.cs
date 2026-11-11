using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>The editable condition tree, shared by the asset inspector, the Sensor inspector and the window.</summary>
    public class ConditionTreeView : VisualElement
    {
        public SensorSettings Settings { get; }
        public SerializedObject SerializedSettings { get; }
        public bool WarnAboutCamera { get; }

        private readonly Label _preview = new Label();
        private readonly VisualElement _content = new VisualElement();
        private readonly VisualElement _dropLine = new VisualElement();
        private readonly ToolbarSearchField _filter = new ToolbarSearchField();
        private readonly List<ConditionNodeElement> _nodes = new List<ConditionNodeElement>();
        private readonly Dictionary<string, bool> _expandedStates = new Dictionary<string, bool>();
        private readonly HashSet<Condition> _expandWhenBuilt = new HashSet<Condition>();

        private ConditionNodeElement _rootElement;
        private Condition _dropParent;
        private int _dropIndex;

        public ConditionTreeView(SensorSettings settings, bool warnAboutCamera)
        {
            Settings = settings;
            SerializedSettings = new SerializedObject(settings);
            WarnAboutCamera = warnAboutCamera;

            AddToClassList("sensor-tree");
            SensorEditorStyles.Apply(this);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("sensor-toolbar");
            _filter.AddToClassList("sensor-filter");
            _filter.tooltip = "Find a condition by name";
            _filter.RegisterValueChangedCallback(evt => ApplyFilter(evt.newValue));
            toolbar.Add(_filter);
            toolbar.Add(new Button(() => SetAllExpanded(true)) { text = "Expand all" });
            toolbar.Add(new Button(() => SetAllExpanded(false)) { text = "Collapse all" });
            Add(toolbar);

            _preview.AddToClassList("sensor-preview");
            _preview.selection.isSelectable = true;
            Add(_preview);

            Add(_content);

            _dropLine.AddToClassList("sensor-drop-line");
            _dropLine.pickingMode = PickingMode.Ignore;
            _dropLine.style.display = DisplayStyle.None;
            Add(_dropLine);

            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);
            this.TrackSerializedObjectValue(SerializedSettings, _ => RefreshTexts());
        }

        public void Edit(Action edit)
        {
            edit();
            Rebuild();
        }

        public void ExpandWhenBuilt(Condition node)
        {
            _expandWhenBuilt.Add(node);
        }

        public void Register(ConditionNodeElement node)
        {
            _nodes.Add(node);
        }

        /// <summary>Operators start open and leaves start closed, until someone changes them.</summary>
        public bool IsExpanded(string path, Condition node)
        {
            if (_expandWhenBuilt.Remove(node))
            {
                _expandedStates[path] = true;
            }
            return _expandedStates.TryGetValue(path, out bool expanded) ? expanded : ConditionTreeEdits.IsOperator(node);
        }

        public void SetExpandedState(string path, bool expanded)
        {
            _expandedStates[path] = expanded;
        }

        public void UpdateDrop(ConditionNodeElement dragged, Vector2 pointer)
        {
            _dropParent = null;
            _dropLine.style.display = DisplayStyle.None;

            ConditionNodeElement over = FindNodeUnder(pointer);
            if (over == null || over == dragged) return;

            Rect headerRect = over.Header.worldBound;
            bool isUpperHalf = pointer.y < headerRect.center.y;
            Condition parent;
            int index;
            float lineY;
            float lineX = over.worldBound.xMin;

            if (!isUpperHalf && over.AcceptsChildren && over.Expanded)
            {
                parent = over.Node;
                index = 0;
                lineY = headerRect.yMax;
                lineX += 16f;
            }
            else if (over.ParentNode is MultiCondition multi)
            {
                parent = multi;
                int overIndex = multi.Children.IndexOf(over.Node);
                index = isUpperHalf ? overIndex : overIndex + 1;
                lineY = isUpperHalf ? over.worldBound.yMin : over.worldBound.yMax;
            }
            else if (over.AcceptsChildren)
            {
                parent = over.Node;
                index = 0;
                lineY = headerRect.yMax;
            }
            else
            {
                return;
            }

            if (!ConditionTreeEdits.CanMoveTo(dragged.Node, parent)) return;

            _dropParent = parent;
            _dropIndex = index;

            Vector2 local = this.WorldToLocal(new Vector2(lineX, lineY));
            _dropLine.style.left = local.x;
            _dropLine.style.top = local.y;
            _dropLine.style.width = over.worldBound.xMax - lineX;
            _dropLine.style.display = DisplayStyle.Flex;
        }

        public void CommitDrop(ConditionNodeElement dragged)
        {
            Condition parent = _dropParent;
            int index = _dropIndex;
            CancelDrop();

            if (parent != null)
            {
                Edit(() => ConditionTreeEdits.MoveTo(Settings, dragged.Node, parent, index));
            }
        }

        public void CancelDrop()
        {
            _dropParent = null;
            _dropLine.style.display = DisplayStyle.None;
        }

        private ConditionNodeElement FindNodeUnder(Vector2 pointer)
        {
            // Deepest cards are registered last, so search backwards.
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                ConditionNodeElement node = _nodes[i];
                if (node.Header.worldBound.Contains(pointer) && node.resolvedStyle.display != DisplayStyle.None && IsShown(node))
                {
                    return node;
                }
            }
            return null;
        }

        private static bool IsShown(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None) return false;
            }
            return true;
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            Undo.undoRedoPerformed += Rebuild;
            // Waits a step so a hosting inspector finishes binding its own fields first.
            schedule.Execute(Rebuild);
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            Undo.undoRedoPerformed -= Rebuild;
        }

        private void Rebuild()
        {
            if (Settings == null) return;

            SerializedSettings.Update();
            _nodes.Clear();
            _content.Clear();
            _rootElement = null;

            if (Settings.Root == null)
            {
                _content.Add(CreateEmptyState());
            }
            else
            {
                _rootElement = new ConditionNodeElement(this, Settings.Root, null, nameof(SensorSettings.Root));
                _content.Add(_rootElement);
            }

            _content.Bind(SerializedSettings);
            RefreshTexts();
            ApplyFilter(_filter.value);
        }

        private VisualElement CreateEmptyState()
        {
            var empty = new VisualElement();
            empty.AddToClassList("sensor-empty");
            empty.Add(new Label("This tree is empty, so the tree detects nothing. Start with AND or OR to combine several conditions."));

            var button = new Button { text = "+ Add first condition" };
            button.AddToClassList("sensor-add-button");
            button.clicked += () => TypePickerDropdown.Show(button.worldBound, typeof(Condition), "Add condition", type =>
            {
                var root = (Condition)Activator.CreateInstance(type);
                ExpandWhenBuilt(root);
                Edit(() => ConditionTreeEdits.AddChild(Settings, null, root));
            });
            empty.Add(button);
            return empty;
        }

        private void RefreshTexts()
        {
            _preview.text = ConditionTree.Describe(Settings.Root);
            _rootElement?.RefreshTexts();
        }

        private void ApplyFilter(string filter)
        {
            _rootElement?.ApplyFilter(filter);
        }

        private void SetAllExpanded(bool expanded)
        {
            _rootElement?.SetExpandedRecursive(expanded);
        }
    }

    /// <summary>The list of injected detections on a settings asset.</summary>
    public class InjectorListView : VisualElement
    {
        private class InjectorCard : TreeCard
        {
            private readonly InjectorListView _list;
            private readonly int _index;
            private readonly DetectionInjector _injector;

            public InjectorCard(InjectorListView list, DetectionInjector injector, int index)
            {
                _list = list;
                _index = index;
                _injector = injector;
                AddToClassList("sensor-card--injector");
                Handle.style.display = DisplayStyle.None;
                Badge.text = "INJECT";
                TitleLabel.text = injector.DisplayName;

                SerializedProperty property = list._serialized.FindProperty($"{nameof(SensorSettings.Injectors)}.Array.data[{index}]");
                if (property != null)
                {
                    SensorEditorStyles.AddFields(Body, property);
                }

                RefreshArrow();
                SetExpanded(false, false);
                RefreshTexts();
            }

            /// <summary>The summary and the warning, such as percents that do not add up to 100%. Updated as the fields change.</summary>
            public void RefreshTexts()
            {
                SetSummary(_injector.Summary);
                SetWarning(_injector.Warning);
            }

            protected override List<CardMenuEntry> BuildMenu()
            {
                SensorSettings settings = _list._settings;
                return new List<CardMenuEntry>
                {
                    new CardMenuEntry("Duplicate", () => _list.Edit(() => ConditionTreeEdits.DuplicateInjector(settings, _index))),
                    new CardMenuEntry("Move up", () => _list.Edit(() => ConditionTreeEdits.MoveInjector(settings, _index, -1)), _index > 0),
                    new CardMenuEntry("Move down", () => _list.Edit(() => ConditionTreeEdits.MoveInjector(settings, _index, 1)), _index < settings.Injectors.Count - 1),
                    new CardMenuEntry("Delete", () => _list.Edit(() => ConditionTreeEdits.RemoveInjector(settings, _index))),
                };
            }
        }

        private readonly SensorSettings _settings;
        private readonly SerializedObject _serialized;
        private readonly VisualElement _content = new VisualElement();

        public InjectorListView(SensorSettings settings)
        {
            _settings = settings;
            _serialized = new SerializedObject(settings);
            SensorEditorStyles.Apply(this);
            Add(_content);

            var add = new Button { text = "+ Add injector" };
            add.AddToClassList("sensor-add-button");
            add.clicked += () => TypePickerDropdown.Show(add.worldBound, typeof(DetectionInjector), "Add injector", type =>
                Edit(() => ConditionTreeEdits.AddInjector(_settings, (DetectionInjector)Activator.CreateInstance(type))));
            Add(add);

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Undo.undoRedoPerformed += Rebuild;
                schedule.Execute(Rebuild);
            });
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= Rebuild);
            this.TrackSerializedObjectValue(_serialized, _ => RefreshTexts());
        }

        private void RefreshTexts()
        {
            foreach (VisualElement child in _content.Children())
            {
                (child as InjectorCard)?.RefreshTexts();
            }
        }

        private void Edit(Action edit)
        {
            edit();
            Rebuild();
        }

        private void Rebuild()
        {
            if (_settings == null) return;

            _serialized.Update();
            _content.Clear();

            for (int i = 0; i < _settings.Injectors.Count; i++)
            {
                DetectionInjector injector = _settings.Injectors[i];
                if (injector != null)
                {
                    _content.Add(new InjectorCard(this, injector, i));
                }
            }
            _content.Bind(_serialized);
        }
    }
}
