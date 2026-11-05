using System.Collections.Generic;

namespace SensorSystem
{
    /// <summary>Where the debug views remember open groups and filter text.</summary>
    public interface IUiStateStore
    {
        bool GetBool(string key, bool fallback);
        void SetBool(string key, bool value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
    }

    /// <summary>Remembers state for as long as the application runs.</summary>
    public sealed class MemoryUiStateStore : IUiStateStore
    {
        public static readonly MemoryUiStateStore Shared = new MemoryUiStateStore();

        private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

        public bool GetBool(string key, bool fallback)
        {
            return _values.TryGetValue(key, out string value) ? value == "1" : fallback;
        }

        public void SetBool(string key, bool value)
        {
            _values[key] = value ? "1" : "0";
        }

        public string GetString(string key, string fallback)
        {
            return _values.TryGetValue(key, out string value) ? value : fallback;
        }

        public void SetString(string key, string value)
        {
            _values[key] = value ?? string.Empty;
        }
    }
}
