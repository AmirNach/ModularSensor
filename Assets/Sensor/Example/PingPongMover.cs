using UnityEngine;

/// <summary>Moves an object back and forth, so the example shows targets being gained and lost.</summary>
public class PingPongMover : MonoBehaviour
{
    public Vector3 Offset = new Vector3(20f, 0f, 0f);
    public float Speed = 0.2f;

    private Vector3 _start;

    private void Start()
    {
        _start = transform.position;
    }

    private void Update()
    {
        transform.position = _start + Offset * Mathf.PingPong(Time.time * Speed, 1f);
    }
}
