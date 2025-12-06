using UnityEngine;

namespace hp55games.FlappyTsunami.Configs
{
    [CreateAssetMenu(
        fileName = "FollowerConfig",
        menuName = "FlappyTsunami/Follower Config")]
    public class FollowerConfig : ScriptableObject
    {
        [Header("Visual")]
        public Sprite sprite;
        public Color color = Color.white;
        public Vector3 localScale = Vector3.one;

        [Header("Movement")]
        public float gravityScale = 1f;
        public float verticalImpulseMultiplier = 1f;
        public float tapDelay = 0f;

        // TODO: aggiungere qui in futuro roba tipo extraHP, trail, VFX
    }
}