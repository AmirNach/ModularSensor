using System;
using UnityEngine;

namespace SensorSystem
{
    public class FakeDetectionInstance : IDetectable, IEntityTyped
    {
        private readonly Vector3 _position;
        private readonly string _id = Guid.NewGuid().ToString();

        public Bounds Bounds { get; set; }
        public bool IsFake => true;
        public EntityType EntityType { get; }

        public FakeDetectionInstance(Vector3 position, EntityType entityType)
        {
            _position = position;
            EntityType = entityType;
            Bounds = new Bounds(position, Vector3.one);
        }

        public string GetIdentifier() => _id;
        public Vector3 GetCenterPosition() => _position;
        public string GetDetectionType() => EntityType.ToString();
        public float GetVelocity() => -1f;
    }
}
