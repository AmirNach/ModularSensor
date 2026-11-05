using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Shows a condition field in the inspector and in the QA overlay only while another bool field of the same object is on.
    /// The value is still stored when hidden.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ShowIfAttribute : Attribute
    {
        public string BoolField { get; }

        public ShowIfAttribute(string boolField)
        {
            BoolField = boolField;
        }
    }
}
