using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>Periodically scans the scene for colliders on the layer mask that belong to an IDetectable.</summary>
    public class AllSimpleObjectsCollection : AbstractObjectCollection<AllSimpleObjectsCollection>
    {
        private const float RefreshRateInSec = 1f;

        private CancellationTokenSource _cancellation;

        public AllSimpleObjectsCollection(int layerMask) : base(layerMask) { }

        public override void SetEnable(bool isEnabled)
        {
            _cancellation?.Cancel();
            _cancellation?.Dispose();
            _cancellation = null;

            if (isEnabled)
            {
                // Also stops when the application quits or Play mode ends.
                _cancellation = CancellationTokenSource.CreateLinkedTokenSource(Application.exitCancellationToken);
                _ = UpdateAllObjectsInLayerAsync(_cancellation.Token);
            }
        }

        private async Task UpdateAllObjectsInLayerAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    Scan();
                    // Unity resumes this on the main thread.
                    await Task.Delay(TimeSpan.FromSeconds(RefreshRateInSec), token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void Scan()
        {
            var found = new HashSet<IDetectable>();
            Collider[] allColliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (Collider collider in allColliders)
            {
                if (IsLayerInLayerMask(LayerMask, collider.gameObject.layer))
                {
                    IDetectable detectable = collider.GetComponentInParent<IDetectable>();
                    if (detectable != null)
                    {
                        found.Add(detectable);
                    }
                }
            }

            AllObjects = found;
        }
    }
}
