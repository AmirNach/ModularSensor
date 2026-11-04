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
                Assert.AreEqual(3, injector.LoopCount);
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
