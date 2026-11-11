using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem
{
    /// <summary>
    /// The controls for one object's fields. Call Refresh now and then so values changed by code show up,
    /// and TrySet to change a field exactly the way typing in its control would.
    /// </summary>
    public sealed class ParameterPanel : VisualElement
    {
        private readonly Dictionary<string, ParameterBinding> _bindings = new Dictionary<string, ParameterBinding>();

        // The first builder rebuilds list rows later, so it has to stay alive with the panel.
        internal List<Action> Refreshers { get; set; } = new List<Action>();

        /// <summary>The path of every field with a control, such as "MinMovingVelocity" or "Ranges[0].Distance".</summary>
        public IReadOnlyCollection<string> Paths => _bindings.Keys;

        internal void AddBinding(ParameterBinding binding) => _bindings[binding.Path] = binding;

        internal void RemoveBindingsUnder(string prefix)
        {
            foreach (string path in _bindings.Keys.Where(path => path.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                _bindings.Remove(path);
            }
        }

        /// <summary>Sets a field as if it was typed into its control: clamped to its [Min] or [Range], then applied. Returns false if there is no such field.</summary>
        public bool TrySet(string path, object value)
        {
            if (!_bindings.TryGetValue(path, out ParameterBinding binding)) return false;

            binding.Apply(value);
            return true;
        }

        public object Get(string path)
        {
            return _bindings.TryGetValue(path, out ParameterBinding binding) ? binding.Get() : null;
        }

        /// <summary>Re-reads every field that is not being edited right now.</summary>
        public void Refresh()
        {
            foreach (Action refresh in Refreshers.ToArray())
            {
                try
                {
                    refresh();
                }
                catch (Exception e)
                {
                    // A field that cannot be read must not break the rest of the panel.
                    Debug.LogWarning($"Sensor debug: could not refresh a parameter ({e.Message}).");
                }
            }
        }
    }

    internal sealed class ParameterBinding
    {
        public string Path;
        public Func<object> Get;
        public Action<object> Set;
        public Action<object> Show;
        public Func<object, object> Clamp;
        public Action Changed;

        /// <summary>What happens when the user edits the control: clamp, write the field, tell the owner.</summary>
        public void Apply(object requested)
        {
            object value = Clamp != null ? Clamp(requested) : requested;
            Set(value);
            if (!Equals(value, requested))
            {
                Show?.Invoke(value);
            }
            Changed?.Invoke();
        }
    }

    public static class ParameterEditor
    {
        private static readonly Regex WordBreaks = new Regex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled);

        /// <summary>Builds a control for every field Unity would serialize on this object. Nothing needs to be added per class.</summary>
        public static ParameterPanel Build(object instance, Action changed = null)
        {
            var panel = new ParameterPanel();
            panel.AddToClassList("sensor-params");

            if (instance != null)
            {
                var builder = new ParameterBuilder(panel, changed);
                builder.AddFields(panel, instance.GetType(), () => instance, null, string.Empty);
            }
            return panel;
        }

        /// <summary>
        /// Public fields and [SerializeField] fields, base classes first. [NonSerialized], [HideInInspector],
        /// [SerializeReference] (the children of operators) and read-only fields are left out.
        /// </summary>
        public static IReadOnlyList<FieldInfo> GetEditableFields(Type type)
        {
            var chain = new Stack<Type>();
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                chain.Push(current);
            }

            var fields = new List<FieldInfo>();
            foreach (Type current in chain)
            {
                foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (IsEditable(field))
                    {
                        fields.Add(field);
                    }
                }
            }
            return fields;
        }

        /// <summary>A field name as a label: "_minMovingVelocity" becomes "Min Moving Velocity".</summary>
        public static string Nicify(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;

            if (name[0] == '<')
            {
                int end = name.IndexOf('>');
                name = end > 1 ? name.Substring(1, end - 1) : name;
            }
            name = name.TrimStart('_');
            if (name.StartsWith("m_", StringComparison.Ordinal))
            {
                name = name.Substring(2);
            }
            if (name.Length == 0) return string.Empty;

            name = WordBreaks.Replace(name, " ");
            return char.ToUpperInvariant(name[0]) + name.Substring(1);
        }

        private static bool IsEditable(FieldInfo field)
        {
            if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized) return false;
            if (field.GetCustomAttribute<HideInInspector>() != null) return false;
            if (field.GetCustomAttribute<SerializeReference>() != null) return false;

            return field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
        }
    }

    internal readonly struct ParameterMeta
    {
        public readonly string Tooltip;
        public readonly string Header;
        public readonly string ShowIf;
        public readonly bool ShowWhen;
        public readonly bool HasMin;
        public readonly float Min;
        public readonly bool HasRange;
        public readonly float Max;

        public ParameterMeta(FieldInfo field)
        {
            Tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
            Header = field.GetCustomAttribute<HeaderAttribute>()?.header;
            var showIf = field.GetCustomAttribute<ShowIfAttribute>();
            ShowIf = showIf?.BoolField;
            ShowWhen = showIf?.ShowWhen ?? true;

            var range = field.GetCustomAttribute<RangeAttribute>();
            var min = field.GetCustomAttribute<MinAttribute>();
            HasRange = range != null;
            HasMin = range != null || min != null;
            Min = range != null ? range.min : min != null ? min.min : 0f;
            Max = range != null ? range.max : 0f;
        }

        public float ClampFloat(float value)
        {
            if (HasRange) return Mathf.Clamp(value, Min, Max);
            return HasMin ? Mathf.Max(value, Min) : value;
        }

        public int ClampInt(int value)
        {
            if (HasRange) return Mathf.Clamp(value, Mathf.RoundToInt(Min), Mathf.RoundToInt(Max));
            return HasMin ? Mathf.Max(value, Mathf.RoundToInt(Min)) : value;
        }
    }

    /// <summary>Creates the right control for each field type by reflection.</summary>
    internal sealed class ParameterBuilder
    {
        private readonly ParameterPanel _panel;
        private readonly Action _changed;

        // Controls add their refresh action here. A list swaps in its own while it builds rows,
        // so rows can be thrown away and rebuilt without leaving stale actions behind.
        private List<Action> _refreshers;

        public ParameterBuilder(ParameterPanel panel, Action changed)
        {
            _panel = panel;
            _changed = changed;
            _refreshers = panel.Refreshers;
        }

        public void AddFields(VisualElement parent, Type ownerType, Func<object> getOwner, Action<object> setOwner, string pathPrefix)
        {
            foreach (FieldInfo field in ParameterEditor.GetEditableFields(ownerType))
            {
                var meta = new ParameterMeta(field);
                if (!string.IsNullOrEmpty(meta.Header))
                {
                    var header = new Label(meta.Header);
                    header.AddToClassList("sensor-param-header");
                    parent.Add(header);
                }

                FieldInfo captured = field;
                VisualElement control = CreateControl(
                    field.FieldType,
                    ParameterEditor.Nicify(field.Name),
                    pathPrefix + field.Name,
                    () => captured.GetValue(getOwner()),
                    value =>
                    {
                        object owner = getOwner();
                        captured.SetValue(owner, value);
                        // A struct was copied out, so it has to be written back.
                        if (ownerType.IsValueType)
                        {
                            setOwner?.Invoke(owner);
                        }
                    },
                    meta);
                parent.Add(control);
                ApplyShowIf(control, meta, ownerType, getOwner);
            }
        }

        /// <summary>Hides a control while the bool field named by [ShowIf] is off, and follows that bool when it changes.</summary>
        private void ApplyShowIf(VisualElement control, ParameterMeta meta, Type ownerType, Func<object> getOwner)
        {
            if (string.IsNullOrEmpty(meta.ShowIf)) return;

            FieldInfo condition = ownerType.GetField(meta.ShowIf, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
            if (condition == null || condition.FieldType != typeof(bool)) return;

            bool showWhen = meta.ShowWhen;
            void Update() => control.style.display = (bool)condition.GetValue(getOwner()) == showWhen ? DisplayStyle.Flex : DisplayStyle.None;
            Update();
            _refreshers.Add(Update);
        }

        private VisualElement CreateControl(Type type, string label, string path, Func<object> get, Action<object> set, ParameterMeta meta)
        {
            if (type == typeof(bool)) return Field<bool, Toggle>(new Toggle(label), path, get, set, meta);
            if (type == typeof(string)) return Field<string, TextField>(new TextField(label), path, get, set, meta);
            if (type == typeof(double)) return Field<double, DoubleField>(new DoubleField(label), path, get, set, meta);
            if (type == typeof(long)) return Field<long, LongField>(new LongField(label), path, get, set, meta);

            if (type == typeof(int))
            {
                BaseField<int> field = meta.HasRange
                    ? (BaseField<int>)new SliderInt(label, Mathf.RoundToInt(meta.Min), Mathf.RoundToInt(meta.Max)) { showInputField = true }
                    : new IntegerField(label);
                return Field<int, BaseField<int>>(field, path, get, set, meta, meta.ClampInt);
            }

            if (type == typeof(float))
            {
                BaseField<float> field = meta.HasRange
                    ? (BaseField<float>)new Slider(label, meta.Min, meta.Max) { showInputField = true }
                    : new FloatField(label);
                return Field<float, BaseField<float>>(field, path, get, set, meta, meta.ClampFloat);
            }

            if (type.IsEnum)
            {
                if (type.IsDefined(typeof(FlagsAttribute), false)) return CreateFlags(type, label, path, get, set, meta);

                return Field<Enum, EnumField>(new EnumField(label, (Enum)get()), path, get, set, meta);
            }

            if (type == typeof(Vector2)) return Field<Vector2, Vector2Field>(new Vector2Field(label), path, get, set, meta);
            if (type == typeof(Vector3)) return Field<Vector3, Vector3Field>(new Vector3Field(label), path, get, set, meta);
            if (type == typeof(Vector4)) return Field<Vector4, Vector4Field>(new Vector4Field(label), path, get, set, meta);
            if (type == typeof(Vector2Int)) return Field<Vector2Int, Vector2IntField>(new Vector2IntField(label), path, get, set, meta);
            if (type == typeof(Vector3Int)) return Field<Vector3Int, Vector3IntField>(new Vector3IntField(label), path, get, set, meta);

            if (type == typeof(Color)) return CreateColor(label, path, get, set, meta);
            if (type == typeof(LayerMask)) return CreateLayerMask(label, path, get, set, meta);

            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return CreateReference(label, path, get, meta);

            if (TryGetListElementType(type, out Type elementType)) return CreateList(type, elementType, label, path, get, set, meta);

            if (IsNestedSerializable(type)) return CreateNested(type, label, path, get, set);

            return Unsupported(label, type);
        }

        private TField Field<T, TField>(TField field, string path, Func<object> get, Action<object> set, ParameterMeta meta, Func<T, T> clamp = null) where TField : BaseField<T>
        {
            field.tooltip = meta.Tooltip;
            field.SetValueWithoutNotify(ReadValue<T>(get));

            var binding = new ParameterBinding
            {
                Path = path,
                Get = get,
                Set = set,
                Show = value => field.SetValueWithoutNotify((T)value),
                Clamp = clamp == null ? (Func<object, object>)null : value => clamp((T)value),
                Changed = _changed,
            };
            _panel.AddBinding(binding);

            field.RegisterValueChangedCallback(evt => binding.Apply(evt.newValue));
            _refreshers.Add(() =>
            {
                if (!HasFocus(field))
                {
                    field.SetValueWithoutNotify(ReadValue<T>(get));
                }
            });
            return field;
        }

        private static T ReadValue<T>(Func<object> get)
        {
            object value = get();
            return value == null ? default : (T)value;
        }

        private VisualElement CreateColor(string label, string path, Func<object> get, Action<object> set, ParameterMeta meta)
        {
            // Runtime UI has no color picker, so the channels are edited as four numbers.
            var field = new Vector4Field(label) { tooltip = meta.Tooltip };
            Func<Vector4> read = () =>
            {
                var color = (Color)get();
                return new Vector4(color.r, color.g, color.b, color.a);
            };
            field.SetValueWithoutNotify(read());

            var binding = new ParameterBinding
            {
                Path = path,
                Get = get,
                Set = value => set(value is Vector4 vector ? new Color(vector.x, vector.y, vector.z, vector.w) : value),
                Show = value => field.SetValueWithoutNotify((Vector4)value),
                Changed = _changed,
            };
            _panel.AddBinding(binding);

            field.RegisterValueChangedCallback(evt => binding.Apply(evt.newValue));
            _refreshers.Add(() =>
            {
                if (!HasFocus(field))
                {
                    field.SetValueWithoutNotify(read());
                }
            });
            return field;
        }

        private VisualElement CreateFlags(Type type, string label, string path, Func<object> get, Action<object> set, ParameterMeta meta)
        {
            // Runtime UI has no flags field, so each flag gets a checkbox.
            var foldout = new CollapsibleGroup(string.Empty, false) { tooltip = meta.Tooltip };
            var toggles = new List<(long bit, Toggle toggle)>();

            Func<long> read = () => Convert.ToInt64(get());
            foreach (object flag in Enum.GetValues(type))
            {
                long bit = Convert.ToInt64(flag);
                if (bit == 0 || (bit & (bit - 1)) != 0) continue; // skip None and combinations

                long captured = bit;
                var toggle = new Toggle(Enum.GetName(type, flag)) { value = (read() & bit) != 0 };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    long mask = evt.newValue ? read() | captured : read() & ~captured;
                    set(Enum.ToObject(type, mask));
                    UpdateTitle();
                    _changed?.Invoke();
                });
                toggles.Add((bit, toggle));
                foldout.Add(toggle);
            }

            void UpdateTitle()
            {
                long mask = read();
                foldout.text = $"{label} ({toggles.Count(entry => (mask & entry.bit) != 0)} set)";
            }

            UpdateTitle();
            _panel.AddBinding(new ParameterBinding
            {
                Path = path,
                Get = get,
                Set = value => set(value is Enum ? value : Enum.ToObject(type, value)),
                Changed = _changed,
            });
            _refreshers.Add(() =>
            {
                long mask = read();
                foreach ((long bit, Toggle toggle) in toggles)
                {
                    toggle.SetValueWithoutNotify((mask & bit) != 0);
                }
                UpdateTitle();
            });
            return foldout;
        }

        private VisualElement CreateLayerMask(string label, string path, Func<object> get, Action<object> set, ParameterMeta meta)
        {
            var foldout = new CollapsibleGroup(string.Empty, false) { tooltip = meta.Tooltip };
            var toggles = new List<(int layer, Toggle toggle)>();

            Func<int> read = () => ((LayerMask)get()).value;
            for (int layer = 0; layer < 32; layer++)
            {
                string name = LayerMask.LayerToName(layer);
                if (string.IsNullOrEmpty(name)) continue;

                int captured = layer;
                var toggle = new Toggle(name) { value = (read() & (1 << layer)) != 0 };
                toggle.RegisterValueChangedCallback(evt =>
                {
                    int mask = read();
                    mask = evt.newValue ? mask | (1 << captured) : mask & ~(1 << captured);
                    set(new LayerMask { value = mask });
                    UpdateTitle();
                    _changed?.Invoke();
                });
                toggles.Add((layer, toggle));
                foldout.Add(toggle);
            }

            void UpdateTitle()
            {
                int mask = read();
                int on = toggles.Count(entry => (mask & (1 << entry.layer)) != 0);
                foldout.text = $"{label} ({on} of {toggles.Count} layers)";
            }

            UpdateTitle();
            _panel.AddBinding(new ParameterBinding
            {
                Path = path,
                Get = () => read(),
                Set = value => set(new LayerMask { value = Convert.ToInt32(value) }),
                Changed = _changed,
            });
            _refreshers.Add(() =>
            {
                int mask = read();
                foreach ((int layer, Toggle toggle) in toggles)
                {
                    toggle.SetValueWithoutNotify((mask & (1 << layer)) != 0);
                }
                UpdateTitle();
            });
            return foldout;
        }

        private VisualElement CreateReference(string label, string path, Func<object> get, ParameterMeta meta)
        {
            // Objects from the scene or project cannot be picked at runtime, so they are only shown.
            var text = new Label();
            text.AddToClassList("sensor-param-readonly");
            text.tooltip = meta.Tooltip;

            Action update = () =>
            {
                var target = get() as UnityEngine.Object;
                text.text = $"{label}: {(target != null ? target.name : "None")}";
            };
            update();
            _refreshers.Add(update);
            return text;
        }

        private VisualElement CreateNested(Type type, string label, string path, Func<object> get, Action<object> set)
        {
            var foldout = new CollapsibleGroup(label, false);
            if (!type.IsValueType && get() == null)
            {
                foldout.Add(new Label("None"));
                return foldout;
            }

            AddFields(foldout, type, get, set, path + ".");
            return foldout;
        }

        private VisualElement CreateList(Type listType, Type elementType, string label, string path, Func<object> get, Action<object> set, ParameterMeta meta)
        {
            var foldout = new CollapsibleGroup(string.Empty, true) { tooltip = meta.Tooltip };
            var body = new VisualElement();
            body.AddToClassList("sensor-param-list");
            var add = new Button { text = "+ Add" };
            add.AddToClassList("sensor-add-button");
            foldout.Add(body);
            foldout.Add(add);

            var rowRefreshers = new List<Action>();
            int builtCount = -1;
            bool isArray = listType.IsArray;

            void Rebuild()
            {
                _panel.RemoveBindingsUnder(path + "[");
                body.Clear();
                rowRefreshers = new List<Action>();

                var list = get() as IList;
                builtCount = list?.Count ?? 0;
                foldout.text = $"{label} ({builtCount})";
                if (list == null)
                {
                    body.Add(new Label("None"));
                    return;
                }

                List<Action> outer = _refreshers;
                _refreshers = rowRefreshers;
                try
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        body.Add(CreateRow(i));
                    }
                }
                finally
                {
                    _refreshers = outer;
                }
            }

            VisualElement CreateRow(int index)
            {
                var row = new VisualElement();
                row.AddToClassList("sensor-param-row");

                VisualElement control = CreateControl(
                    elementType,
                    $"Item {index + 1}",
                    $"{path}[{index}]",
                    () => ((IList)get())[index],
                    value => ((IList)get())[index] = value,
                    meta);
                control.AddToClassList("sensor-param-row-control");
                row.Add(control);

                var remove = new Button(() =>
                {
                    RemoveAt(index);
                    Rebuild();
                    _changed?.Invoke();
                })
                { text = "✕", tooltip = "Remove this item" };
                remove.AddToClassList("sensor-param-remove");
                row.Add(remove);
                return row;
            }

            void RemoveAt(int index)
            {
                if (!(get() is IList list) || index < 0 || index >= list.Count) return;

                if (isArray)
                {
                    Array shorter = Array.CreateInstance(elementType, list.Count - 1);
                    for (int i = 0, j = 0; i < list.Count; i++)
                    {
                        if (i != index) shorter.SetValue(list[i], j++);
                    }
                    set(shorter);
                }
                else
                {
                    list.RemoveAt(index);
                }
            }

            bool canCreate = TryCreateDefault(elementType, out _);
            add.SetEnabled(canCreate);
            add.clicked += () =>
            {
                if (!(get() is IList list) || !TryCreateDefault(elementType, out object created)) return;

                if (isArray)
                {
                    Array longer = Array.CreateInstance(elementType, list.Count + 1);
                    list.CopyTo(longer, 0);
                    longer.SetValue(created, list.Count);
                    set(longer);
                }
                else
                {
                    list.Add(created);
                }
                Rebuild();
                _changed?.Invoke();
            };

            Rebuild();
            _refreshers.Add(() =>
            {
                int count = (get() as IList)?.Count ?? 0;
                if (count != builtCount)
                {
                    Rebuild();
                }
                foreach (Action refresh in rowRefreshers.ToArray())
                {
                    refresh();
                }
            });
            return foldout;
        }

        private static bool TryGetListElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (type.IsArray && type.GetArrayRank() == 1)
            {
                elementType = type.GetElementType();
            }
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
            }
            return elementType != null;
        }

        private static bool IsNestedSerializable(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type.IsAbstract || type.IsInterface || type.IsGenericType) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;

            return Attribute.IsDefined(type, typeof(SerializableAttribute), false);
        }

        private static bool TryCreateDefault(Type type, out object value)
        {
            value = null;
            if (type == typeof(string))
            {
                value = string.Empty;
                return true;
            }

            if (type.IsValueType)
            {
                value = Activator.CreateInstance(type);
                return true;
            }

            if (!type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
            {
                value = Activator.CreateInstance(type);
                return true;
            }
            return false;
        }

        private static VisualElement Unsupported(string label, Type type)
        {
            var text = new Label($"{label}: not editable here ({type.Name})");
            text.AddToClassList("sensor-param-readonly");
            return text;
        }

        private static bool HasFocus(VisualElement element)
        {
            VisualElement focused = element.focusController?.focusedElement as VisualElement;
            return focused != null && (focused == element || element.Contains(focused));
        }
    }
}
