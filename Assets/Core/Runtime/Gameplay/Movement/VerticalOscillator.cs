using UnityEngine;

public class VerticalOscillator : MonoBehaviour
{
    public float amplitude = 1.5f;
    public float speed = 0.8f;

    private Vector3 startPos;

    private void Awake() => startPos = transform.localPosition;

    private void Update()
    {
        transform.localPosition = startPos + Vector3.up * Mathf.Sin(Time.time * speed) * amplitude;
    }
}