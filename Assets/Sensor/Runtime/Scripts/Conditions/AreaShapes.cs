using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>One polygon of an "In any polygon" condition: 3 or more X/Z points in order around the shape.</summary>
    [Serializable]
    public class AreaPolygon
    {
        [Tooltip("This polygon's name, unique in the condition: shown in the list, and used from code to change or remove it (UpdatePolygon, RemovePolygon, FindPolygon). A copy or an empty one is renamed when the game starts.")]
        public string Label = "";

        [Tooltip("3 or more corners in order around the shape. x is world X and y is world Z (or right and front of the sensor when relative). A polygon with fewer than 3 points is ignored.")]
        public List<Vector2> Points = new List<Vector2>();

        public AreaPolygon() { }

        public AreaPolygon(IEnumerable<Vector2> points, string label = "")
        {
            Label = label ?? "";
            if (points != null) Points.AddRange(points);
        }
    }

    /// <summary>
    /// Labels name the shapes inside one area and are unique there. A wanted label that is empty gets "Polygon 3" (the kind and
    /// the first free number from the count up). One that is taken gets " (2)", " (3)" and so on added, except a taken automatic
    /// label ("Polygon 1", which is what the inspector's + button copies), which gets the next free number instead.
    /// </summary>
    internal static class AreaLabels
    {
        public static string Unique<TState>(string wanted, string kind, int count, TState state, Func<TState, string, bool> isTaken)
        {
            bool isFree = !string.IsNullOrWhiteSpace(wanted) && !isTaken(state, wanted);
            if (isFree) return wanted;

            if (string.IsNullOrWhiteSpace(wanted) || IsAutomatic(wanted, kind))
            {
                int number = count + 1;
                while (isTaken(state, $"{kind} {number}")) number++;
                return $"{kind} {number}";
            }

            int copy = 2;
            while (isTaken(state, $"{wanted} ({copy})")) copy++;
            return $"{wanted} ({copy})";
        }

        /// <summary>Is this "Polygon 12" for the kind "Polygon"?</summary>
        private static bool IsAutomatic(string label, string kind)
        {
            if (label.Length <= kind.Length + 1 || !label.StartsWith(kind + " ", StringComparison.Ordinal)) return false;
            for (int i = kind.Length + 1; i < label.Length; i++)
            {
                if (!char.IsDigit(label[i])) return false;
            }
            return true;
        }
    }

    /// <summary>One circle of an "In any circle" condition: an X/Z center and a radius.</summary>
    [Serializable]
    public class AreaCircle
    {
        [Tooltip("This circle's name, unique in the condition: shown in the list, and used from code to change or remove it (UpdateCircle, RemoveCircle, FindCircle). A copy or an empty one is renamed when the game starts.")]
        public string Label = "";

        [Tooltip("x is world X and y is world Z (or right and front of the sensor when relative).")]
        public Vector2 Center;

        [Min(0f)] public float Radius = 5f;

        public AreaCircle() { }

        public AreaCircle(Vector2 center, float radius, string label = "")
        {
            Center = center;
            Radius = Mathf.Max(0f, radius);
            Label = label ?? "";
        }
    }
}
