using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    /// <summary>The fake detection injector driven by hand: each Prepare is one sensor refresh at the given time in seconds.</summary>
    public class FakeDetectionsTests
    {
        // Far from anything in the open scene, so the ground check below finds nothing and keeps the height.
        private static readonly Vector3 SensorPosition = new Vector3(50000f, 0f, 50000f);

        private GameObject _sensor;

        [SetUp]
        public void SetUp()
        {
            _sensor = new GameObject("sensor");
            _sensor.transform.position = SensorPosition;
            Random.InitState(1234);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sensor);
        }

        private static FakeRandomReflectedEntitiesDetections Injector(float everyMinutes, float lastsMinutes, int max = 5)
        {
            return new FakeRandomReflectedEntitiesDetections
            {
                MinSpawnIntervalMinutes = everyMinutes,
                MaxSpawnIntervalMinutes = everyMinutes,
                LifetimeMinutes = lastsMinutes,
                MaxActiveFakes = max,
                FakeTypes = new List<FakeRandomReflectedEntitiesDetections.FakeEntity>
                {
                    Entity(EntityType.Vehicle, (100f, 100f)),
                },
            };
        }

        private static FakeRandomReflectedEntitiesDetections.FakeEntity Entity(EntityType type, params (float distance, float percent)[] bands) => Entity(type, 100f, bands);

        private static FakeRandomReflectedEntitiesDetections.FakeEntity Entity(EntityType type, float percent, params (float distance, float percent)[] bands)
        {
            var entity = new FakeRandomReflectedEntitiesDetections.FakeEntity { EntityType = type, PercentOfCreatedFakes = percent };
            foreach ((float distance, float bandPercent) in bands)
            {
                entity.Distances.Add(new DistancePercent { UpToMeters = distance, PercentOfThisType = bandPercent });
            }
            return entity;
        }

        private List<IDetectable> Refresh(DetectionInjector injector, float seconds)
        {
            injector.Prepare(new SensorContext(_sensor.transform, null, seconds));
            var output = new List<IDetectable>();
            injector.Collect(output);
            return output;
        }

        private static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [Test]
        public void FirstSpawn_WaitsTheInterval_ThenLastsItsTime()
        {
            var injector = Injector(everyMinutes: 1f, lastsMinutes: 0.5f);

            Assert.AreEqual(0, Refresh(injector, 0f).Count, "Nothing at the start: the first spawn waits too.");
            Assert.AreEqual(0, Refresh(injector, 59f).Count);

            List<IDetectable> spawned = Refresh(injector, 60f);
            Assert.AreEqual(1, spawned.Count);
            Assert.IsTrue(spawned[0].IsFake);
            Assert.AreEqual(EntityType.Vehicle, ((IEntityTyped)spawned[0]).EntityType);
            Assert.AreEqual("Vehicle", spawned[0].GetDetectionType());

            Assert.AreSame(spawned[0], Refresh(injector, 80f)[0], "The same fake is reported while it lasts.");
            Assert.AreEqual(0, Refresh(injector, 90f).Count, "Gone after 30 seconds.");
        }

        [Test]
        public void EachFake_HasItsOwnIdentifier()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 10f);
            Refresh(injector, 0f);
            List<IDetectable> fakes = Refresh(injector, 1f);

            Assert.AreEqual(2, fakes.Count);
            Assert.AreNotEqual(fakes[0].GetIdentifier(), fakes[1].GetIdentifier());
        }

        [Test]
        public void MaxActiveFakes_LimitsHowManyExistAtOnce()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 1f, max: 2);
            for (int second = 0; second < 10; second++)
            {
                Refresh(injector, second);
            }
            Assert.AreEqual(2, Refresh(injector, 10f).Count);

            // Once they expire, new ones can come: it is a limit at once, not in total.
            Assert.AreEqual(1, Refresh(injector, 70f).Count);
        }

        [Test]
        public void Unlimited_IgnoresTheLimit()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 10f, max: 1);
            injector.Unlimited = true;
            for (int second = 0; second < 5; second++)
            {
                Refresh(injector, second);
            }
            Assert.AreEqual(6, Refresh(injector, 5f).Count);
        }

        [Test]
        public void SpawnTimers_RunsThatManyTimers()
        {
            var injector = Injector(everyMinutes: 1f, lastsMinutes: 10f);
            injector.SpawnTimers = 3;

            Refresh(injector, 0f);
            Assert.AreEqual(3, Refresh(injector, 60f).Count);

            injector.SpawnTimers = 0;
            Assert.AreEqual(3, Refresh(injector, 120f).Count, "No timers, no new ones; the old ones stay until they expire.");
        }

        [Test]
        public void Distance_IsInsideThePickedBand_MeasuredFlat()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 10f, max: 1000);
            injector.FakeTypes[0] = Entity(EntityType.Person, (10f, 0f), (20f, 100f), (30f, 0f));

            List<IDetectable> fakes = null;
            for (int second = 0; second < 200; second++)
            {
                fakes = Refresh(injector, second);
            }

            Assert.AreEqual(200, fakes.Count);
            foreach (IDetectable fake in fakes)
            {
                float distance = FlatDistance(fake.GetCenterPosition(), SensorPosition);
                Assert.That(distance, Is.InRange(10f - 0.01f, 20f + 0.01f), "Only the 10–20 m band has weight.");
            }
        }

        [Test]
        public void Distance_SpreadsAcrossTheBand_AndAllDirections()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 10f, max: 1000);
            injector.FakeTypes[0] = Entity(EntityType.Person, (100f, 100f));

            List<IDetectable> fakes = null;
            for (int second = 0; second < 400; second++)
            {
                fakes = Refresh(injector, second);
            }

            int far = 0, east = 0, north = 0;
            foreach (IDetectable fake in fakes)
            {
                Vector3 offset = fake.GetCenterPosition() - SensorPosition;
                if (FlatDistance(fake.GetCenterPosition(), SensorPosition) > 50f) far++;
                if (offset.x > 0f) east++;
                if (offset.z > 0f) north++;
            }

            // A uniform 0–100 m pick puts about half past 50 m, and about half on each side.
            Assert.That(far, Is.InRange(150, 250));
            Assert.That(east, Is.InRange(150, 250));
            Assert.That(north, Is.InRange(150, 250));
        }

        [Test]
        public void EntityType_IsPickedByPercent()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 10f, max: 1000);
            injector.FakeTypes = new List<FakeRandomReflectedEntitiesDetections.FakeEntity>
            {
                Entity(EntityType.Drone, 0f, (100f, 100f)),
                Entity(EntityType.Person, 25f, (100f, 100f)),
                Entity(EntityType.Vehicle, 75f, (50f, 40f), (100f, 60f)),
            };
            Assert.IsNull(injector.Warning);

            List<IDetectable> fakes = null;
            for (int second = 0; second < 400; second++)
            {
                fakes = Refresh(injector, second);
            }

            int people = 0, vehicles = 0;
            foreach (IDetectable fake in fakes)
            {
                EntityType type = ((IEntityTyped)fake).EntityType;
                Assert.AreNotEqual(EntityType.Drone, type, "A type at 0% never comes.");
                if (type == EntityType.Person) people++;
                if (type == EntityType.Vehicle) vehicles++;
            }

            // 25%: about a quarter people.
            Assert.That(people, Is.InRange(60, 140));
            Assert.AreEqual(400, people + vehicles);
        }

        [Test]
        public void Percents_NotAddingUpTo100_AreWarned_AndUsedAsShares()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 10f, max: 1000);
            injector.FakeTypes = new List<FakeRandomReflectedEntitiesDetections.FakeEntity>
            {
                Entity(EntityType.Person, 30f, (100f, 100f)),
                Entity(EntityType.Vehicle, 30f, (50f, 40f), (100f, 50f)),
                Entity(EntityType.Drone, 20f),
                Entity(EntityType.Aircraft, 0f, (100f, 10f)), // 0% never comes, so its distances are not checked
            };

            string warning = injector.Warning;
            StringAssert.Contains("Percent Of Created Fakes adds up to 80% across the types, not 100%", warning);
            StringAssert.Contains("Vehicle: Percent Of This Type adds up to 90% across its distances, not 100%", warning);
            StringAssert.Contains("Drone has no distances", warning);
            StringAssert.DoesNotContain("Aircraft", warning);

            // Until fixed they still work, as shares: Person and Vehicle half each, the Drone has nowhere to go.
            List<IDetectable> fakes = null;
            for (int second = 0; second < 400; second++)
            {
                fakes = Refresh(injector, second);
            }
            int people = 0;
            foreach (IDetectable fake in fakes)
            {
                EntityType type = ((IEntityTyped)fake).EntityType;
                Assert.That(type, Is.EqualTo(EntityType.Person).Or.EqualTo(EntityType.Vehicle));
                if (type == EntityType.Person) people++;
            }
            Assert.That(people, Is.InRange(160, 240));

            // Fixed: no warning.
            injector.FakeTypes[1].Distances[1].PercentOfThisType = 60f;
            injector.FakeTypes[2].PercentOfCreatedFakes = 0f;
            injector.FakeTypes[0].PercentOfCreatedFakes = 70f;
            Assert.IsNull(injector.Warning);
        }

        [Test]
        public void Percents_SmallRoundingIsAccepted()
        {
            var injector = Injector(everyMinutes: 1f, lastsMinutes: 1f);
            injector.FakeTypes = new List<FakeRandomReflectedEntitiesDetections.FakeEntity>
            {
                Entity(EntityType.Person, 33.333f, (100f, 100f)),
                Entity(EntityType.Vehicle, 33.333f, (10f, 33.333f), (20f, 33.333f), (30f, 33.334f)),
                Entity(EntityType.Drone, 33.334f, (100f, 100f)),
            };
            Assert.IsNull(injector.Warning);
        }

        [Test]
        public void SpawnChance_DecidesWhetherAFakeIsCreated()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 10f, max: 1000);
            injector.SpawnChancePercent = 25f;

            List<IDetectable> fakes = null;
            for (int second = 0; second < 400; second++)
            {
                fakes = Refresh(injector, second);
            }

            // About a quarter of the 400 timer firings create a fake, and each one created has a type.
            Assert.That(fakes.Count, Is.InRange(60, 140));
            foreach (IDetectable fake in fakes)
            {
                Assert.AreEqual(EntityType.Vehicle, ((IEntityTyped)fake).EntityType);
            }
        }

        [Test]
        public void SpawnChance_ZeroCreatesNothing_HundredAlwaysCreates()
        {
            var never = Injector(everyMinutes: 0f, lastsMinutes: 10f, max: 1000);
            never.SpawnChancePercent = 0f;
            var always = Injector(everyMinutes: 0f, lastsMinutes: 10f, max: 1000);
            always.SpawnChancePercent = 100f;

            for (int second = 0; second < 50; second++)
            {
                Refresh(never, second);
                Refresh(always, second);
            }
            Assert.AreEqual(0, Refresh(never, 50f).Count);
            Assert.AreEqual(51, Refresh(always, 50f).Count);
        }

        [Test]
        public void SpawnChance_AFailedRoll_StillWaitsForTheNextTimer()
        {
            var injector = Injector(everyMinutes: 1f, lastsMinutes: 10f);
            injector.SpawnChancePercent = 0f;

            Refresh(injector, 0f);
            Assert.AreEqual(0, Refresh(injector, 60f).Count);

            // The timer was restarted at 60 s even though nothing came, so nothing fires again before 120 s.
            injector.SpawnChancePercent = 100f;
            Assert.AreEqual(0, Refresh(injector, 61f).Count);
            Assert.AreEqual(1, Refresh(injector, 120f).Count);
        }

        [Test]
        public void SpawnChance_IsInTheSummary()
        {
            var injector = Injector(everyMinutes: 1f, lastsMinutes: 1f);
            StringAssert.DoesNotContain("%", injector.Summary);

            injector.SpawnChancePercent = 30f;
            StringAssert.Contains("at 30%", injector.Summary);
        }

        [Test]
        public void NeverLost_KeepsTheFakes_UntilReset()
        {
            var injector = Injector(everyMinutes: 1f, lastsMinutes: 1f);
            injector.NeverLost = true;

            Refresh(injector, 0f);
            Assert.AreEqual(1, Refresh(injector, 60f).Count);
            Assert.AreEqual(1, Refresh(injector, 10000f).Count, "Still there long after its lifetime.");
            StringAssert.Contains("never lost", injector.Summary);

            injector.ResetState();
            Assert.AreEqual(0, Refresh(injector, 10001f).Count);
        }

        [Test]
        public void Lifetime_ChangedAtRuntime_AppliesToTheFakesAlreadyThere()
        {
            var injector = Injector(everyMinutes: 1f, lastsMinutes: 10f);
            injector.NeverLost = true;
            Refresh(injector, 0f);
            IDetectable first = Refresh(injector, 60f)[0]; // made at 60 s

            // Turning Never Lost off with a 1 minute lifetime: the fake made at 60 s is gone from 120 s.
            injector.NeverLost = false;
            injector.LifetimeMinutes = 1f;
            CollectionAssert.Contains(Refresh(injector, 100f), first);

            // The timer also fires at 120 s and makes a new one, so check that the first is the one gone.
            List<IDetectable> after = Refresh(injector, 121f);
            CollectionAssert.DoesNotContain(after, first);
            Assert.AreEqual(1, after.Count);
        }

        [Test]
        public void HiddenFields_FollowTheirToggle()
        {
            ShowIfAttribute OnField(string name) => (ShowIfAttribute)System.Attribute.GetCustomAttribute(
                typeof(FakeRandomReflectedEntitiesDetections).GetField(name), typeof(ShowIfAttribute));

            ShowIfAttribute max = OnField(nameof(FakeRandomReflectedEntitiesDetections.MaxActiveFakes));
            Assert.AreEqual(nameof(FakeRandomReflectedEntitiesDetections.Unlimited), max.BoolField);
            Assert.IsFalse(max.ShowWhen, "Shown only while Unlimited is off.");

            ShowIfAttribute lifetime = OnField(nameof(FakeRandomReflectedEntitiesDetections.LifetimeMinutes));
            Assert.AreEqual(nameof(FakeRandomReflectedEntitiesDetections.NeverLost), lifetime.BoolField);
            Assert.IsFalse(lifetime.ShowWhen, "Shown only while Never Lost is off.");

            Assert.IsFalse(new FakeRandomReflectedEntitiesDetections().NeverLost, "Off by default.");
        }

        [Test]
        public void NewInjector_HasAValidDefault()
        {
            var injector = new FakeRandomReflectedEntitiesDetections();
            Assert.IsNull(injector.Warning);
            Assert.AreEqual(1, injector.FakeTypes.Count);

            injector.FakeTypes.Clear();
            StringAssert.Contains("No fake types", injector.Warning);
        }

        [Test]
        public void NoUsableEntities_SpawnsNothing()
        {
            var injector = Injector(everyMinutes: 0f, lastsMinutes: 10f);
            injector.FakeTypes = new List<FakeRandomReflectedEntitiesDetections.FakeEntity>
            {
                Entity(EntityType.Drone),
                Entity(EntityType.Person, (100f, 0f)),
                null,
            };
            Assert.AreEqual(0, Refresh(injector, 0f).Count);

            injector.FakeTypes = null;
            Assert.AreEqual(0, Refresh(injector, 1f).Count);
        }

        [Test]
        public void ResetState_DropsTheFakes_AndRestartsTheTimers()
        {
            var injector = Injector(everyMinutes: 1f, lastsMinutes: 10f);
            Refresh(injector, 0f);
            Assert.AreEqual(1, Refresh(injector, 60f).Count);

            injector.ResetState();
            Assert.AreEqual(0, Refresh(injector, 61f).Count);
            Assert.AreEqual(0, Refresh(injector, 120f).Count, "The timer restarted at 61 s.");
            Assert.AreEqual(1, Refresh(injector, 121f).Count);
        }

        [Test]
        public void Viewport_WithoutACamera_FallsBackToAroundTheSensor()
        {
            var injector = new FakeRandomDetectionInViewPort
            {
                MinSpawnIntervalMinutes = 0f,
                MaxSpawnIntervalMinutes = 0f,
                FakeTypes = new List<FakeRandomReflectedEntitiesDetections.FakeEntity> { Entity(EntityType.Person, (20f, 100f)) },
            };

            List<IDetectable> fakes = Refresh(injector, 0f);
            Assert.AreEqual(1, fakes.Count);
            Assert.That(FlatDistance(fakes[0].GetCenterPosition(), SensorPosition), Is.LessThanOrEqualTo(20.01f));
        }

        [Test]
        public void Viewport_WithACamera_StaysInsideTheRectangle()
        {
            var cameraObject = new GameObject("camera", typeof(Camera));
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = SensorPosition;
                camera.farClipPlane = 1000f;

                var injector = new FakeRandomDetectionInViewPort
                {
                    MinSpawnIntervalMinutes = 0f,
                    MaxSpawnIntervalMinutes = 0f,
                    MaxActiveFakes = 1000,
                    LifetimeMinutes = 10f,
                    TopLeftCorner = new Vector2(0.6f, 0.9f),
                    BottomRightCorner = new Vector2(0.9f, 0.6f),
                    FakeTypes = new List<FakeRandomReflectedEntitiesDetections.FakeEntity> { Entity(EntityType.Person, (40f, 0f), (50f, 100f)) },
                };

                List<IDetectable> fakes = null;
                for (int second = 0; second < 50; second++)
                {
                    injector.Prepare(new SensorContext(_sensor.transform, camera, second));
                    fakes = new List<IDetectable>();
                    injector.Collect(fakes);
                }

                // Nothing to hit out here, so each fake is the viewport point at its depth, with the height kept.
                foreach (IDetectable fake in fakes)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(fake.GetCenterPosition());
                    Assert.That(viewport.x, Is.InRange(0.6f - 0.01f, 0.9f + 0.01f));
                    Assert.That(viewport.y, Is.InRange(0.6f - 0.01f, 0.9f + 0.01f));
                    Assert.That(viewport.z, Is.InRange(40f - 0.1f, 50f + 0.1f));
                }
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
