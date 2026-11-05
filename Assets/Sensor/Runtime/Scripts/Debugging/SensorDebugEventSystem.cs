using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// UI Toolkit only gets the mouse through an EventSystem. If the scene has none, this adds one that
    /// matches the project's input setup. It finds the types by name, so Sensor.Runtime depends on neither
    /// the UI package nor the Input System package.
    /// </summary>
    public static class SensorDebugEventSystem
    {
        private const string EventSystemType = "UnityEngine.EventSystems.EventSystem, UnityEngine.UI";
        private const string InputSystemModuleType = "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";
        private const string StandaloneModuleType = "UnityEngine.EventSystems.StandaloneInputModule, UnityEngine.UI";

        /// <summary>Returns what it did: "present", "created with the Input System module", "created with the standalone module" or "unavailable".</summary>
        public static string EnsureExists()
        {
            Type eventSystem = Type.GetType(EventSystemType);
            if (eventSystem == null) return "unavailable";
            if (UnityEngine.Object.FindFirstObjectByType(eventSystem) != null) return "present";

            var holder = new GameObject("EventSystem (sensor debug)");
            holder.AddComponent(eventSystem);

            Type inputSystemModule = Type.GetType(InputSystemModuleType);
            if (inputSystemModule != null)
            {
                var module = (Component)holder.AddComponent(inputSystemModule);
                inputSystemModule.GetMethod("AssignDefaultActions")?.Invoke(module, null);
                return "created with the Input System module";
            }

            Type standalone = Type.GetType(StandaloneModuleType);
            if (standalone != null)
            {
                holder.AddComponent(standalone);
            }
            return "created with the standalone module";
        }
    }
}
