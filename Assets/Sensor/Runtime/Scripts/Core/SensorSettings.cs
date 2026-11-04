using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace SensorSystem
{
    [CreateAssetMenu(fileName = "SensorSettings", menuName = "Sensor/Sensor Settings")]
    public class SensorSettings : IdSettings
    {
        public const float MinRefreshRate = 0.5f;

        [Tooltip("Seconds between evaluations of the condition tree.")]
        [SerializeField, Min(MinRefreshRate)] private float _refreshRate = 1f;

        [SerializeReference] public Condition Root;

        [FormerlySerializedAs("Sources")]
        [SerializeReference] public List<DetectionInjector> Injectors = new List<DetectionInjector>();

        public float RefreshRate => Mathf.Max(MinRefreshRate, _refreshRate);

        private void OnValidate()
        {
            _refreshRate = Mathf.Max(MinRefreshRate, _refreshRate);
        }
    }
}
