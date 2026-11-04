using System.Collections.Generic;

namespace SensorSystem
{
    /// <summary>Defines how a Sensor gets every object it may detect. Implement this to plug in your own source.</summary>
    public interface IAllObjectsCollection : IEnabled
    {
        HashSet<IDetectable> AllObjects { get; }
    }
}
