using System;
using System.Collections.Generic;
using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [Tooltip("Oyun başlamadan önce ekranda görünen 'Tap to Start' paneli.")]
        [SerializeField] private GameObject _readyPanel;

        [TabGroup("GameTabs", "UI Panelleri")]
        [Tooltip("Hafifçe yanıp sönen 'Tap to Start' metni.")]
        [SerializeField] private TMP_Text _tapToStartText;

        [TabGroup("GameTabs", "UI Panelleri")]
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
            EnsureUIReferences();
            SetupUIButtons();
            UpdateLevelDisplay();
            SetupReadyState();
        }

        private void EnsureUIReferences()
        {
            if (_readyPanel == null) _readyPanel = GameObject.Find("Panel_Ready");
            if (_tapToStartText == null && _readyPanel != null) _tapToStartText = _readyPanel.GetComponentInChildren<TMP_Text>();
            if (_levelText == null)
            {
                var lvl = GameObject.Find("Text_Level");
                if (lvl != null) _levelText = lvl.GetComponent<TMP_Text>();
            }
            if (_victoryPanel == null) _victoryPanel = GameObject.Find("Panel_Victory");
            if (_gameOverPanel == null) _gameOverPanel = GameObject.Find("Panel_GameOver");
            if (_nextLevelButton == null && _victoryPanel != null) _nextLevelButton = _victoryPanel.GetComponentInChildren<Button>();
            if (_retryButton == null && _gameOverPanel != null) _retryButton = _gameOverPanel.GetComponentInChildren<Button>();

            // Eğer sahnede UI Canvas veya paneller eksikse otomatik olarak runtime canvas oluştur
            if (_readyPanel == null && FindAnyObjectByType<Canvas>() == null)
            {
                CreateFallbackUICanvas();
            }
        }

        private void CreateFallbackUICanvas()
        {
            var canvasGo = new GameObject("UI_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // Ready Panel (Tap to Start)
            _readyPanel = new GameObject("Panel_Ready");
            _readyPanel.transform.SetParent(canvasGo.transform, false);
            var readyRect = _readyPanel.AddComponent<RectTransform>();
            readyRect.anchorMin = Vector2.zero;
            readyRect.anchorMax = Vector2.one;
            readyRect.sizeDelta = Vector2.zero;

            var readyTxtGo = new GameObject("Text_TapToStart");
            readyTxtGo.transform.SetParent(_readyPanel.transform, false);
            var rTxtRect = readyTxtGo.AddComponent<RectTransform>();
            rTxtRect.anchorMin = new Vector2(0.5f, 0.22f);
            rTxtRect.anchorMax = new Vector2(0.5f, 0.22f);
            rTxtRect.pivot = new Vector2(0.5f, 0.5f);
            rTxtRect.sizeDelta = new Vector2(900f, 160f);
            var tmpR = readyTxtGo.AddComponent<TextMeshProUGUI>();
            tmpR.text = "TAP TO START!";
            tmpR.fontSize = 72;
            tmpR.fontStyle = FontStyles.Bold;
            tmpR.alignment = TextAlignmentOptions.Center;
            tmpR.color = new Color(1.0f, 0.84f, 0.12f);
            _tapToStartText = tmpR;

            // Level Text (Top-Center)
            var lvlGo = new GameObject("Text_Level");
            lvlGo.transform.SetParent(canvasGo.transform, false);
            var lvlRect = lvlGo.AddComponent<RectTransform>();
            lvlRect.anchorMin = new Vector2(0.5f, 1f);
            lvlRect.anchorMax = new Vector2(0.5f, 1f);
            lvlRect.pivot = new Vector2(0.5f, 1f);
            lvlRect.anchoredPosition = new Vector2(0f, -95f);
            lvlRect.sizeDelta = new Vector2(500f, 80f);
            var tmpL = lvlGo.AddComponent<TextMeshProUGUI>();
            tmpL.text = $"LEVEL {_currentLevel}";
            tmpL.fontSize = 56;
            tmpL.fontStyle = FontStyles.Bold;
            tmpL.alignment = TextAlignmentOptions.Center;
            tmpL.color = Color.white;
            _levelText = tmpL;

            // Coin Pill Badge (Top-Right)
            var coinPillGo = new GameObject("Panel_CoinPill");
            coinPillGo.transform.SetParent(canvasGo.transform, false);
            var pillRect = coinPillGo.AddComponent<RectTransform>();
            pillRect.anchorMin = new Vector2(1f, 1f);
            pillRect.anchorMax = new Vector2(1f, 1f);
            pillRect.pivot = new Vector2(1f, 1f);
            pillRect.anchoredPosition = new Vector2(-55f, -95f);
            pillRect.sizeDelta = new Vector2(185f, 68f);
            var pillImg = coinPillGo.AddComponent<Image>();
            pillImg.color = new Color(1f, 1f, 1f, 0.95f);

            var coinTxtGo = new GameObject("Text_Coin");
            coinTxtGo.transform.SetParent(coinPillGo.transform, false);
            var coinTxtRect = coinTxtGo.AddComponent<RectTransform>();
            coinTxtRect.anchorMin = Vector2.zero;
            coinTxtRect.anchorMax = Vector2.one;
            coinTxtRect.sizeDelta = Vector2.zero;
            var tmpCoin = coinTxtGo.AddComponent<TextMeshProUGUI>();
            tmpCoin.text = "🪙 0";
            tmpCoin.fontSize = 36;
            tmpCoin.fontStyle = FontStyles.Bold;
            tmpCoin.alignment = TextAlignmentOptions.Center;
            tmpCoin.color = new Color(0.18f, 0.18f, 0.18f);

            // Victory Panel
            _victoryPanel = CreateSimpleResultPanel(canvasGo.transform, "Panel_Victory", "BÖLÜM TAMAMLANDI!", "SONRAKİ BÖLÜM", new Color(0.1f, 0.75f, 0.3f, 0.95f), out _nextLevelButton);

            // Game Over Panel
            _gameOverPanel = CreateSimpleResultPanel(canvasGo.transform, "Panel_GameOver", "BÖLÜM BAŞARISIZ!", "TEKRAR DENE", new Color(0.85f, 0.2f, 0.2f, 0.95f), out _retryButton);
        }

        private GameObject CreateSimpleResultPanel(Transform parent, string name, string title, string btnLabel, Color bgColor, out Button outButton)
        {
            var pGo = new GameObject(name);
            pGo.transform.SetParent(parent, false);
            var pRect = pGo.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.5f, 0.5f);
            pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.pivot = new Vector2(0.5f, 0.5f);
            pRect.sizeDelta = new Vector2(700f, 480f);
            var img = pGo.AddComponent<Image>();
            img.color = bgColor;

            var tGo = new GameObject("Text_Title");
            tGo.transform.SetParent(pGo.transform, false);
            var tRect = tGo.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 1f);
            tRect.anchorMax = new Vector2(0.5f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -60f);
            tRect.sizeDelta = new Vector2(650f, 120f);
            var tmp = tGo.AddComponent<TextMeshProUGUI>();
            tmp.text = title;
            tmp.fontSize = 48;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            var bGo = new GameObject("Button_Action");
            bGo.transform.SetParent(pGo.transform, false);
            var bRect = bGo.AddComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0.5f, 0f);
            bRect.anchorMax = new Vector2(0.5f, 0f);
            bRect.pivot = new Vector2(0.5f, 0f);
            bRect.anchoredPosition = new Vector2(0f, 50f);
            bRect.sizeDelta = new Vector2(450f, 110f);
            var bImg = bGo.AddComponent<Image>();
            bImg.color = Color.white;
            outButton = bGo.AddComponent<Button>();

            var lblGo = new GameObject("Text_Label");
            lblGo.transform.SetParent(bGo.transform, false);
            var lblRect = lblGo.AddComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.sizeDelta = Vector2.zero;
            var tmpB = lblGo.AddComponent<TextMeshProUGUI>();
            tmpB.text = btnLabel;
            tmpB.fontSize = 38;
            tmpB.fontStyle = FontStyles.Bold;
            tmpB.alignment = TextAlignmentOptions.Center;
            tmpB.color = new Color(0.15f, 0.15f, 0.15f);

            pGo.SetActive(false);
            return pGo;
        }

        private void Update()
        {
            if (_currentState == GameState.Ready)
            {
                // New Input System: Ekrana ilk dokunma veya fare tıklaması ile koşuyu başlat
                bool isTapped = (Pointer.current != null && Pointer.current.press.wasPressedThisFrame) ||
                                (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                                (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);

                if (isTapped)
                {
                    StartGame();
                }
            }
        }

        private void SetupReadyState()
        {
            _currentState = GameState.Ready;
            OnGameStateChanged?.Invoke(_currentState);

            if (_readyPanel != null)
            {
                _readyPanel.SetActive(true);
                _readyPanel.transform.localScale = Vector3.one;

                Transform animTarget = _tapToStartText != null ? _tapToStartText.transform : _readyPanel.transform;
                animTarget.DOKill();
                animTarget.localScale = Vector3.one;
                animTarget.DOScale(1.12f, 0.7f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
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
            if (_currentState == GameState.Running) return;

            // Tap to Start panelini kapat
            if (_readyPanel != null)
            {
                Transform animTarget = _tapToStartText != null ? _tapToStartText.transform : _readyPanel.transform;
                animTarget.DOKill();
                _readyPanel.transform.DOKill();
                _readyPanel.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    _readyPanel.SetActive(false);
                });
            }

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
