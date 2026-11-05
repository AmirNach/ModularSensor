using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    public enum TargetState
    {
        Passed,
        Failed,
        Injected,
    }

    /// <summary>One object in a live target list.</summary>
    public readonly struct TargetRow
    {
        public readonly IDetectable Target;
        public readonly string Name;
        public readonly TargetState State;
        public readonly float Distance;
        public readonly string Detail;

        public TargetRow(IDetectable target, string name, TargetState state, float distance, string detail)
        {
            Target = target;
            Name = name;
            State = state;
            Distance = distance;
            Detail = detail;
        }
    }

    /// <summary>The logic behind the live target lists: names, filtering, grouping and sorting. No UI.</summary>
    public static class TargetListModel
    {
        public const int StateCount = 3;
        private const int ShortIdLength = 4;

        public sealed class Groups
        {
            /// <summary>Rows that match the filter, sorted, per state.</summary>
            public List<TargetRow>[] Shown { get; } = { new List<TargetRow>(), new List<TargetRow>(), new List<TargetRow>() };

            /// <summary>Every row per state, ignoring the filter.</summary>
            public int[] Totals { get; } = new int[StateCount];
        }

        public static string GetName(IDetectable target)
        {
            if (target is Component component)
            {
                return component != null ? component.name : "(destroyed)";
            }

            if (target.IsFake)
            {
                // Fakes have no name of their own, so a short ID tells them apart.
                string id = target.GetIdentifier() ?? string.Empty;
                string shortId = id.Length <= ShortIdLength ? id : id.Substring(0, ShortIdLength);
                return $"Fake {target.GetDetectionType()} {shortId}".TrimEnd();
            }

            return target.GetIdentifier();
        }

        /// <summary>True when every word of the filter appears in the name, in any order, ignoring case.</summary>
        public static bool Matches(string name, string filter)
        {
            return MatchesAll(name, SplitWords(filter));
        }

        public static Groups Build(IEnumerable<TargetRow> rows, string filter)
        {
            var groups = new Groups();
            string[] words = SplitWords(filter);

            foreach (TargetRow row in rows)
            {
                int index = (int)row.State;
                groups.Totals[index]++;
                if (MatchesAll(row.Name, words))
                {
                    groups.Shown[index].Add(row);
                }
            }

            foreach (List<TargetRow> shown in groups.Shown)
            {
                shown.Sort(CompareRows);
            }
            return groups;
        }

        /// <summary>"Passed (12)", or "Passed (3 of 12)" while filtering.</summary>
        public static string FormatCount(string title, int shown, int total, bool isFiltering)
        {
            return isFiltering ? $"{title} ({shown} of {total})" : $"{title} ({total})";
        }

        /// <summary>Compares text so that "Target 2" comes before "Target 10".</summary>
        public static int NaturalCompare(string a, string b)
        {
            a = a ?? string.Empty;
            b = b ?? string.Empty;
            int i = 0;
            int j = 0;

            while (i < a.Length && j < b.Length)
            {
                if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
                {
                    int startA = i;
                    int startB = j;
                    while (i < a.Length && char.IsDigit(a[i])) i++;
                    while (j < b.Length && char.IsDigit(b[j])) j++;

                    string numberA = a.Substring(startA, i - startA).TrimStart('0');
                    string numberB = b.Substring(startB, j - startB).TrimStart('0');
                    if (numberA.Length != numberB.Length) return numberA.Length.CompareTo(numberB.Length);

                    int byDigits = string.CompareOrdinal(numberA, numberB);
                    if (byDigits != 0) return byDigits;
                }
                else
                {
                    int byChar = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                    if (byChar != 0) return byChar;
                    i++;
                    j++;
                }
            }
            return (a.Length - i).CompareTo(b.Length - j);
        }

        private static int CompareRows(TargetRow a, TargetRow b)
        {
            int byName = NaturalCompare(a.Name, b.Name);
            if (byName != 0) return byName;

            int byDistance = a.Distance.CompareTo(b.Distance);
            if (byDistance != 0) return byDistance;

            return string.CompareOrdinal(a.Target.GetIdentifier(), b.Target.GetIdentifier());
        }

        private static string[] SplitWords(string filter)
        {
            return string.IsNullOrWhiteSpace(filter)
                ? Array.Empty<string>()
                : filter.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static bool MatchesAll(string name, string[] words)
        {
            foreach (string word in words)
            {
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            return true;
        }
    }
}
