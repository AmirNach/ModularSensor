using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace SensorSystem
{
    public static class ConditionTree
    {
        [Serializable]
        private class Snapshot
        {
            [SerializeReference] public Condition Root;
            [SerializeReference] public List<DetectionInjector> Injectors = new List<DetectionInjector>();
        }

        /// <summary>A type name split into words, e.g. "InFrustum" becomes "In Frustum".</summary>
        public static string GetDisplayName(Type type)
        {
            return Regex.Replace(type.Name, "(?<=[a-z0-9])(?=[A-Z])", " ");
        }

        /// <summary>Lists every node in depth-first order.</summary>
        public static List<Condition> Flatten(Condition root)
        {
            var nodes = new List<Condition>();
            if (root != null) AddDepthFirst(root, nodes);
            return nodes;
        }

        private static void AddDepthFirst(Condition node, List<Condition> nodes)
        {
            nodes.Add(node);
            var children = new List<Condition>();
            node.CollectChildren(children);
            foreach (Condition child in children)
            {
                AddDepthFirst(child, nodes);
            }
        }

        /// <summary>Copies a tree and its injectors, so sensors sharing a settings asset do not share runtime state.</summary>
        public static void Clone(Condition root, List<DetectionInjector> injectors, out Condition rootCopy, out List<DetectionInjector> injectorsCopy)
        {
            var original = new Snapshot { Root = root, Injectors = injectors ?? new List<DetectionInjector>() };
            Snapshot copy = JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(original));
            rootCopy = copy.Root;
            injectorsCopy = copy.Injectors ?? new List<DetectionInjector>();
        }

        public static Condition Clone(Condition root)
        {
            Clone(root, null, out Condition rootCopy, out _);
            return rootCopy;
        }

        public static DetectionInjector Clone(DetectionInjector injector)
        {
            Clone(null, new List<DetectionInjector> { injector }, out _, out List<DetectionInjector> injectorsCopy);
            return injectorsCopy.FirstOrDefault();
        }

        /// <summary>A readable one-line expression, e.g. "In Frustum AND (Is Moving OR NOT With Raycast)".</summary>
        public static string Describe(Condition root)
        {
            return root == null ? "(empty)" : DescribeNode(root);
        }

        private static string DescribeNode(Condition node)
        {
            var children = new List<Condition>();
            node.CollectChildren(children);

            switch (node)
            {
                case MultiCondition _:
                    return children.Count == 0
                        ? $"{node.DisplayName} (empty)"
                        : string.Join($" {node.DisplayName} ", children.Select(DescribeOperand));
                case Not _:
                    return children.Count == 0 ? $"{node.DisplayName} (empty)" : $"{node.DisplayName} {DescribeOperand(children[0])}";
                default:
                    return node.DisplayName;
            }
        }

        private static string DescribeOperand(Condition node)
        {
            return node is MultiCondition ? $"({DescribeNode(node)})" : DescribeNode(node);
        }
    }
}
