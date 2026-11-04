using UnityEngine;

namespace SensorSystem
{
    internal static class SingletonLifetime
    {
        public static bool IsQuitting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            IsQuitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting()
        {
            IsQuitting = true;
        }
    }

    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;

        /// <summary>The scene's instance, created on demand. Null while the application quits.</summary>
        public static T Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (SingletonLifetime.IsQuitting) return null;

                _instance = FindFirstObjectByType<T>();
                if (_instance == null)
                {
                    _instance = new GameObject(typeof(T).Name).AddComponent<T>();
                }
                return _instance;
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                _instance = this as T;
            }
            else if (_instance != this)
            {
                Destroy(this);
            }
        }
    }
}
