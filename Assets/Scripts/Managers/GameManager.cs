using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GateRunner.Managers
{
    public enum GameState
    {
        [InspectorName("Hazır (Ready)")]
        Ready,

        [InspectorName("Oynanıyor (Running)")]
        Running,

        [InspectorName("Bölüm Başarılı (Victory)")]
        Victory,

        [InspectorName("Bölüm Başarısız (GameOver)")]
        GameOver
    }

    /// <summary>
    /// Hypercasual Gate Runner oyun döngüsünü (Ready, Running, Victory, GameOver),
    /// seviye ilerlemesini, UI panellerini ve GameAnalytics / AppLovin MAX analitik & reklam
    /// SDK köprülerini yöneten merkezi oyun yöneticisi.
    /// </summary>
    public class GameManager : SerializedMonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        private const string PREFS_LEVEL_KEY = "GateRunner_CurrentLevel";

        [TabGroup("GameTabs", "Oyun Durumu", SdfIconType.PlayFill)]
        [ShowInInspector, ReadOnly]
        public GameState State => _currentState;

        [TabGroup("GameTabs", "Oyun Durumu")]
        [ShowInInspector, ReadOnly]
        public int CurrentLevel => _currentLevel;

        [TabGroup("GameTabs", "UI Panelleri", SdfIconType.Display)]
        [Tooltip("Bölüm başarıyla bittiğinde açılacak zafer paneli.")]
        [SerializeField] private GameObject _victoryPanel;

        [TabGroup("GameTabs", "UI Panelleri")]
        [Tooltip("Karakter elendiğinde açılacak yenilgi paneli.")]
        [SerializeField] private GameObject _gameOverPanel;

        [TabGroup("GameTabs", "UI Panelleri")]
        [Tooltip("Seviye numarasını gösteren UI metni.")]
        [SerializeField] private TMP_Text _levelText;

        [TabGroup("GameTabs", "UI Panelleri")]
        [Tooltip("Zafer panelindeki sonraki bölüm butonu.")]
        [SerializeField] private Button _nextLevelButton;

        [TabGroup("GameTabs", "UI Panelleri")]
        [Tooltip("Yenilgi panelindeki tekrar dene butonu.")]
        [SerializeField] private Button _retryButton;

        #region Events
        public event Action<GameState> OnGameStateChanged;
        public event Action<int> OnLevelChanged;
        #endregion

        private GameState _currentState = GameState.Ready;
        private int _currentLevel = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _currentLevel = PlayerPrefs.GetInt(PREFS_LEVEL_KEY, 1);
        }

        private void Start()
        {
            SetupUIButtons();
            UpdateLevelDisplay();
            StartGame();
        }

        private void SetupUIButtons()
        {
            if (_nextLevelButton != null)
            {
                _nextLevelButton.onClick.RemoveAllListeners();
                _nextLevelButton.onClick.AddListener(NextLevel);
            }

            if (_retryButton != null)
            {
                _retryButton.onClick.RemoveAllListeners();
                _retryButton.onClick.AddListener(RestartLevel);
            }

            if (_victoryPanel != null) _victoryPanel.SetActive(false);
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
        }

        private void UpdateLevelDisplay()
        {
            if (_levelText != null)
            {
                _levelText.text = $"LEVEL {_currentLevel}";
            }
            OnLevelChanged?.Invoke(_currentLevel);
        }

        /// <summary>
        /// Seviyeyi başlatır ve analiz SDK'larına level_start olayını iletir.
        /// </summary>
        public void StartGame()
        {
            _currentState = GameState.Running;
            OnGameStateChanged?.Invoke(_currentState);

            LogAnalyticsEvent("level_start", new Dictionary<string, object>
            {
                { "level_index", _currentLevel }
            });

            Debug.Log($"<color=#29B6F6><b>[GameManager]</b></color> Seviye Başladı: Level {_currentLevel}");
        }

        /// <summary>
        /// Oyuncu bitiş çizgisine ulaştığında zafer durumunu tetikler.
        /// </summary>
        public void LevelComplete()
        {
            if (_currentState != GameState.Running) return;

            _currentState = GameState.Victory;
            OnGameStateChanged?.Invoke(_currentState);

            int finalScore = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentScore : 0;
            int coinsEarned = ScoreManager.Instance != null ? ScoreManager.Instance.CurrentCoins : 0;

            LogAnalyticsEvent("level_complete", new Dictionary<string, object>
            {
                { "level_index", _currentLevel },
                { "score", finalScore },
                { "coins", coinsEarned }
            });

            Debug.Log($"<color=#66BB6A><b>[GameManager]</b></color> Bölüm Tamamlandı! Skor: {finalScore} | Altın: {coinsEarned}");

            // Zafer Panelini DOTween ile aç
            if (_victoryPanel != null)
            {
                _victoryPanel.SetActive(true);
                _victoryPanel.transform.localScale = Vector3.zero;
                _victoryPanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }

            // Geçiş reklamı (Interstitial Ad) çağrısı
            ShowInterstitialAd("level_complete");
        }

        /// <summary>
        /// Oyuncu öldüğünde yenilgi durumunu tetikler.
        /// </summary>
        public void LevelFail()
        {
            if (_currentState != GameState.Running) return;

            _currentState = GameState.GameOver;
            OnGameStateChanged?.Invoke(_currentState);

            LogAnalyticsEvent("level_fail", new Dictionary<string, object>
            {
                { "level_index", _currentLevel },
                { "reason", "obstacle_death" }
            });

            Debug.Log($"<color=#EF5350><b>[GameManager]</b></color> Bölüm Başarısız! Level {_currentLevel}");

            // Yenilgi Panelini DOTween ile aç
            if (_gameOverPanel != null)
            {
                _gameOverPanel.SetActive(true);
                _gameOverPanel.transform.localScale = Vector3.zero;
                _gameOverPanel.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
            }
        }

        /// <summary>
        /// Mevcut seviyeyi yeniden başlatır.
        /// </summary>
        public void RestartLevel()
        {
            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// Sonraki seviyeye geçer ve kaydeder.
        /// </summary>
        public void NextLevel()
        {
            _currentLevel++;
            PlayerPrefs.SetInt(PREFS_LEVEL_KEY, _currentLevel);
            PlayerPrefs.Save();

            DOTween.KillAll();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        #region Analitik & Monetizasyon (GameAnalytics & AppLovin MAX Köprüsü)
        /// <summary>
        /// GameAnalytics / Firebase vb. analiz platformları için merkezi olay gönderme köprüsü.
        /// </summary>
        public void LogAnalyticsEvent(string eventName, Dictionary<string, object> parameters = null)
        {
            string paramSummary = parameters != null ? string.Join(", ", parameters) : "None";
            Debug.Log($"<color=#AB47BC><b>[Analytics SDK]</b></color> Event: {eventName} | Params: [{paramSummary}]");
            // Gelecekte: GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, ...);
        }

        /// <summary>
        /// AppLovin MAX / IronSource geçiş reklamı (Interstitial) gösterme köprüsü.
        /// </summary>
        public void ShowInterstitialAd(string placement)
        {
            Debug.Log($"<color=#FFA726><b>[Ad SDK - Interstitial]</b></color> Reklam talep edildi: {placement}");
            // Gelecekte: MaxSdk.ShowInterstitial("YOUR_AD_UNIT_ID");
        }

        /// <summary>
        /// AppLovin MAX ödüllü video reklam (Rewarded Ad) köprüsü.
        /// </summary>
        public void ShowRewardedAd(string placement, Action onReward)
        {
            Debug.Log($"<color=#FFA726><b>[Ad SDK - Rewarded]</b></color> Ödüllü video reklam istendi: {placement}");
            // Gelecekte: MaxSdk.ShowRewardedAd("YOUR_AD_UNIT_ID");
            onReward?.Invoke();
        }
        #endregion

        #region Odin Inspector Test Araçları
        [Button("Zafer Tetikle (Win)", ButtonSizes.Small), TabGroup("GameTabs", "Oyun Durumu")]
        private void TestWin() => LevelComplete();

        [Button("Yenilgi Tetikle (Fail)", ButtonSizes.Small), TabGroup("GameTabs", "Oyun Durumu")]
        private void TestFail() => LevelFail();

        [Button("Sonraki Seviye (Next)", ButtonSizes.Small), TabGroup("GameTabs", "Oyun Durumu")]
        private void TestNext() => NextLevel();

        [Button("Seviye İlerlemesini Sıfırla", ButtonSizes.Small), TabGroup("GameTabs", "Oyun Durumu")]
        public void ResetLevelProgress()
        {
            _currentLevel = 1;
            PlayerPrefs.SetInt(PREFS_LEVEL_KEY, 1);
            PlayerPrefs.Save();
            UpdateLevelDisplay();
        }
        #endregion
    }
}
