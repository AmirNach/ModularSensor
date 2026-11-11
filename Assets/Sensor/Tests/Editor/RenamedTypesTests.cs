using NUnit.Framework;
using UnityEngine;

namespace SensorSystem.Tests
{
    public class RenamedTypesTests
    {
        [Test]
        public void InjectorSavedUnderTheOldMisspelledName_StillLoads()
        {
            const string saved = @"{
                ""Injectors"": [ { ""rid"": 101 } ],
                ""references"": {
                    ""version"": 2,
                    ""RefIds"": [
                        { ""rid"": 101,
                          ""type"": { ""class"": ""FakeRandomReflecedEntitiesDetections"", ""ns"": ""SensorSystem"", ""asm"": ""Sensor.Runtime"" },
                          ""data"": { ""LoopCount"": 3 } }
                    ]
                }
            }";

            var settings = ScriptableObject.CreateInstance<SensorSettings>();
            try
            {
                JsonUtility.FromJsonOverwrite(saved, settings);

                Assert.AreEqual(1, settings.Injectors.Count);
                var injector = settings.Injectors[0] as FakeRandomReflectedEntitiesDetections;
                Assert.IsNotNull(injector, "The old class name should map to the renamed class.");
                Assert.AreEqual(3, injector.SpawnTimers);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void InjectorSavedWithTheOldFieldNames_StillLoads()
        {
            const string saved = @"{
                ""Injectors"": [ { ""rid"": 101 } ],
                ""references"": {
                    ""version"": 2,
                    ""RefIds"": [
                        { ""rid"": 101,
                          ""type"": { ""class"": ""FakeRandomReflectedEntitiesDetections"", ""ns"": ""SensorSystem"", ""asm"": ""Sensor.Runtime"" },
                          ""data"": {
                              ""EntityTypeToDistanceWeights"": [ { ""EntityType"": 2, ""DistanceWeights"": [ { ""Distance"": 30.0, ""Weight"": 1.0 } ] } ],
                              ""MinTimeMinutes"": 3.0, ""MaxTimeMinutes"": 4.0, ""LoopCount"": 2,
                              ""UseInfiniteDetections"": 1, ""MaxTotalDetections"": 7, ""ExpirationTimeMinutes"": 5.0 } }
                    ]
                }
            }";

            var settings = ScriptableObject.CreateInstance<SensorSettings>();
            try
            {
                JsonUtility.FromJsonOverwrite(saved, settings);
                var injector = (FakeRandomReflectedEntitiesDetections)settings.Injectors[0];

                Assert.AreEqual(1, injector.FakeTypes.Count);
                Assert.AreEqual(EntityType.Vehicle, injector.FakeTypes[0].EntityType);
                Assert.AreEqual(30f, injector.FakeTypes[0].Distances[0].UpToMeters);
                Assert.AreEqual(1f, injector.FakeTypes[0].Distances[0].PercentOfThisType, "An old weight loads as a percent; the card then warns that it is not 100%.");
                Assert.AreEqual(3f, injector.MinSpawnIntervalMinutes);
                Assert.AreEqual(4f, injector.MaxSpawnIntervalMinutes);
                Assert.AreEqual(2, injector.SpawnTimers);
                Assert.IsTrue(injector.Unlimited);
                Assert.AreEqual(7, injector.MaxActiveFakes);
                Assert.AreEqual(5f, injector.LifetimeMinutes);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void DetectableMembers_UseTheCorrectedSpelling()
        {
            var fake = new FakeDetectionInstance(Vector3.zero, EntityType.Drone);
            IDetectable detectable = fake;

            Assert.IsNotEmpty(detectable.GetIdentifier());
        }
    }
}
