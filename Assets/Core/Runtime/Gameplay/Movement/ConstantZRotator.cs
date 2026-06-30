using UnityEngine;

namespace hp55games.Mobile.Core.Gameplay
{
    /// <summary>
    /// Rotates the GameObject continuously around the Z axis at a constant speed.
    /// Positive speed = counter-clockwise, negative speed = clockwise (Unity convention).
    /// </summary>
    public sealed class ConstantZRotator : MonoBehaviour
    {
        [SerializeField] private float _degreesPerSecond = 90f;
        [SerializeField] private Space _space = Space.Self;

        private void Update()
        {
            transform.Rotate(0f, 0f, _degreesPerSecond * Time.deltaTime, _space);
        }

        public void SetSpeed(float degreesPerSecond) => _degreesPerSecond = degreesPerSecond;
    }
}
