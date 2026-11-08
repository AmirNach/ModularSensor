using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SensorSystem
{
    /// <summary>
    /// Once a second, updates the list of objects a sensor may detect: the ones in the DetectableRegistry that are on the layer mask.
    /// The two sets are swapped instead of rebuilt, so a scan does not create garbage. With
    /// DetectableRegistry.IncludeUnregisteredSceneObjects on, it also searches every collider for IDetectable components that never registered.
    /// </summary>
    public class AllSimpleObjectsCollection : AbstractObjectCollection<AllSimpleObjectsCollection>
    {
        private const float RefreshRateInSec = 1f;

        private CancellationTokenSource _cancellation;
        private HashSet<IDetectable> _spare = new HashSet<IDetectable>();

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
                    using (SensorProfiler.Scan.Measure()) Scan();
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
            // Fill the spare set, then swap it in, so a sensor reading AllObjects never sees a half-built list.
            _spare.Clear();
            DetectableRegistry.CollectInto(_spare, LayerMask);
            if (DetectableRegistry.IncludeUnregisteredSceneObjects)
            {
                AddUnregisteredFromScene(_spare);
            }

            HashSet<IDetectable> previous = AllObjects;
            AllObjects = _spare;
            _spare = previous;
        }

        private void AddUnregisteredFromScene(HashSet<IDetectable> found)
        {
            Collider[] allColliders;
            using (SensorProfiler.ScanFindObjects.Measure())
            {
                allColliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            }

            using (SensorProfiler.ScanFilter.Measure())
            {
                foreach (Collider collider in allColliders)
                {
                    if (!IsLayerInLayerMask(LayerMask, collider.gameObject.layer)) continue;

                    IDetectable detectable = collider.GetComponentInParent<IDetectable>();
                    if (detectable != null)
                    {
                        found.Add(detectable);
                    }
                }
            }
        }
    }
}
