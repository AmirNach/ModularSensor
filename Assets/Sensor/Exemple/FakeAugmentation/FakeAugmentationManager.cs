using System.Collections;
using System.Collections.Generic;
using SensorSystem;
using UnityEngine;


public class FakeAugmentationManager : SingletonMonoBehaviour<FakeAugmentationManager>
{

    [SerializeField] private GameObject _fakeAugmentationPrefab;

    private Dictionary<int, FakeAugmentationPublisher> _activeAugmentations = new Dictionary<int, FakeAugmentationPublisher>();
    private int _idCounter = 0;

    public FakeAugmentationPublisher CreateAugmentation(Color color, Vector3 position, float destroyAfterSeconds = -1)
    {
        if (_fakeAugmentationPrefab == null)
        {
            Debug.LogError($"FakeAugmentationManager: Prefab was not assigned ");
            return null;
        }

        GameObject instance = Instantiate(_fakeAugmentationPrefab);
        FakeAugmentationPublisher publisher = instance.GetComponentInChildren<FakeAugmentationPublisher>();
        publisher.Position = position;
        int newId = _idCounter++;

        if (destroyAfterSeconds != -1)
        {
            StartCoroutine(DestroyFakeAugmentationAfter(destroyAfterSeconds, newId));
        }

        publisher.Initialize(newId, color);
        publisher.OnFakeAugmentationDestroyed += HandleAugmentationDestroyed;

        _activeAugmentations.Add(newId, publisher);
        return publisher;
    }

    private IEnumerator DestroyFakeAugmentationAfter(float seconds, int id)
    {
        yield return new WaitForSeconds(seconds);
        DestroyAugmentation(id);
    }

    public void DestroyAugmentation(int id)
    {
        if (_activeAugmentations.TryGetValue(id, out FakeAugmentationPublisher publisher))
        {
            Destroy(publisher.gameObject);
        }
    }

    private void HandleAugmentationDestroyed(int id)
    {
        if (_activeAugmentations.ContainsKey(id))
        {
            _activeAugmentations.Remove(id);
        }
    }
}

