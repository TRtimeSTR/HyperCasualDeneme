using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Audio
{
    /// <summary>
    /// Gate Runner oyununun ses efektlerini (SFX) ve geri bildirimlerini yöneten Singleton ses yöneticisi.
    /// Inspector'da atanmış ses klibi bulunmadığında Unity'nin ses API'sini kullanarak
    /// sıfır bağımlılıkla ve tam hypercasual tınısıyla prosedürel sesler sentezler (Procedural Audio Synth).
    /// </summary>
    [SelectionBase]
    public class AudioManager : SerializedMonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<AudioManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[AudioManager]");
                        _instance = go.AddComponent<AudioManager>();
                    }
                }
                return _instance;
            }
            private set => _instance = value;
        }

        [FoldoutGroup("Ses Efektleri (SFX Clips)")]
        [Tooltip("Altın toplandığında çalacak ses. Boşsa prosedürel sentezlenir.")]
        [SerializeField] private AudioClip _coinClip;

        [FoldoutGroup("Ses Efektleri (SFX Clips)")]
        [Tooltip("Diken/engele çarpıldığında çalacak hasar sesi. Boşsa prosedürel sentezlenir.")]
        [SerializeField] private AudioClip _damageClip;

        [FoldoutGroup("Ses Efektleri (SFX Clips)")]
        [Tooltip("Pozitif matematik kapısından geçildiğinde (+, x) çalacak ses. Boşsa prosedürel sentezlenir.")]
        [SerializeField] private AudioClip _gateBuffClip;

        [FoldoutGroup("Ses Efektleri (SFX Clips)")]
        [Tooltip("Negatif matematik kapısından geçildiğinde (-, /) çalacak ses. Boşsa prosedürel sentezlenir.")]
        [SerializeField] private AudioClip _gateDebuffClip;

        [FoldoutGroup("Ses Efektleri (SFX Clips)")]
        [Tooltip("Bitiş çizgisine ulaşıldığında zafer fanfare sesi. Boşsa prosedürel sentezlenir.")]
        [SerializeField] private AudioClip _victoryClip;

        [FoldoutGroup("Ses Efektleri (SFX Clips)")]
        [Tooltip("Karakter elendiğinde (Game Over) çalacak ses. Boşsa prosedürel sentezlenir.")]
        [SerializeField] private AudioClip _gameOverClip;

        [FoldoutGroup("Ses Seviyesi Ayarları")]
        [Range(0f, 1f)]
        [SerializeField] private float _masterVolume = 1.0f;

        [FoldoutGroup("Ses Seviyesi Ayarları")]
        [Range(0f, 1f)]
        [SerializeField] private float _sfxVolume = 0.9f;

        [FoldoutGroup("Ses Seviyesi Ayarları")]
        [Range(0f, 1f)]
        [SerializeField] private float _musicVolume = 0.8f;

        [FoldoutGroup("Audio Kaynakları")]
        [SerializeField] private AudioSource _sfxSource;

        [FoldoutGroup("Audio Kaynakları")]
        [SerializeField] private AudioSource _musicSource;

        // Prosedürel olarak bellekte oluşturulan önbelleklenmiş klipler
        private AudioClip _procCoinClip;
        private AudioClip _procDamageClip;
        private AudioClip _procGateBuffClip;
        private AudioClip _procGateDebuffClip;
        private AudioClip _procVictoryClip;
        private AudioClip _procGameOverClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureAudioSources();
            GenerateProceduralFallbacks();
        }

        private void EnsureAudioSources()
        {
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.spatialBlend = 0f; // 2D ses
            }

            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.playOnAwake = false;
                _musicSource.spatialBlend = 0f;
            }
        }

        /// <summary>
        /// Harici ses dosyası olmadığında çalışacak hafif hypercasual ses dalgalarını bellekte üretir.
        /// </summary>
        private void GenerateProceduralFallbacks()
        {
            if (_coinClip == null) _procCoinClip = SynthesizeArpeggio("Proc_Coin", new[] { 987f, 1318f }, 0.14f);
            if (_damageClip == null) _procDamageClip = SynthesizeNoiseThud("Proc_Damage", 140f, 50f, 0.22f);
            if (_gateBuffClip == null) _procGateBuffClip = SynthesizeArpeggio("Proc_GateBuff", new[] { 523f, 659f, 784f, 1046f }, 0.22f);
            if (_gateDebuffClip == null) _procGateDebuffClip = SynthesizeFrequencySlide("Proc_GateDebuff", 460f, 160f, 0.25f);
            if (_victoryClip == null) _procVictoryClip = SynthesizeFanfare("Proc_Victory", 0.9f);
            if (_gameOverClip == null) _procGameOverClip = SynthesizeArpeggio("Proc_GameOver", new[] { 392f, 349f, 311f, 261f }, 0.5f);
        }

        #region Public SFX API

        /// <summary>
        /// Karakter altın topladığında tatlı bir çınlama sesi çalar.
        /// </summary>
        public void PlayCoinSound()
        {
            AudioClip clip = _coinClip != null ? _coinClip : _procCoinClip;
            PlayOneShotWithPitch(clip, _sfxVolume * _masterVolume, 0.95f, 1.08f);
        }

        /// <summary>
        /// Karakter diken veya engele çarptığında tok bir hasar sesi çalar.
        /// </summary>
        public void PlayDamageSound()
        {
            AudioClip clip = _damageClip != null ? _damageClip : _procDamageClip;
            PlayOneShotWithPitch(clip, _sfxVolume * _masterVolume, 0.9f, 1.05f);
        }

        /// <summary>
        /// Karakter matematik kapısından geçtiğinde işlem türüne göre pozitif/negatif ses çalar.
        /// </summary>
        public void PlayGateSound(bool isPositive)
        {
            AudioClip clip;
            if (isPositive)
            {
                clip = _gateBuffClip != null ? _gateBuffClip : _procGateBuffClip;
            }
            else
            {
                clip = _gateDebuffClip != null ? _gateDebuffClip : _procGateDebuffClip;
            }

            PlayOneShotWithPitch(clip, _sfxVolume * _masterVolume, 0.98f, 1.02f);
        }

        /// <summary>
        /// Bitiş çizgisine ulaşıldığında zafer melodisi çalar.
        /// </summary>
        public void PlayVictorySound()
        {
            AudioClip clip = _victoryClip != null ? _victoryClip : _procVictoryClip;
            if (clip != null && _musicSource != null)
            {
                _musicSource.pitch = 1.0f;
                _musicSource.PlayOneShot(clip, _musicVolume * _masterVolume);
            }
        }

        /// <summary>
        /// Karakter elendiğinde yenilgi sesi çalar.
        /// </summary>
        public void PlayGameOverSound()
        {
            AudioClip clip = _gameOverClip != null ? _gameOverClip : _procGameOverClip;
            if (clip != null && _sfxSource != null)
            {
                _sfxSource.pitch = 1.0f;
                _sfxSource.PlayOneShot(clip, _sfxVolume * _masterVolume);
            }
        }

        private void PlayOneShotWithPitch(AudioClip clip, float volume, float minPitch, float maxPitch)
        {
            if (clip == null || _sfxSource == null) return;

            _sfxSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            _sfxSource.PlayOneShot(clip, volume);
        }

        #endregion

        #region Prosedürel Ses Sentezleyicisi (Pure C# Audio Synthesizer)

        private static AudioClip SynthesizeArpeggio(string name, float[] notes, float duration)
        {
            const int sampleRate = 44100;
            int totalSamples = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            int noteCount = notes.Length;
            int samplesPerNote = totalSamples / noteCount;

            for (int n = 0; n < noteCount; n++)
            {
                float freq = notes[n];
                int start = n * samplesPerNote;
                int end = (n == noteCount - 1) ? totalSamples : (n + 1) * samplesPerNote;

                for (int i = start; i < end; i++)
                {
                    float t = (float)i / sampleRate;
                    float noteT = (float)(i - start) / (end - start);
                    float envelope = Mathf.Exp(-3.5f * noteT); // Hızlı sönümlenme
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.7f +
                                 Mathf.Sin(4f * Mathf.PI * freq * t) * 0.2f; // Hafif harmonik
                    samples[i] = wave * envelope * 0.5f;
                }
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeFrequencySlide(string name, float startFreq, float endFreq, float duration)
        {
            const int sampleRate = 44100;
            int totalSamples = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float phase = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float progress = (float)i / totalSamples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, progress);
                phase += 2f * Mathf.PI * currentFreq / sampleRate;

                float envelope = (1f - progress);
                float wave = Mathf.Sin(phase) * 0.65f;
                samples[i] = wave * envelope * 0.45f;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeNoiseThud(string name, float startFreq, float endFreq, float duration)
        {
            const int sampleRate = 44100;
            int totalSamples = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float phase = 0f;
            for (int i = 0; i < totalSamples; i++)
            {
                float progress = (float)i / totalSamples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, progress);
                phase += 2f * Mathf.PI * currentFreq / sampleRate;

                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.25f;
                float envelope = Mathf.Exp(-6f * progress);
                float wave = (Mathf.Sin(phase) * 0.75f + noise) * envelope;
                samples[i] = Mathf.Clamp(wave * 0.6f, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip SynthesizeFanfare(string name, float duration)
        {
            const int sampleRate = 44100;
            int totalSamples = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float[] chordFreqs = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C Majör

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float progress = (float)i / totalSamples;
                float envelope = progress < 0.05f ? progress / 0.05f : Mathf.Exp(-1.5f * (progress - 0.05f));

                float wave = 0f;
                foreach (float f in chordFreqs)
                {
                    wave += Mathf.Sin(2f * Mathf.PI * f * t);
                }
                wave = (wave / chordFreqs.Length) * envelope * 0.5f;
                samples[i] = wave;
            }

            AudioClip clip = AudioClip.Create(name, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        #endregion

        #region Odin Test Buttons

        [Button("🔔 Test Altın Sesi (Coin)"), FoldoutGroup("Odin Test Butonları")]
        private void TestCoin() => PlayCoinSound();

        [Button("💥 Test Hasar Sesi (Damage)"), FoldoutGroup("Odin Test Butonları")]
        private void TestDamage() => PlayDamageSound();

        [Button("🟢 Test Kapı Buff Sesi (+ / x)"), FoldoutGroup("Odin Test Butonları")]
        private void TestGateBuff() => PlayGateSound(true);

        [Button("🔴 Test Kapı Debuff Sesi (- / /)"), FoldoutGroup("Odin Test Butonları")]
        private void TestGateDebuff() => PlayGateSound(false);

        [Button("🎉 Test Zafer Sesi (Victory)"), FoldoutGroup("Odin Test Butonları")]
        private void TestVictory() => PlayVictorySound();

        [Button("💀 Test Game Over Sesi"), FoldoutGroup("Odin Test Butonları")]
        private void TestGameOver() => PlayGameOverSound();

        #endregion
    }
}
