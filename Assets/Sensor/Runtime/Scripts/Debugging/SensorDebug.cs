using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// The one place game code talks to the QA overlay. It knows commands and receivers, nothing else,
    /// so callers never reference the overlay or any UI class.
    /// </summary>
    public static class SensorDebug
    {
        private static readonly List<ISensorDebugReceiver> Receivers = new List<ISensorDebugReceiver>();

        /// <summary>True when an overlay is listening. False in release builds, where the overlay switches itself off.</summary>
        public static bool HasReceiver => Receivers.Count > 0;

        public static void Register(ISensorDebugReceiver receiver)
        {
            if (receiver != null && !Receivers.Contains(receiver))
            {
                Receivers.Add(receiver);
            }
        }

        public static void Unregister(ISensorDebugReceiver receiver)
        {
            Receivers.Remove(receiver);
        }

        /// <summary>Sends the command to every overlay. Returns false when nothing is listening, which is harmless.</summary>
        public static bool Dispatch(ISensorDebugCommand command)
        {
            if (command == null || Receivers.Count == 0) return false;

            // A command may register or remove receivers, so work on a copy.
            foreach (ISensorDebugReceiver receiver in Receivers.ToArray())
            {
                command.Execute(receiver);
            }
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Receivers.Clear();
        }
    }
}
