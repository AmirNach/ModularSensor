using System;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Drop this on an object with a collider to make it detectable.</summary>
    [DisallowMultipleComponent]
    public class DetectableEntity : MonoBehaviour, IDetectable, IEntityTyped
    {
        [SerializeField] private EntityType _entityType = EntityType.Unknown;
        [Tooltip("Size used for the bounds when the object has no collider.")]
        [SerializeField] private Vector3 _fallbackBoundsSize = Vector3.one;

        private Collider _collider;
        private Bounds _bounds;
        private Vector3 _lastPosition;
        private float _velocity;
        private string _identifier;

        public EntityType EntityType => _entityType;
        public bool IsFake => false;

        public Bounds Bounds
        {
            get => _collider != null ? _collider.bounds : _bounds;
            set => _bounds = value;
        }

        protected virtual void Awake()
        {
            _collider = GetComponentInChildren<Collider>();
            _bounds = new Bounds(transform.position, _fallbackBoundsSize);
            _lastPosition = transform.position;
            _identifier = Guid.NewGuid().ToString();
        }

        protected virtual void Update()
        {
            if (Time.deltaTime > 0f)
            {
                _velocity = (transform.position - _lastPosition).magnitude / Time.deltaTime;
                _lastPosition = transform.position;
            }
        }

        public string GetIdentifier() => _identifier;
        public Vector3 GetCenterPosition() => _collider != null ? _collider.bounds.center : transform.position;
        public string GetDetectionType() => _entityType.ToString();
        public float GetVelocity() => _velocity;
    }
}
