using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Spawns fake targets at random times, types and distances, simulating false positives.
    /// Spawn times are checked on each refresh, so the refresh rate limits their precision.
    /// </summary>
    [Serializable, ConditionMenu("Fake/Random detections")]
    public class FakeRandomReflecedEntitiesDetections : DetectionInjector
    {
        [Serializable]
        public class EntityWeights
        {
            public EntityType EntityType;
            public List<DistanceWeight> DistanceWeights = new List<DistanceWeight>();
        }

        private struct ActiveFake
        {
            public IDetectable Detectable;
            public float ExpirationTime;
        }

        public List<EntityWeights> EntityTypeToDistanceWeights = new List<EntityWeights>();
        [Min(0f)] public float MinTimeMinutes = 1f;
        [Min(0f)] public float MaxTimeMinutes = 2f;
        [Tooltip("How many independent spawn timers run at once.")]
        [Min(0)] public int LoopCount = 1;
        public bool UseInfiniteDetections;
        [Min(0)] public int MaxTotalDetections = 5;
        [Min(0f)] public float ExpirationTimeMinutes = 1f;

        [NonSerialized] private List<ActiveFake> _active;
        [NonSerialized] private float[] _nextSpawnTimes;

        protected Transform SensorTransform { get; private set; }
        protected Camera Camera { get; private set; }

        public override string Summary => $"Every {MinTimeMinutes:0.#}–{MaxTimeMinutes:0.#} min, lasts {ExpirationTimeMinutes:0.#} min";

        public override void Prepare(in SensorContext context)
        {
            SensorTransform = context.SensorTransform;
            Camera = context.Camera;
            _active ??= new List<ActiveFake>();

            float now = context.Now;
            _active.RemoveAll(fake => now >= fake.ExpirationTime);

            if (_nextSpawnTimes == null || _nextSpawnTimes.Length != LoopCount)
            {
                _nextSpawnTimes = new float[LoopCount];
                for (int i = 0; i < LoopCount; i++)
                {
                    _nextSpawnTimes[i] = now + GetRandomWaitSeconds();
                }
            }

            for (int i = 0; i < LoopCount; i++)
            {
                if (now < _nextSpawnTimes[i]) continue;

                _nextSpawnTimes[i] = now + GetRandomWaitSeconds();
                if (UseInfiniteDetections || _active.Count < MaxTotalDetections)
                {
                    TrySpawn(now);
                }
            }
        }

        public override void Collect(ICollection<IDetectable> detections)
        {
            if (_active == null) return;

            foreach (ActiveFake fake in _active)
            {
                detections.Add(fake.Detectable);
            }
        }

        public override void ResetState()
        {
            _active?.Clear();
            _nextSpawnTimes = null;
        }

        private float GetRandomWaitSeconds()
        {
            return UnityEngine.Random.Range(MinTimeMinutes, MaxTimeMinutes) * 60f;
        }

        private void TrySpawn(float now)
        {
            EntityWeights selected = PickEntity();
            if (selected == null) return;

            Vector3 position = GetDetectionPosition(PickDistance(selected.DistanceWeights));
            _active.Add(new ActiveFake
            {
                Detectable = new FakeDetectionInstance(position, selected.EntityType),
                ExpirationTime = now + ExpirationTimeMinutes * 60f,
            });
        }

        private EntityWeights PickEntity()
        {
            float total = EntityTypeToDistanceWeights.Sum(entity => GetTotalWeight(entity));
            if (total <= 0f) return null;

            float roll = UnityEngine.Random.Range(0f, total);
            foreach (EntityWeights entity in EntityTypeToDistanceWeights)
            {
                float weight = GetTotalWeight(entity);
                if (weight > 0f && roll <= weight) return entity;
                roll -= weight;
            }
            return EntityTypeToDistanceWeights.Last(entity => GetTotalWeight(entity) > 0f);
        }

        private static float GetTotalWeight(EntityWeights entity)
        {
            return entity.DistanceWeights.Sum(entry => entry.Weight);
        }

        private static float PickDistance(List<DistanceWeight> weights)
        {
            List<DistanceWeight> ordered = weights.OrderBy(entry => entry.Distance).ToList();
            float roll = UnityEngine.Random.Range(0f, ordered.Sum(entry => entry.Weight));
            float previous = 0f;
            foreach (DistanceWeight entry in ordered)
            {
                if (entry.Weight > 0f && roll <= entry.Weight)
                {
                    return UnityEngine.Random.Range(previous, entry.Distance);
                }
                roll -= entry.Weight;
                previous = entry.Distance;
            }
            return previous;
        }

        protected virtual Vector3 GetDetectionPosition(float distance)
        {
            Vector3 position = SensorTransform.position + UnityEngine.Random.onUnitSphere * distance;
            position.y = TerrainUtil.GetGroundHeight(position);
            return position;
        }
    }
}
