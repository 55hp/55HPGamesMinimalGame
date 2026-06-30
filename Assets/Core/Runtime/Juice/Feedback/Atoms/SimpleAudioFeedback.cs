using System.Collections;
using UnityEngine;

namespace hp55games.Mobile.Core.Juice
{
    /// <summary>
    /// Plays an AudioClip one-shot on an AudioSource resolved at Awake.
    /// Falls back to AddComponent if no AudioSource is found on this GameObject.
    /// If _pitchVariation is greater than zero, pitch is randomized in the range
    /// [1 - variation, 1 + variation] on every Activate call.
    /// </summary>
    public sealed class SimpleAudioFeedback : MonoBehaviour, IFeedback
    {
        [Header("Timing")]
        [Tooltip("Seconds to wait before the clip plays.")]
        [SerializeField] private float _startDelay = 0f;

        [Header("Audio")]
        [SerializeField] private AudioClip _clip;
        [SerializeField] [Range(0f, 1f)] private float _volume         = 1f;
        [SerializeField] [Range(0f, 1f)] private float _pitchVariation = 0f;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null)
                _source = gameObject.AddComponent<AudioSource>();

            // Prevent the AudioSource from playing automatically or looping.
            _source.playOnAwake = false;
            _source.loop        = false;
        }

        public void Activate(Transform origin = null)
        {
            if (_clip == null)
            {
                Debug.LogWarning("[SimpleAudioFeedback] _clip is null — skipping.", this);
                return;
            }

            if (_startDelay > 0f)
                StartCoroutine(DelayedPlay());
            else
                Play();
        }

        private IEnumerator DelayedPlay()
        {
            yield return new WaitForSeconds(_startDelay);
            Play();
        }

        private void Play()
        {
            if (_pitchVariation > 0f)
                _source.pitch = Random.Range(1f - _pitchVariation, 1f + _pitchVariation);
            else
                _source.pitch = 1f;

            _source.PlayOneShot(_clip, _volume);
        }
    }
}
