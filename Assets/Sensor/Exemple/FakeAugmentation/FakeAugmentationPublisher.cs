using System;
using UnityEngine;

public class FakeAugmentationPublisher : MonoBehaviour
{
    public Action<int> OnFakeAugmentationDestroyed;

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            if (_spriteRenderer != null) _spriteRenderer.color = value;
        }
    }

    public Vector3 Position
    {
        get => _position;
        set
        {
            _position = value;
            transform.position = _position;
        }
    }


    public bool Visible
    {
        get => _visible;
        set
        {
            _visible = value;
            if (_spriteRenderer != null) _spriteRenderer.enabled = value;
        }
    }

    public int Id => _id;

    private SpriteRenderer _spriteRenderer;
    private int _id;
    private Color _color;
    private bool _visible;
    private Vector3 _position;

    public void Initialize(int id, Color color)
    {
        _id = id;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        Color = color;
        Visible = true;
    }

    private void OnDestroy()
    {
        OnFakeAugmentationDestroyed?.Invoke(_id);
    }
}

