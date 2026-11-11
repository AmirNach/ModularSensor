using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.Serialization;

namespace SensorSystem
{
    /// <summary>
    /// Spawns fake targets at random times, types and distances, simulating false positives.
    /// Spawn times are checked on each refresh, so the refresh rate limits their precision.
    /// </summary>
    // Assets saved before the spelling fix still load under the old name.
    [MovedFrom(true, "SensorSystem", "Sensor.Runtime", "FakeRandomReflecedEntitiesDetections")]
    [Serializable, ConditionMenu("Fake/Random detections")]
    public class FakeRandomReflectedEntitiesDetections : DetectionInjector
    {
        /// <summary>One type a created fake can be: how many of the created fakes are this type, and at which distances.</summary>
        [Serializable]
        public class FakeEntity
        {
            public EntityType EntityType;

            [Tooltip("Of the fakes that ARE created (after Spawn Chance), the percent that are this type. All types together add up to 100%. Example: Spawn Chance 30% and this 50% means about 15% of timer firings make a fake of this type.")]
            [FormerlySerializedAs("Percent")]
            [Range(0f, 100f)] public float PercentOfCreatedFakes = 100f;

            [Tooltip("Where this type's fakes appear: distance bands, each with the percent of this type's fakes in it. They add up to 100%.")]
            [FormerlySerializedAs("DistanceWeights")]
            public List<DistancePercent> Distances = new List<DistancePercent>();
        }

        private struct ActiveFake
        {
            public IDetectable Detectable;
            public float SpawnTime;
        }

        // The fields are in the order things happen: a timer fires, a chance decides whether a fake is created, and a created
        // fake gets a type and a distance. The FormerlySerializedAs names keep values saved before the renames.

        [Header("1. When: spawn timers")]
        [Tooltip("Minutes between timer firings: a random time between min and max, picked again after each firing. The first firing waits too.")]
        [FormerlySerializedAs("MinTimeMinutes")]
        [Min(0f)] public float MinSpawnIntervalMinutes = 1f;

        [Tooltip("Minutes between timer firings: a random time between min and max, picked again after each firing. The first firing waits too.")]
        [FormerlySerializedAs("MaxTimeMinutes")]
        [Min(0f)] public float MaxSpawnIntervalMinutes = 2f;

        [Tooltip("How many independent spawn timers run at once. 3 fires about three times as often.")]
        [FormerlySerializedAs("LoopCount")]
        [Min(0)] public int SpawnTimers = 1;

        [Header("2. Whether: chance a firing creates a fake")]
        [Tooltip("Each time a timer fires, the chance that a fake is created at all. When it is not, nothing comes and the timer waits again.")]
        [Range(0f, 100f)] public float SpawnChancePercent = 100f;

        [Header("3. What: each created fake's type and distance")]
        [Tooltip("The types a created fake can be. Their Percent Of Created Fakes add up to 100%, since every created fake gets a type. Each type's distances add up to 100% too.")]
        [FormerlySerializedAs("Entities")]
        [FormerlySerializedAs("EntityTypeToDistanceWeights")]
        public List<FakeEntity> FakeTypes = new List<FakeEntity>
        {
            new FakeEntity
            {
                EntityType = EntityType.Person,
                PercentOfCreatedFakes = 100f,
                Distances = { new DistancePercent { UpToMeters = 100f, PercentOfThisType = 100f } },
            },
        };

        // The header is on the first field, which is never hidden, so it does not disappear with a hidden field.
        [Header("4. How many and how long")]
        [Tooltip("On: no limit on how many fakes exist at once.")]
        [FormerlySerializedAs("UseInfiniteDetections")]
        public bool Unlimited;

        [Tooltip("How many fakes can exist at the same time (not in total). A timer that fires while at the limit creates nothing.")]
        [FormerlySerializedAs("MaxTotalDetections")]
        [ShowIf(nameof(Unlimited), false)]
        [Min(0)] public int MaxActiveFakes = 5;

        [Tooltip("On: a fake stays until the sensor turns off or is reset, and is never lost on its own. With Unlimited on too, fakes keep piling up.")]
        public bool NeverLost;

        [Tooltip("Minutes each fake lasts before it is lost.")]
        [FormerlySerializedAs("ExpirationTimeMinutes")]
        [ShowIf(nameof(NeverLost), false)]
        [Min(0f)] public float LifetimeMinutes = 1f;

        public override string DisplayName => "Random fake detections";

        [NonSerialized] private List<ActiveFake> _active;
        [NonSerialized] private float[] _nextSpawnTimes;

        protected Transform SensorTransform { get; private set; }
        protected Camera Camera { get; private set; }

        public override string Summary
        {
            get
            {
                string chance = SpawnChancePercent >= 100f ? "" : $" at {SpawnChancePercent:0.#}%";
                string lasts = NeverLost ? "never lost" : $"lasts {LifetimeMinutes:0.#} min";
                return $"Every {MinSpawnIntervalMinutes:0.#}–{MaxSpawnIntervalMinutes:0.#} min{chance}, {lasts}";
            }
        }

        public override void Prepare(in SensorContext context)
        {
            SensorTransform = context.SensorTransform;
            Camera = context.Camera;
            _active ??= new List<ActiveFake>();

            // Checked against the current settings, so changing Lifetime Minutes or Never Lost applies to the fakes already there.
            float now = context.Now;
            if (!NeverLost)
            {
                float lifetime = LifetimeMinutes * 60f;
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    if (now >= _active[i].SpawnTime + lifetime) _active.RemoveAt(i);
                }
            }

