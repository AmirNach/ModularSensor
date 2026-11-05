using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// A copy of what a detectable looked like the last time it was seen. It stays valid after the target is destroyed,
    /// so a lost detection can still say what it was. It is an IDetectable that never throws.
    /// </summary>
    public sealed class DetectableSnapshot : IDetectable, IEntityTyped
    {
        public string Identifier { get; private set; } = string.Empty;
        public string DetectionType { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public EntityType EntityType { get; private set; } = EntityType.Unknown;
        public bool IsFake { get; private set; }
        public Vector3 Position { get; private set; }
        public float Speed { get; private set; }

        /// <summary>The time, from Time.time, when it was last read from the live target.</summary>
        public float LastSeenTime { get; private set; }

        public Bounds Bounds { get; set; }
        public bool IsValid => true;

        public string GetIdentifier() => Identifier;
        public Vector3 GetCenterPosition() => Position;
        public string GetDetectionType() => DetectionType;
        public float GetVelocity() => Speed;

        /// <summary>Makes a snapshot of a live target under the ID the sensor tracks it by.</summary>
        public static DetectableSnapshot Of(IDetectable live, string identifier)
        {
            var snapshot = new DetectableSnapshot { Identifier = identifier ?? string.Empty };
            snapshot.Update(live);
            return snapshot;
        }

        /// <summary>Copies the target's current values. Returns false, keeping the old values, when it cannot be read.</summary>
        public bool Update(IDetectable live)
        {
            if (!live.IsAlive()) return false;

            try
            {
                // Read everything first, so a failure half way leaves the snapshot as it was.
                string type = live.GetDetectionType() ?? string.Empty;
                string name = TargetListModel.GetName(live) ?? string.Empty;
                Vector3 position = live.GetCenterPosition();
                float speed = live.GetVelocity();
                Bounds bounds = live.Bounds;
                bool isFake = live.IsFake;
                EntityType entityType = live is IEntityTyped typed ? typed.EntityType : EntityType.Unknown;

                DetectionType = type;
                Name = name;
                Position = position;
                Speed = speed;
                Bounds = bounds;
                IsFake = isFake;
                EntityType = entityType;
                LastSeenTime = Time.time;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Takes over another snapshot's values, keeping this one's ID.</summary>
        public void CopyFrom(DetectableSnapshot other)
        {
            if (other == null || ReferenceEquals(other, this)) return;

            DetectionType = other.DetectionType;
            Name = other.Name;
            EntityType = other.EntityType;
            IsFake = other.IsFake;
            Position = other.Position;
            Speed = other.Speed;
            Bounds = other.Bounds;
            LastSeenTime = other.LastSeenTime;
        }
    }
}
