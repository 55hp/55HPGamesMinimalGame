using UnityEngine;

namespace hp55games.Mobile.Core.Gameplay
{
    /// <summary>
    /// Simple constant mover, ideal for pipes, bullets, obstacles, etc.
    /// Supports full 3D velocity via Vector3. Default (-5, 0, 0) preserves original 2D behavior.
    /// </summary>
    public sealed class ConstantMover : MonoBehaviour
    {
        [SerializeField] private Vector3 _speed = new Vector3(-5f, 0f, 0f);
        [SerializeField] private Space _space = Space.World;

        private void Update()
        {
            var delta = _speed * Time.deltaTime;
            transform.Translate(delta, _space);
        }

        public void SetSpeed(Vector3 speed) => _speed = speed;

        // Convenience overload to keep compatibility with callers that pass a Vector2.
        public void SetSpeed(Vector2 speed) => _speed = new Vector3(speed.x, speed.y, 0f);
    }
}