            if (_nextSpawnTimes == null || _nextSpawnTimes.Length != SpawnTimers)
            {
                _nextSpawnTimes = new float[SpawnTimers];
                for (int i = 0; i < SpawnTimers; i++)
                {
                    _nextSpawnTimes[i] = now + GetRandomWaitSeconds();
                }
            }

            for (int i = 0; i < SpawnTimers; i++)
            {
                if (now < _nextSpawnTimes[i]) continue;

                _nextSpawnTimes[i] = now + GetRandomWaitSeconds();
                if ((Unlimited || _active.Count < MaxActiveFakes) && RollSpawnChance())
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

        private bool RollSpawnChance()
        {
            if (SpawnChancePercent >= 100f) return true;
            return SpawnChancePercent > 0f && UnityEngine.Random.Range(0f, 100f) < SpawnChancePercent;
        }

        private float GetRandomWaitSeconds()
        {
            return UnityEngine.Random.Range(MinSpawnIntervalMinutes, MaxSpawnIntervalMinutes) * 60f;
        }

        // ---------- Percent check ----------

        private const float PercentTolerance = 0.01f;

        /// <summary>
        /// What is wrong with the percents, or null when the types add up to 100% and so do the distances of each type that can come.
        /// Shown on the injector's card. Until fixed, the percents are used as shares of their total, so 30% and 30% act like 50% and 50%.
        /// </summary>
        public override string Warning
        {
            get
            {
                if (FakeTypes == null || FakeTypes.Count == 0) return "No fake types, so nothing is created";

                var problems = new List<string>();
                float total = FakeTypes.Sum(entity => entity != null ? entity.PercentOfCreatedFakes : 0f);
                if (Mathf.Abs(total - 100f) > PercentTolerance)
                {
                    problems.Add($"Percent Of Created Fakes adds up to {total:0.##}% across the types, not 100%");
                }

                foreach (FakeEntity entity in FakeTypes)
                {
                    if (entity == null || entity.PercentOfCreatedFakes <= 0f) continue;

                    float distances = DistanceTotal(entity);
                    if (entity.Distances == null || entity.Distances.Count == 0)
                    {
                        problems.Add($"{entity.EntityType} has no distances");
                    }
                    else if (Mathf.Abs(distances - 100f) > PercentTolerance)
                    {
                        problems.Add($"{entity.EntityType}: Percent Of This Type adds up to {distances:0.##}% across its distances, not 100%");
                    }
                }
                return problems.Count == 0 ? null : string.Join(". ", problems);
            }
        }

        private static float DistanceTotal(FakeEntity entity)
        {
            return entity?.Distances?.Sum(entry => entry != null ? entry.PercentOfThisType : 0f) ?? 0f;
        }

        // ---------- Spawning ----------

        private void TrySpawn(float now)
        {
            FakeEntity selected = PickEntity();
            if (selected == null) return;

            Vector3 position = GetDetectionPosition(PickDistance(selected.Distances));
            _active.Add(new ActiveFake
            {
                Detectable = new FakeDetectionInstance(position, selected.EntityType),
                SpawnTime = now,
            });
        }

        // A type can come when it has a percent and at least one distance with a percent.
        private static float ChanceOf(FakeEntity entity)
        {
            return entity != null && entity.PercentOfCreatedFakes > 0f && DistanceTotal(entity) > 0f ? entity.PercentOfCreatedFakes : 0f;
        }

        private FakeEntity PickEntity()
        {
            if (FakeTypes == null) return null;

            float total = FakeTypes.Sum(entity => ChanceOf(entity));
            if (total <= 0f) return null;

            float roll = UnityEngine.Random.Range(0f, total);
            foreach (FakeEntity entity in FakeTypes)
            {
                float chance = ChanceOf(entity);
                if (chance > 0f && roll <= chance) return entity;
                roll -= chance;
            }
            return FakeTypes.Last(entity => ChanceOf(entity) > 0f);
        }

        private static float PickDistance(List<DistancePercent> distances)
        {
            List<DistancePercent> ordered = distances.Where(entry => entry != null).OrderBy(entry => entry.UpToMeters).ToList();
            float roll = UnityEngine.Random.Range(0f, ordered.Sum(entry => entry.PercentOfThisType));
            float previous = 0f;
            foreach (DistancePercent entry in ordered)
            {
                if (entry.PercentOfThisType > 0f && roll <= entry.PercentOfThisType)
                {
                    return UnityEngine.Random.Range(previous, entry.UpToMeters);
                }
                roll -= entry.PercentOfThisType;
                previous = entry.UpToMeters;
            }
            return previous;
        }

        /// <summary>
        /// A point on the ground at this distance from the sensor, measured flat (X/Z), in a random direction.
        /// The direction is flat on purpose: a random 3D direction would put most fakes much nearer than the chosen
        /// distance once they are dropped to the ground (straight up or down is a distance of zero).
        /// </summary>
        protected virtual Vector3 GetDetectionPosition(float distance)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 position = SensorTransform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            position.y = TerrainUtil.GetGroundHeight(position);
            return position;
        }
    }
}
