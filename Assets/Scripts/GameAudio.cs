using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    /// <summary>
    /// Plays one-shot sounds from a small pool, with pitch variation and a per-clip cooldown
    /// so ten towers firing at once don't stack into noise. Also keeps a zombie horde murmuring.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        [Header("UI")]
        public AudioClip click;
        public AudioClip error;
        public AudioClip build;
        public AudioClip waveStart;
        public AudioClip victory;
        public AudioClip defeat;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 0.8f;
        [SerializeField] private float sameClipCooldown = 0.06f;
        [SerializeField] private Vector2 groanInterval = new Vector2(1.2f, 3f);

        private const int Voices = 16;
        private readonly AudioSource[] sources = new AudioSource[Voices];
        private readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();
        private int next;
        private float groanTimer = 2f;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < Voices; i++)
            {
                sources[i] = gameObject.AddComponent<AudioSource>();
                sources[i].playOnAwake = false;
                sources[i].spatialBlend = 0f;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Plays a clip if one is set and it hasn't just played.</summary>
        public static void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.08f)
        {
            if (Instance != null) Instance.PlayClip(clip, volume, pitchJitter);
        }

        /// <summary>Plays a random clip from a set.</summary>
        public static void PlayAny(AudioClip[] clips, float volume = 1f, float pitchJitter = 0.1f)
        {
            if (clips == null || clips.Length == 0) return;
            Play(clips[Random.Range(0, clips.Length)], volume, pitchJitter);
        }

        private void PlayClip(AudioClip clip, float volume, float pitchJitter)
        {
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(clip, out float last) && now - last < sameClipCooldown) return;
            lastPlayed[clip] = now;

            AudioSource source = sources[next];
            next = (next + 1) % Voices;
            source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            source.PlayOneShot(clip, volume * masterVolume);
        }

        private void Update()
        {
            if (Enemy.Active.Count == 0 || (GameManager.Instance != null && GameManager.Instance.IsFinished)) return;

            // A random zombie on the road groans every so often; more zombies, more often.
            groanTimer -= Time.deltaTime;
            if (groanTimer > 0f) return;
            float crowd = Mathf.Clamp01(Enemy.Active.Count / 12f);
            groanTimer = Mathf.Lerp(groanInterval.y, groanInterval.x, crowd) * Random.Range(0.7f, 1.3f);
            Enemy enemy = Enemy.Active[Random.Range(0, Enemy.Active.Count)];
            PlayAny(enemy.Stats.voice, 0.45f, 0.12f);
        }
    }
}
