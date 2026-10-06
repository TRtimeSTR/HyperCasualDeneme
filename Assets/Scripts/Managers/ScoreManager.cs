using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace GateRunner.Managers
{
    /// <summary>
    /// Oyuncunun skorunu ve topladığı altın miktarını yöneten merkezi sınıf.
    /// UI metinlerini günceller ve her artışta tatmin edici DOTween punch-scale animasyonu oynatır.
    /// </summary>
    public class ScoreManager : SerializedMonoBehaviour
    {
        public static ScoreManager Instance { get; private set; }

        [FoldoutGroup("UI Metin Bileşenleri")]
        [Tooltip("Skor metnini gösteren TextMeshPro bileşeni.")]
        [SerializeField] private TMP_Text _scoreText;

        [FoldoutGroup("UI Metin Bileşenleri")]
        [Tooltip("Toplanan altın sayısını gösteren TextMeshPro bileşeni.")]
        [SerializeField] private TMP_Text _coinText;

        [FoldoutGroup("Animasyon Ayarları")]
        [Tooltip("Puan alındığında UI metninin büyüme (punch) şiddeti.")]
        [SerializeField, Range(0.1f, 1.0f)] private float _punchIntensity = 0.35f;

        [FoldoutGroup("Animasyon Ayarları")]
        [Tooltip("Punch animasyonunun süresi.")]
        [SerializeField, Range(0.1f, 0.5f)] private float _punchDuration = 0.22f;

        #region Live Stats (Odin)
        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Skor Bilgisi")]
        public int CurrentScore => _currentScore;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Skor Bilgisi")]
        public int CurrentCoins => _currentCoins;
        #endregion

        private int _currentScore = 0;
        private int _currentCoins = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            RefreshUI(punch: false);
        }

        /// <summary>
        /// Skora puan ekler ve UI'ı DOTween ile günceller.
        /// </summary>
        public void AddScore(int amount)
        {
            _currentScore += amount;
            UpdateScoreUI(punch: true);
        }

        /// <summary>
        /// Toplanan altın miktarını artırır ve UI'ı günceller.
        /// </summary>
        public void AddCoin(int amount)
        {
            _currentCoins += amount;
            _currentScore += amount * 10; // Her altın skora da katkı sağlar
            UpdateCoinUI(punch: true);
            UpdateScoreUI(punch: true);
        }

        private void UpdateScoreUI(bool punch)
        {
            if (_scoreText == null) return;

            _scoreText.text = $"{_currentScore}";

            if (punch)
            {
                _scoreText.transform.DOKill(complete: true);
                _scoreText.transform.localScale = Vector3.one;
                _scoreText.transform.DOPunchScale(Vector3.one * _punchIntensity, _punchDuration, 6, 0.5f);
            }
        }

        private void UpdateCoinUI(bool punch)
        {
            if (_coinText == null) return;

            _coinText.text = $"{_currentCoins}";

            if (punch)
            {
                _coinText.transform.DOKill(complete: true);
                _coinText.transform.localScale = Vector3.one;
                _coinText.transform.DOPunchScale(Vector3.one * (_punchIntensity * 1.2f), _punchDuration, 7, 0.5f);
            }
        }

        public void RefreshUI(bool punch = false)
        {
            UpdateScoreUI(punch);
            UpdateCoinUI(punch);
        }

        #region Odin Inspector Test Araçları
        [Button("+5 Altın Topla", ButtonSizes.Small), FoldoutGroup("Odin Test")]
        private void TestCollectCoin() => AddCoin(5);

        [Button("+100 Skor Ekle", ButtonSizes.Small), FoldoutGroup("Odin Test")]
        private void TestAddScore() => AddScore(100);

        [Button("Sıfırla (Reset)", ButtonSizes.Small), FoldoutGroup("Odin Test")]
        public void ResetScore()
        {
            _currentScore = 0;
            _currentCoins = 0;
            RefreshUI(punch: false);
        }
        #endregion

        private void OnDestroy()
        {
            if (_scoreText != null) _scoreText.transform.DOKill();
            if (_coinText != null) _coinText.transform.DOKill();
        }
    }
}
