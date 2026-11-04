using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Finds the conditions and injectors available to add. New classes appear with no registration.</summary>
    public static class ConditionTypes
    {
        public const string OperatorsGroup = "Operators/";

        public readonly struct Entry
        {
            public readonly Type Type;
            public readonly string Path;

            public Entry(Type type, string path)
            {
                Type = type;
                Path = path;
            }

            /// <summary>Unity can only store a class marked [Serializable].</summary>
            public bool IsUsable => Type.IsSerializable;
        }

        public static List<Entry> GetEntries(Type baseType)
        {
            return TypeCache.GetTypesDerivedFrom(baseType)
                .Where(type => !type.IsAbstract && !type.IsGenericType && !(type.IsNested && !type.IsNestedPublic) && type.GetConstructor(Type.EmptyTypes) != null)
                .Select(type => new Entry(type, GetPath(type)))
                .OrderBy(entry => entry.Path.StartsWith(OperatorsGroup) ? 0 : 1)
                .ThenBy(entry => entry.Path, StringComparer.Ordinal)
                .ToList();
        }

        private static string GetPath(Type type)
        {
            var attribute = type.GetCustomAttribute<ConditionMenuAttribute>();
            return attribute != null ? attribute.Path : $"Other/{ConditionTree.GetDisplayName(type)}";
        }
    }

    /// <summary>The searchable add menu, shared by conditions and injectors.</summary>
    public class TypePickerDropdown : AdvancedDropdown
    {
        private class TypeItem : AdvancedDropdownItem
        {
            public Type Type { get; }

            public TypeItem(string name, Type type) : base(name)
            {
                Type = type;
            }
        }

        private readonly Type _baseType;
        private readonly string _title;
        private readonly Action<Type> _onPicked;

        private TypePickerDropdown(Type baseType, string title, Action<Type> onPicked) : base(new AdvancedDropdownState())
        {
            _baseType = baseType;
            _title = title;
            _onPicked = onPicked;
            minimumSize = new Vector2(280f, 340f);
        }

        public static void Show(Rect rect, Type baseType, string title, Action<Type> onPicked)
        {
            new TypePickerDropdown(baseType, title, onPicked).Show(rect);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(_title);
            var groups = new Dictionary<string, AdvancedDropdownItem>();

            foreach (ConditionTypes.Entry entry in ConditionTypes.GetEntries(_baseType))
            {
                string[] parts = entry.Path.Split('/');
                AdvancedDropdownItem parent = root;
                string groupPath = string.Empty;

                for (int i = 0; i < parts.Length - 1; i++)
                {
                    groupPath += parts[i] + "/";
                    if (!groups.TryGetValue(groupPath, out AdvancedDropdownItem group))
                    {
                        group = new AdvancedDropdownItem(parts[i]);
                        groups.Add(groupPath, group);
                        parent.AddChild(group);
                    }
                    parent = group;
                }

                string name = parts[parts.Length - 1];
                parent.AddChild(new TypeItem(entry.IsUsable ? name : $"{name} (add [Serializable] to the class)", entry.Type) { enabled = entry.IsUsable });
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is TypeItem typeItem && typeItem.enabled)
            {
                _onPicked(typeItem.Type);
            }
        }
    }
}
