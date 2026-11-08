using System;
using System.Collections.Generic;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Drop this on an object with a collider to make it detectable. It is kept in the DetectableRegistry while it is enabled.</summary>
    [DisallowMultipleComponent]
    public class DetectableEntity : MonoBehaviour, IDetectable, IEntityTyped, IDetectableLayer, IDetectableCollider
    {
        // One string per entity type for everyone, so reading the type never creates garbage.
        private static readonly Dictionary<EntityType, string> TypeNames = new Dictionary<EntityType, string>();

        [SerializeField] private EntityType _entityType = EntityType.Unknown;
        [Tooltip("Size used for the bounds when the object has no collider.")]
        [SerializeField] private Vector3 _fallbackBoundsSize = Vector3.one;

        private Collider _collider;
        private int _colliderId;
        private Bounds _bounds;
        private Bounds _colliderBounds;
        private int _colliderBoundsFrame = -1;
        private Vector3 _lastPosition;
        private float _velocity;
        private string _identifier;

        public EntityType EntityType => _entityType;
        public bool IsFake => false;

        // Called for every object on every refresh, so it skips the interface's default version.
        public bool IsValid => this != null;

        /// <summary>The layer tested against a sensor's layer mask: the collider's, or this object's when it has none.</summary>
        public int DetectionLayer => _collider != null ? _collider.gameObject.layer : gameObject.layer;

        /// <summary>The collider found in Awake. It is on this object or a child, so a ray that hits it has reached this target.</summary>
        public int ColliderInstanceId => _colliderId;

        public Bounds Bounds
        {
            get => _collider != null ? GetColliderBounds() : _bounds;
            set => _bounds = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            TypeNames.Clear();
        }

        protected virtual void Awake()
        {
            _collider = GetComponentInChildren<Collider>();
            _colliderId = _collider != null ? _collider.GetInstanceID() : 0;
            _bounds = new Bounds(transform.position, _fallbackBoundsSize);
            _lastPosition = transform.position;
            _identifier = Guid.NewGuid().ToString();
        }

        protected virtual void OnEnable()
        {
            DetectableRegistry.Register(this);
        }

        protected virtual void OnDisable()
        {
            DetectableRegistry.Unregister(this);
        }

        protected virtual void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime > 0f)
            {
                Vector3 position = transform.position;
                _velocity = (position - _lastPosition).magnitude / deltaTime;
                _lastPosition = position;
            }
        }

        public string GetIdentifier() => _identifier;
        public Vector3 GetCenterPosition() => _collider != null ? GetColliderBounds().center : transform.position;
        public float GetVelocity() => _velocity;

        public string GetDetectionType()
        {
            if (!TypeNames.TryGetValue(_entityType, out string name))
            {
                name = _entityType.ToString();
                TypeNames[_entityType] = name;
            }
            return name;
        }

        /// <summary>
        /// Reading a collider's bounds goes into the engine, and one refresh asks for them many times (the conditions, the distance,
        /// the snapshot). They are read once per frame instead.
        /// </summary>
        private Bounds GetColliderBounds()
        {
            int frame = Time.frameCount;
            if (_colliderBoundsFrame != frame)
            {
                _colliderBounds = _collider.bounds;
                _colliderBoundsFrame = frame;
            }
            return _colliderBounds;
        }
    }
}
