using System.Collections.Generic;
using UnityEditor;

namespace SensorSystem
{
    /// <summary>Every structural change to a settings asset's tree. Each one can be undone.</summary>
    public static class ConditionTreeEdits
    {
        public static Condition FindParent(SensorSettings settings, Condition node)
        {
            foreach (Condition candidate in ConditionTree.Flatten(settings.Root))
            {
                var children = new List<Condition>();
                candidate.CollectChildren(children);
                if (children.Contains(node)) return candidate;
            }
            return null;
        }

        public static void AddChild(SensorSettings settings, Condition parent, Condition child)
        {
            Record(settings, "Add condition");
            switch (parent)
            {
                case null:
                    settings.Root = child;
                    break;
                case MultiCondition multi:
                    multi.Children.Add(child);
                    break;
                case Not not:
                    not.Child = child;
                    break;
            }
            Commit(settings);
        }

        public static void Remove(SensorSettings settings, Condition node)
        {
            Record(settings, "Remove condition");
            Replace(settings, node, null);
            Commit(settings);
        }

        /// <summary>Removes a list entry whose class no longer exists.</summary>
        public static void RemoveMissingChild(SensorSettings settings, MultiCondition parent, int index)
        {
            Record(settings, "Remove condition");
            parent.Children.RemoveAt(index);
            Commit(settings);
        }

        public static void Wrap(SensorSettings settings, Condition node, Condition wrapper)
        {
            Record(settings, "Wrap condition");
            Replace(settings, node, wrapper);
            AttachChild(wrapper, node);
            Commit(settings);
        }

        /// <summary>An operator can dissolve if its children fit where it stands.</summary>
        public static bool CanUnwrap(SensorSettings settings, Condition node)
        {
            int childCount = GetChildren(node).Count;
            if (childCount == 0 || !IsOperator(node)) return false;
            return childCount == 1 || FindParent(settings, node) is MultiCondition;
        }

        public static void Unwrap(SensorSettings settings, Condition node)
        {
            if (!CanUnwrap(settings, node)) return;

            List<Condition> children = GetChildren(node);
            Record(settings, "Unwrap condition");
            if (children.Count == 1)
            {
                Replace(settings, node, children[0]);
            }
            else
            {
                var parent = (MultiCondition)FindParent(settings, node);
                int index = parent.Children.IndexOf(node);
                parent.Children.RemoveAt(index);
                parent.Children.InsertRange(index, children);
            }
            Commit(settings);
        }

        public static bool CanDuplicate(SensorSettings settings, Condition node)
        {
            return FindParent(settings, node) is MultiCondition;
        }

        public static void Duplicate(SensorSettings settings, Condition node)
        {
            if (!(FindParent(settings, node) is MultiCondition parent)) return;

            Condition copy = ConditionTree.Clone(node);
            Record(settings, "Duplicate condition");
            parent.Children.Insert(parent.Children.IndexOf(node) + 1, copy);
            Commit(settings);
        }

        public static bool CanMove(SensorSettings settings, Condition node, int delta)
        {
            if (!(FindParent(settings, node) is MultiCondition parent)) return false;

            int target = parent.Children.IndexOf(node) + delta;
            return target >= 0 && target < parent.Children.Count;
        }

        public static void Move(SensorSettings settings, Condition node, int delta)
        {
            if (!CanMove(settings, node, delta)) return;

            var parent = (MultiCondition)FindParent(settings, node);
            int index = parent.Children.IndexOf(node);
            Record(settings, "Move condition");
            parent.Children.RemoveAt(index);
            parent.Children.Insert(index + delta, node);
            Commit(settings);
        }

        /// <summary>A node can't move into itself or anything under it, and a full NOT has no room.</summary>
        public static bool CanMoveTo(Condition node, Condition newParent)
        {
            if (newParent == null || ConditionTree.Flatten(node).Contains(newParent)) return false;

            switch (newParent)
            {
                case MultiCondition _:
                    return true;
                case Not not:
                    return not.Child == null;
                default:
                    return false;
            }
        }

        public static void MoveTo(SensorSettings settings, Condition node, Condition newParent, int index)
        {
            if (!CanMoveTo(node, newParent)) return;

            Record(settings, "Move condition");
            if (FindParent(settings, node) is MultiCondition oldParent && oldParent == newParent && oldParent.Children.IndexOf(node) < index)
            {
                index--;
            }
            Replace(settings, node, null);

            if (newParent is MultiCondition multi)
            {
                multi.Children.Insert(System.Math.Min(index, multi.Children.Count), node);
            }
            else
            {
                AttachChild(newParent, node);
            }
            Commit(settings);
        }

        /// <summary>Operators keep their children when switching to another operator.</summary>
        public static void ChangeType(SensorSettings settings, Condition node, Condition replacement)
        {
            Record(settings, "Change condition type");
            List<Condition> children = GetChildren(node);
            Replace(settings, node, replacement);

            if (IsOperator(node) && children.Count > 0)
            {
                if (replacement is MultiCondition multi)
                {
                    multi.Children.AddRange(children);
                }
                else if (replacement is Not not)
                {
                    not.Child = children.Count == 1 ? children[0] : new And { Children = children };
                }
            }
            Commit(settings);
        }

        public static void AddInjector(SensorSettings settings, DetectionInjector injector)
        {
            Record(settings, "Add injector");
            settings.Injectors.Add(injector);
            Commit(settings);
        }

        public static void RemoveInjector(SensorSettings settings, int index)
        {
            Record(settings, "Remove injector");
            settings.Injectors.RemoveAt(index);
            Commit(settings);
        }

        public static void MoveInjector(SensorSettings settings, int index, int delta)
        {
            int target = index + delta;
            if (target < 0 || target >= settings.Injectors.Count) return;

            DetectionInjector injector = settings.Injectors[index];
            Record(settings, "Move injector");
            settings.Injectors.RemoveAt(index);
            settings.Injectors.Insert(target, injector);
            Commit(settings);
        }

        public static void DuplicateInjector(SensorSettings settings, int index)
        {
            DetectionInjector copy = ConditionTree.Clone(settings.Injectors[index]);
            Record(settings, "Duplicate injector");
            settings.Injectors.Insert(index + 1, copy);
            Commit(settings);
        }

        public static bool IsOperator(Condition node)
        {
            return node is MultiCondition || node is Not;
        }

        private static List<Condition> GetChildren(Condition node)
        {
            var children = new List<Condition>();
            node.CollectChildren(children);
            return children;
        }

        private static void AttachChild(Condition parent, Condition child)
        {
            if (parent is MultiCondition multi)
            {
                multi.Children.Add(child);
            }
            else if (parent is Not not)
            {
                not.Child = child;
            }
        }

        /// <summary>Puts replacement where node stands. A null replacement removes it.</summary>
        private static void Replace(SensorSettings settings, Condition node, Condition replacement)
        {
            if (settings.Root == node)
            {
                settings.Root = replacement;
                return;
            }

            switch (FindParent(settings, node))
            {
                case MultiCondition multi:
                    int index = multi.Children.IndexOf(node);
                    if (replacement != null)
                    {
                        multi.Children[index] = replacement;
                    }
                    else
                    {
                        multi.Children.RemoveAt(index);
                    }
                    break;
                case Not not:
                    not.Child = replacement;
                    break;
            }
        }

        private static void Record(SensorSettings settings, string name)
        {
            Undo.RecordObject(settings, name);
        }

        private static void Commit(SensorSettings settings)
        {
            EditorUtility.SetDirty(settings);
        }
    }
}
