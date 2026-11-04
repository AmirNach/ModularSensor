using UnityEngine;

namespace SensorSystem
{
    public class IdSettings : ScriptableObject
    {
        [Tooltip("The first ID given to a detection.")]
        [SerializeField, Min(0)] private int _firstDetectionId = 1;
        [Tooltip("The last ID given to a detection. IDs are reused after a detection is lost.")]
        [SerializeField, Min(1)] private int _lastDetectionId = 99999;
        [Tooltip("Hand out IDs in random order instead of counting up.")]
        [SerializeField] private bool _shuffledNumbers;

        public int MinDetectionId => _firstDetectionId;
        public int MaxDetectionId => _lastDetectionId;
        public bool ShuffleIdNumbers => _shuffledNumbers;
    }
}
