using System.Linq;

namespace SensorSystem
{
    public enum NodeStatusKind
    {
        None,
        Passed,
        Failed,
        NotReached,
    }

    /// <summary>What happened to one condition for the object picked in the debug view.</summary>
    public static class NodeStatus
    {
        public static NodeStatusKind Get(Sensor sensor, Condition node)
        {
            IDetectable target = sensor.DebugTarget;
            // Injected objects bypass the tree, so no condition decided anything about them.
            if (target == null || sensor.WasInjected(target)) return NodeStatusKind.None;

            if (!sensor.DebugResults.TryGetValue(node, out bool result)) return NodeStatusKind.NotReached;
            return result ? NodeStatusKind.Passed : NodeStatusKind.Failed;
        }

        public static string Text(NodeStatusKind kind)
        {
            switch (kind)
            {
                case NodeStatusKind.Passed: return "✓ Passed";
                case NodeStatusKind.Failed: return "✗ Failed";
                case NodeStatusKind.NotReached: return "Not reached";
                default: return string.Empty;
            }
        }
    }
}
