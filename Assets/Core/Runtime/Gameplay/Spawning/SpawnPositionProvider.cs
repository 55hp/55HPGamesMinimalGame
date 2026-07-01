using UnityEngine;

namespace hp55games.Mobile.Core.Gameplay.Spawning
{
    public sealed class SpawnPositionProvider : MonoBehaviour
    {
        [Header("Spawn Point")]
        [SerializeField]
        [Tooltip("Transform defining the spawn position (typically at right edge of screen).")]
        private Transform _spawnPoint;

        [Header("Lane Configuration")]
        [SerializeField]
        [Tooltip("Y positions for each lane (bottom to top).")]
        private float[] _laneYPositions = new[] { -2f, 0f, 2f };

        public Vector2 GetSpawnPosition(int lane = -1)
        {
            if (_spawnPoint == null)
                return Vector2.zero;

            var x = _spawnPoint.position.x;
            var y = GetLaneY(lane);

            return new Vector2(x, y);
        }

        public float GetSpawnX()
        {
            return _spawnPoint != null ? _spawnPoint.position.x : 0f;
        }

        public float GetLaneY(int lane)
        {
            if (lane < 0 || lane >= _laneYPositions.Length)
                return 0f;

            return _laneYPositions[lane];
        }

        public int GetLaneCount()
        {
            return _laneYPositions.Length;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_spawnPoint == null)
                return;

            Gizmos.color = Color.cyan;
            var x = _spawnPoint.position.x;

            for (int i = 0; i < _laneYPositions.Length; i++)
            {
                var pos = new Vector3(x, _laneYPositions[i], 0f);
                Gizmos.DrawWireSphere(pos, 0.2f);
                UnityEditor.Handles.Label(pos + Vector3.up * 0.5f, $"Lane {i}");
            }
        }
#endif
    }
}
