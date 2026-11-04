namespace SensorSystem
{
    /// <summary>Placeholder entity categories. Edit freely to match your project.</summary>
    public enum EntityType
    {
        Unknown,
        Person,
        Vehicle,
        Aircraft,
        Drone,
    }

    public interface IEntityTyped
    {
        EntityType EntityType { get; }
    }

    public static class EntityTypeExtensions
    {
        public static bool Matches(this EntityType pattern, EntityType entityType)
        {
            return pattern == entityType;
        }
    }
}
