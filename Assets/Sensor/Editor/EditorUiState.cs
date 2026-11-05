using UnityEditor;

namespace SensorSystem
{
    /// <summary>Remembers open groups and filters across recompiles for as long as the Editor stays open.</summary>
    public sealed class SessionStateStore : IUiStateStore
    {
        public static readonly SessionStateStore Instance = new SessionStateStore();

        public bool GetBool(string key, bool fallback) => SessionState.GetBool(key, fallback);

        public void SetBool(string key, bool value) => SessionState.SetBool(key, value);

        public string GetString(string key, string fallback) => SessionState.GetString(key, fallback);

        public void SetString(string key, string value) => SessionState.SetString(key, value ?? string.Empty);
    }
}
