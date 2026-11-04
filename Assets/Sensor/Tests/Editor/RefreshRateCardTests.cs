using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace SensorSystem.Tests
{
    public class RefreshRateCardTests
    {
        [TestCase(0.5f, "Checks 2 times per second")]
        [TestCase(1f, "Checks once per second")]
        [TestCase(2f, "Checks once every 2 seconds")]
        [TestCase(10f, "Checks once every 10 seconds")]
        public void Describe_UsesPlainWords(float seconds, string expected)
        {
            Assert.AreEqual(expected, RefreshRateCard.Describe(seconds));
        }

        [Test]
        public void Describe_NonPositiveRate_IsEmpty()
        {
            Assert.AreEqual(string.Empty, RefreshRateCard.Describe(0f));
        }

        [Test]
        public void Card_ShowsTheCurrentRateAndItsMeaning()
        {
            var settings = ScriptableObject.CreateInstance<SensorSettings>();
            try
            {
                var card = new RefreshRateCard(settings);

                Assert.AreEqual("Refresh rate", card.Q<Label>(className: "sensor-refresh-title").text);
                Assert.AreEqual("seconds", card.Q<Label>(className: "sensor-refresh-unit").text);
                Assert.AreEqual("Checks once per second", card.Q<Label>(className: "sensor-refresh-summary").text);
                Assert.IsNotNull(card.Q<FloatField>(className: "sensor-refresh-field"));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
