using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>
    /// A filterable list of live targets in three collapsible groups: Passed, Failed and Injected.
    /// A long group scrolls inside its own list, so thousands of rows stay fast.
    /// </summary>
    public class TargetListView : VisualElement
    {
        public const int MaxVisibleRows = 10;
        public const float RowHeight = 22f;
        // The list's border and padding take a few pixels. Without this much spare room, a short list
        // is a pixel too small for its rows and Unity adds a scrollbar that has nothing to scroll.
        private const float ListBorder = 6f;

        private static readonly string[] Titles = { "Passed", "Failed", "Injected" };

        private static readonly string[] Tooltips =
        {
            "The conditions accepted these objects, so they are detected.",
            "The conditions rejected these objects.",
            "Added by an injector, which bypasses the conditions.",
        };

        private static readonly string[] StateClasses =
        {
            "sensor-target-group--passed",
            "sensor-target-group--failed",
            "sensor-target-group--injected",
        };

        private sealed class Group
        {
            public CollapsibleGroup Foldout;
            public ListView List;
            public Label Message;
            public string FoldKey;
            public readonly List<TargetRow> Items = new List<TargetRow>();
        }

        /// <summary>Raised when the user picks a row. Not raised for selections made by code.</summary>
        public event Action<IDetectable> TargetPicked;

        public IDetectable SelectedTarget { get; private set; }

        private readonly string _filterKey;
        private readonly IUiStateStore _store;
        private readonly SensorSearchField _filter = new SensorSearchField("Filter by name");
        private readonly Group[] _groups = new Group[TargetListModel.StateCount];
        private IReadOnlyList<TargetRow> _rows = Array.Empty<TargetRow>();

        /// <param name="viewKey">Names this list, so each kind of view remembers its own filter and open groups.</param>
        /// <param name="store">Where open groups and the filter are remembered. Defaults to memory for the life of the app.</param>
        public TargetListView(string viewKey, IUiStateStore store = null)
        {
            _store = store ?? MemoryUiStateStore.Shared;
            _filterKey = $"SensorSystem.TargetList.{viewKey}.Filter";
            SensorStyles.Apply(this);
            AddToClassList("sensor-target-list");

            var toolbar = new VisualElement();
            toolbar.AddToClassList("sensor-toolbar");
            _filter.AddToClassList("sensor-filter");
            _filter.tooltip = "Find objects by name. Every word you type has to match.";
            _filter.SetValueWithoutNotify(_store.GetString(_filterKey, string.Empty));
            _filter.ValueChanged += text =>
            {
                _store.SetString(_filterKey, text);
                Apply();
            };
            toolbar.Add(_filter);
            toolbar.Add(new Button(() => ExpandAll(true)) { text = "Expand all" });
            toolbar.Add(new Button(() => ExpandAll(false)) { text = "Collapse all" });
            Add(toolbar);

            for (int i = 0; i < _groups.Length; i++)
            {
                _groups[i] = CreateGroup(i, viewKey);
                Add(_groups[i].Foldout);
            }
            Apply();
        }

        public string FilterText
        {
            get => _filter.value;
            set
            {
                _filter.SetValueWithoutNotify(value ?? string.Empty);
                _store.SetString(_filterKey, _filter.value);
                Apply();
            }
        }

        public void SetRows(IReadOnlyList<TargetRow> rows)
        {
            _rows = rows ?? Array.Empty<TargetRow>();
            Apply();
        }

        /// <summary>Highlights a target without raising TargetPicked. Null clears the highlight.</summary>
        public void SetSelected(IDetectable target)
        {
            SelectedTarget = target;
            foreach (Group group in _groups)
            {
                RestoreSelection(group);
            }
        }

        public void ExpandAll(bool expanded)
        {
            foreach (Group group in _groups)
            {
                group.Foldout.SetValueWithoutNotify(expanded);
                _store.SetBool(group.FoldKey, expanded);
                if (expanded)
                {
                    group.List.RefreshItems();
                }
            }
        }

        public bool IsExpanded(TargetState state) => _groups[(int)state].Foldout.value;

        public string GetTitle(TargetState state) => _groups[(int)state].Foldout.text;

        /// <summary>The rows shown in a group, after filtering and sorting.</summary>
        public IReadOnlyList<TargetRow> GetRows(TargetState state) => _groups[(int)state].Items;

        public ListView GetList(TargetState state) => _groups[(int)state].List;

        private Group CreateGroup(int index, string viewKey)
        {
            var group = new Group { FoldKey = $"SensorSystem.TargetList.{viewKey}.{Titles[index]}" };

            group.Foldout = new CollapsibleGroup(Titles[index], _store.GetBool(group.FoldKey, true)) { tooltip = Tooltips[index] };
            group.Foldout.AddToClassList("sensor-target-group");
            group.Foldout.AddToClassList(StateClasses[index]);
            group.Foldout.ValueChanged += expanded =>
            {
                _store.SetBool(group.FoldKey, expanded);
                if (expanded)
                {
                    group.List.RefreshItems();
                }
            };

            group.Message = new Label();
            group.Message.AddToClassList("sensor-hint");

            group.List = new ListView
            {
                fixedItemHeight = RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.Single,
                showBorder = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                itemsSource = group.Items,
            };
            group.List.AddToClassList("sensor-target-items");
            group.List.makeItem = MakeRow;
            group.List.bindItem = (element, i) => BindRow(group, element, i);
            group.List.selectionChanged += selected => OnSelectionChanged(group, selected);

            group.Foldout.Add(group.Message);
            group.Foldout.Add(group.List);
            return group;
        }

        private static VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("sensor-target-row");

            var name = new Label { name = "target-name" };
            name.AddToClassList("sensor-target-name");
            row.Add(name);

            var detail = new Label { name = "target-detail" };
            detail.AddToClassList("sensor-target-detail");
            row.Add(detail);
            return row;
        }

        private static void BindRow(Group group, VisualElement element, int index)
        {
            if (index < 0 || index >= group.Items.Count) return;

            TargetRow row = group.Items[index];
            var name = element.Q<Label>("target-name");
            name.text = row.Name;
            name.tooltip = row.Name;
            element.Q<Label>("target-detail").text = row.Detail;
        }

        private void Apply()
        {
            bool isFiltering = !string.IsNullOrWhiteSpace(_filter.value);
            TargetListModel.Groups groups = TargetListModel.Build(_rows, _filter.value);

            for (int i = 0; i < _groups.Length; i++)
            {
                Group group = _groups[i];
                int total = groups.Totals[i];

                group.Items.Clear();
                group.Items.AddRange(groups.Shown[i]);

                group.Foldout.text = TargetListModel.FormatCount(Titles[i], group.Items.Count, total, isFiltering);
                group.Foldout.EnableInClassList("sensor-target-group--empty", total == 0);

                bool hasRows = group.Items.Count > 0;
                group.Message.text = total == 0 ? "None" : "No matches";
                group.Message.style.display = hasRows ? DisplayStyle.None : DisplayStyle.Flex;
                group.List.style.display = hasRows ? DisplayStyle.Flex : DisplayStyle.None;
                group.List.style.height = Mathf.Min(group.Items.Count, MaxVisibleRows) * RowHeight + ListBorder;

                // Only a list taller than its window needs a scrollbar.
                ScrollView scroll = group.List.Q<ScrollView>();
                if (scroll != null)
                {
                    scroll.verticalScrollerVisibility = group.Items.Count > MaxVisibleRows ? ScrollerVisibility.Auto : ScrollerVisibility.Hidden;
                }

                // A collapsed group costs nothing until it is opened again.
                if (group.Foldout.value)
                {
                    group.List.RefreshItems();
                }
                RestoreSelection(group);
            }
        }

        private void RestoreSelection(Group group)
        {
            int index = SelectedTarget == null ? -1 : group.Items.FindIndex(row => ReferenceEquals(row.Target, SelectedTarget));
            group.List.SetSelectionWithoutNotify(index >= 0 ? new[] { index } : Array.Empty<int>());
        }

        private void OnSelectionChanged(Group picked, IEnumerable<object> selected)
        {
            object first = selected.FirstOrDefault();
            if (!(first is TargetRow row)) return;

            SelectedTarget = row.Target;
            foreach (Group group in _groups)
            {
                if (group != picked)
                {
                    group.List.SetSelectionWithoutNotify(Array.Empty<int>());
                }
            }
            TargetPicked?.Invoke(SelectedTarget);
        }
    }
}
