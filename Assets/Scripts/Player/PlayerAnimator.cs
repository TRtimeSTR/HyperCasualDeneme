using System;
using DG.Tweening;
using GateRunner.Managers;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Player
{
    /// <summary>
    /// Karakterin animasyon durumlarını (Koşma, Zafer, Yenilgi) yöneten kontrolcü.
    /// Kapsül gibi basit mesh'ler için DOTween tabanlı prosedürel koşma adım sallantısı (wobble/hop),
    /// ileride eklenebilecek humanoid 3D modeller için ise Animator parametrelerini ('IsRunning', 'TriggerWin', 'TriggerFail') senkronize eder.
    /// </summary>
    [SelectionBase]
    public class PlayerAnimator : SerializedMonoBehaviour
    {
        [TabGroup("AnimTabs", "Animator")]
        [Tooltip("Opsiyonel Unity Animator bileşeni (Humanoid modeller için).")]
        [SerializeField] private Animator _animator;

        [TabGroup("AnimTabs", "Animator")]
        [Tooltip("Koşma durumu boolean parametresi.")]
        [SerializeField] private string _isRunningParam = "IsRunning";

        [TabGroup("AnimTabs", "Animator")]
        [Tooltip("Bölüm tamamlama zafer tetikleyici parametresi.")]
        [SerializeField] private string _triggerWinParam = "TriggerWin";

        [TabGroup("AnimTabs", "Animator")]
        [Tooltip("Bölüm başarısız olma tetikleyici parametresi.")]
        [SerializeField] private string _triggerFailParam = "TriggerFail";

        [TabGroup("AnimTabs", "Prosedürel Wobble")]
        [Tooltip("Sallantı uygulanacak görsel transform. Boşsa bu objenin kendisi veya 'Visual' alt objesi kullanılır.")]
        [SerializeField] private Transform _visualModel;

        [TabGroup("AnimTabs", "Prosedürel Wobble")]
        [Tooltip("Prosedürel koşma hop & tilt sallantısı aktif mi?")]
        [SerializeField] private bool _enableProceduralWobble = true;

        [TabGroup("AnimTabs", "Prosedürel Wobble")]
        [Tooltip("Tek bir adımın süresi (saniye).")]
        [SerializeField, Range(0.1f, 0.5f)] private float _stepDuration = 0.20f;

        [TabGroup("AnimTabs", "Prosedürel Wobble")]
        [Tooltip("Koşarken adımlarda dikey zıplama yüksekliği (metre).")]
        [SerializeField, Range(0.01f, 0.2f)] private float _hopHeight = 0.06f;

        [TabGroup("AnimTabs", "Prosedürel Wobble")]
        [Tooltip("Koşarken sağa-sola yalpalanma açısı (derece).")]
        [SerializeField, Range(0.5f, 15f)] private float _rollWobbleAngle = 3.5f;

        #region Live Stats (Odin)
        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Durum")]
        public bool IsRunning => _isRunning;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Durum")]
        public bool HasAnimator => _animator != null;
        #endregion

        private int _isRunningHash;
        private int _triggerWinHash;
        private int _triggerFailHash;

        private Vector3 _initialLocalPos;
        private Quaternion _initialLocalRot;
        private bool _isRunning = false;

        private Tween _hopTween;
        private Sequence _rollSequence;
        private Sequence _celebrationSequence;

        private Transform VisualTarget => _visualModel != null ? _visualModel : transform;

        private void Awake()
        {
            if (_visualModel == null)
            {
                _visualModel = transform.Find("Visual") ?? transform;
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _isRunningHash = Animator.StringToHash(_isRunningParam);
            _triggerWinHash = Animator.StringToHash(_triggerWinParam);
            _triggerFailHash = Animator.StringToHash(_triggerFailParam);

            _initialLocalPos = VisualTarget.localPosition;
            _initialLocalRot = VisualTarget.localRotation;
        }

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

                if (GameManager.Instance.State == GameState.Running)
                {
                    StartRunningAnimation();
                }
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }

            KillAllTweens();
        }

        private void HandleGameStateChanged(GameState state)
        {
            switch (state)
            {
                case GameState.Ready:
                    ResetToIdle();
                    break;

                case GameState.Running:
                    StartRunningAnimation();
                    break;

                case GameState.Victory:
                    PlayVictoryAnimation();
                    break;

                case GameState.GameOver:
                    PlayFailAnimation();
                    break;
            }
        }

        /// <summary>
        /// Koşma durumunu ve prosedürel adım sallantısını başlatır.
        /// </summary>
        public void StartRunningAnimation()
        {
            if (_isRunning) return;
            _isRunning = true;

            KillAllTweens();

            // 1. Animator parametresi
            if (_animator != null && _animator.isInitialized)
            {
                _animator.SetBool(_isRunningHash, true);
            }

            // 2. Prosedürel DOTween Wobble
            if (_enableProceduralWobble && VisualTarget != null)
            {
                // Dikey adım sekmesi (Hop)
                _hopTween = VisualTarget.DOLocalMoveY(_initialLocalPos.y + _hopHeight, _stepDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLoops(-1, LoopType.Yoyo);

                // Sağa-sola tatlı yalpalanma (Roll wobble)
                _rollSequence = DOTween.Sequence();
                _rollSequence.Append(VisualTarget.DOLocalRotate(new Vector3(0f, 0f, _rollWobbleAngle), _stepDuration * 0.5f).SetEase(Ease.InOutSine))
                             .Append(VisualTarget.DOLocalRotate(new Vector3(0f, 0f, -_rollWobbleAngle), _stepDuration).SetEase(Ease.InOutSine))
                             .Append(VisualTarget.DOLocalRotate(Vector3.zero, _stepDuration * 0.5f).SetEase(Ease.InOutSine))
                             .SetLoops(-1);
            }
        }

        /// <summary>
        /// Koşma durumunu durdurur ve bekleme (idle) pozisyonuna döner.
        /// </summary>
        public void ResetToIdle()
        {
            _isRunning = false;
            KillAllTweens();

            if (_animator != null && _animator.isInitialized)
            {
                _animator.SetBool(_isRunningHash, false);
            }

            if (VisualTarget != null)
            {
                VisualTarget.localPosition = _initialLocalPos;
                VisualTarget.localRotation = _initialLocalRot;
            }
        }

        /// <summary>
        /// Seviye zaferi kutlama animasyonu.
        /// </summary>
        public void PlayVictoryAnimation()
        {
            _isRunning = false;
            KillAllTweens();

            if (_animator != null && _animator.isInitialized)
            {
                _animator.SetBool(_isRunningHash, false);
                _animator.SetTrigger(_triggerWinHash);
            }

            if (_enableProceduralWobble && VisualTarget != null)
            {
                _celebrationSequence = DOTween.Sequence();
                // Zafer zıplaması ve minik squash
                _celebrationSequence.Append(VisualTarget.DOLocalMoveY(_initialLocalPos.y + 0.35f, 0.28f).SetEase(Ease.OutQuad))
                                    .Append(VisualTarget.DOLocalMoveY(_initialLocalPos.y, 0.22f).SetEase(Ease.InQuad))
                                    .Append(VisualTarget.DOPunchScale(new Vector3(0.12f, -0.15f, 0.12f), 0.3f, 4, 0.4f))
                                    .SetLoops(3);
            }
        }

        /// <summary>
        /// Seviye yenilgi devrilme animasyonu.
        /// </summary>
        public void PlayFailAnimation()
        {
            _isRunning = false;
            KillAllTweens();

            if (_animator != null && _animator.isInitialized)
            {
                _animator.SetBool(_isRunningHash, false);
                _animator.SetTrigger(_triggerFailHash);
            }

            if (_enableProceduralWobble && VisualTarget != null)
            {
                // Karakter arkaya/öne doğru hafifçe yıkılır
                VisualTarget.DOLocalRotate(new Vector3(75f, 0f, 0f), 0.38f)
                    .SetEase(Ease.OutBounce);
            }
        }

        private void KillAllTweens()
        {
            if (_hopTween != null && _hopTween.IsActive()) _hopTween.Kill();
            if (_rollSequence != null && _rollSequence.IsActive()) _rollSequence.Kill();
            if (_celebrationSequence != null && _celebrationSequence.IsActive()) _celebrationSequence.Kill();
        }

        #region Odin Inspector Test Buttons
        [Button("Koşma Başlat (Test Run)", ButtonSizes.Small), TabGroup("AnimTabs", "Test Butonları")]
        public void TestRun() => StartRunningAnimation();

        [Button("Zafer Kutlaması (Test Win)", ButtonSizes.Small), TabGroup("AnimTabs", "Test Butonları")]
        public void TestWin() => PlayVictoryAnimation();

        [Button("Yenilgi (Test Fail)", ButtonSizes.Small), TabGroup("AnimTabs", "Test Butonları")]
        public void TestFail() => PlayFailAnimation();

        [Button("Sıfırla (Reset Idle)", ButtonSizes.Small), TabGroup("AnimTabs", "Test Butonları")]
        public void TestReset() => ResetToIdle();
        #endregion
    }
}
