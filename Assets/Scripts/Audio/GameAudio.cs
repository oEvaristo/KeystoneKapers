using UnityEngine;

namespace KeystoneKapers
{
    /// <summary>
    /// Efeitos sonoros do jogo (2D): passos em loop enquanto o policial corre no chão,
    /// som de pulo e som ao pegar um item. Os clipes ficam em Assets/Audios.
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        public static GameAudio Instance { get; private set; }

        [Header("Clipes")]
        [Tooltip("Loop tocado enquanto o policial corre (andando.mp3).")]
        public AudioClip runLoop;
        [Tooltip("Tocado quando o policial pula (pulo.mp3).")]
        public AudioClip jump;
        [Tooltip("Tocado ao pegar qualquer item (getItens.mp3).")]
        public AudioClip itemPickup;

        [Header("Volumes")]
        [Range(0f, 1f)] public float runVolume = 0.6f;
        [Range(0f, 1f)] public float jumpVolume = 0.8f;
        [Range(0f, 1f)] public float itemVolume = 0.9f;

        AudioSource loopSource, sfxSource;

        void Awake()
        {
            Instance = this;
            loopSource = CreateSource(true);
            sfxSource = CreateSource(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        AudioSource CreateSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f; // som 2D, independente da posição da câmera
            return s;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            bool running = gm && gm.State == GameState.Playing && gm.police && gm.police.IsRunning;
            if (running && runLoop)
            {
                if (!loopSource.isPlaying)
                {
                    loopSource.clip = runLoop;
                    loopSource.volume = runVolume;
                    loopSource.Play();
                }
            }
            else if (loopSource.isPlaying)
            {
                loopSource.Stop();
            }
        }

        public static void PlayJump()
        {
            if (Instance && Instance.jump) Instance.sfxSource.PlayOneShot(Instance.jump, Instance.jumpVolume);
        }

        public static void PlayItemPickup()
        {
            if (Instance && Instance.itemPickup) Instance.sfxSource.PlayOneShot(Instance.itemPickup, Instance.itemVolume);
        }
    }
}
