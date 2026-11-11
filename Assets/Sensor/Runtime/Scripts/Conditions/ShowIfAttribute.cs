using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Shows a field in the inspector and in the QA overlay only while another bool field of the same object is on, or, with
    /// [ShowIf(nameof(Unlimited), false)], only while it is off. The value is still stored when hidden.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ShowIfAttribute : Attribute
    {
        public string BoolField { get; }

        /// <summary>The value of the bool field that shows this field. True by default.</summary>
        public bool ShowWhen { get; }

        public ShowIfAttribute(string boolField, bool showWhen = true)
        {
            BoolField = boolField;
            ShowWhen = showWhen;
        }
    }
}
