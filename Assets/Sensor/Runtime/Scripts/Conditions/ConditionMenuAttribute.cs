using System;

namespace SensorSystem
{
    /// <summary>Optional. Sets where a condition or source appears in the inspector's add menu, e.g. "Spatial/In frustum".</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ConditionMenuAttribute : Attribute
    {
        public string Path { get; }

        public ConditionMenuAttribute(string path)
        {
            Path = path;
        }
    }
}
