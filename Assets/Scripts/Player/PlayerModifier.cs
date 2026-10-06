using System;
using DG.Tweening;
using GateRunner.Data;
using GateRunner.Movement;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Player
{
    public enum CharacterScalingMode
    {
        [InspectorName("Orantılı Büyüme (Uniform - XYZ)")]
        Uniform,

        [InspectorName("Daha Uzun (Taller - Sadece Y)")]
        Taller,

        [InspectorName("Daha Kalın (Thicker - Sadece X & Z)")]
        Thicker,

        [InspectorName("Hem Uzun Hem Kalın (Taller & Thicker)")]
        Both
    }

    /// <summary>
    /// Karakterin kapılardan (MathGate) geçtiğinde matematiksel işlem sonucuna göre
    /// fiziksel boyutunu (Scale - Taller/Thicker) pürüzsüzce büyüten veya küçülten modifikatör.
    /// </summary>
    [RequireComponent(typeof(SwerveMovement))]
    [SelectionBase]
    public class PlayerModifier : SerializedMonoBehaviour
    {
        [Title("Görsel Hedef & Büyüme Modu", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Ölçeklendirilecek görsel model Transform'u. Boş bırakılırsa bu objenin kendi Transform'u kullanılır.")]
        [SerializeField] private Transform _visualModel;

        [EnumToggleButtons]
        [SerializeField] private CharacterScalingMode _scalingMode = CharacterScalingMode.Uniform;

        [FoldoutGroup("Boyut & Puan Parametreleri")]
        [Tooltip("Karakterin başlangıç matematiksel puanı.")]
        [SerializeField, MinValue(1)] private int _initialScore = 10;

        [FoldoutGroup("Boyut & Puan Parametreleri")]
        [Tooltip("Her bir puan artışının ölçeğe (scale) yansıma katsayısı.")]
        [SerializeField, Range(0.01f, 0.2f)] private float _scalePerScorePoint = 0.035f;

        [FoldoutGroup("Boyut & Puan Parametreleri")]
        [Tooltip("Ulaşılabilecek minimum ölçek katsayısı.")]
        [SerializeField, MinValue(0.2f)] private float _minScale = 0.4f;

        [FoldoutGroup("Boyut & Puan Parametreleri")]
        [Tooltip("Ulaşılabilecek maksimum ölçek katsayısı.")]
        [SerializeField, MinValue(1.0f)] private float _maxScale = 4.0f;

        [FoldoutGroup("Boyut & Puan Parametreleri")]
        [Tooltip("Hedef boyuta ulaşma yumuşaklığı (Lerp).")]
        [SerializeField, Range(2f, 30f)] private float _scaleLerpSpeed = 12.0f;

        #region Live Stats (Odin)
        [ShowInInspector, ReadOnly, ProgressBar(1, 100, 0.2f, 0.7f, 1f), FoldoutGroup("Canlı Durum")]
        public int CurrentScore => _currentScore;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Durum")]
        public Vector3 TargetScale => _targetScale;
        #endregion

        #region Events
        public event Action<int> OnScoreChanged;
        public event Action<Vector3> OnScaleChanged;
        #endregion

        private int _currentScore;
        private Vector3 _targetScale = Vector3.one;
        private Transform TargetTransform => _visualModel != null ? _visualModel : transform;

        private void Awake()
        {
            if (_visualModel == null)
            {
                // Görsel modeli otomatik tespit etmeye çalış
                _visualModel = transform.Find("Visual") ?? transform;
            }

            _currentScore = _initialScore;
            RecalculateTargetScale();
            TargetTransform.localScale = _targetScale;
        }

        private void Update()
        {
            ApplySmoothScaling();
        }

        /// <summary>
        /// Kapıdan (MathGate) gelen işlemi mevcut skora uygular ve hedef boyutu günceller.
        /// </summary>
        public void ApplyGateOperation(GateOperationType operationType, int value)
        {
            int previousScore = _currentScore;

            _currentScore = operationType switch
            {
                GateOperationType.Add => _currentScore + value,
                GateOperationType.Subtract => Mathf.Max(1, _currentScore - value),
                GateOperationType.Multiply => _currentScore * value,
                GateOperationType.Divide => value > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)_currentScore / value)) : _currentScore,
                _ => _currentScore
            };

            Debug.Log($"<color=#00E676><b>[MathGate Geçişi]</b></color> İşlem: {operationType} {value} | Eski Skor: {previousScore} -> Yeni Skor: {_currentScore}");

            RecalculateTargetScale();
            OnScoreChanged?.Invoke(_currentScore);
            OnScaleChanged?.Invoke(_targetScale);

            // DOTween ile Pop-Up / Punch / Shake Animasyonu
            ApplyDOTweenScaleFeedback(operationType);
        }

        private void ApplyDOTweenScaleFeedback(GateOperationType operationType)
        {
            Transform t = TargetTransform;
            t.DOKill();

            bool isBuff = operationType == GateOperationType.Add || operationType == GateOperationType.Multiply;

            if (isBuff)
            {
                // Tatmin edici OutBack büyüme ve hafif punch efekti
                t.DOScale(_targetScale, 0.35f)
                    .SetEase(Ease.OutBack)
                    .OnComplete(() =>
                    {
                        t.DOPunchScale(_targetScale * 0.15f, 0.25f, 6, 0.5f);
                    });
            }
            else
            {
                // Küçülme veya cezada DOShakeScale sarsılma efekti
                t.DOScale(_targetScale, 0.25f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        t.DOShakeScale(0.25f, 0.2f, 8, 90f);
                    });
            }
        }

        private void OnDestroy()
        {
            TargetTransform.DOKill();
        }

        /// <summary>
        /// Mevcut skora ve seçilen moda göre hedef Vector3 ölçeğini hesaplar.
        /// </summary>
        private void RecalculateTargetScale()
        {
            // 1.0 taban ölçeği üzerinden skora göre artış hesapla
            float scaleMultiplier = 1.0f + (_currentScore - _initialScore) * _scalePerScorePoint;
            scaleMultiplier = Mathf.Clamp(scaleMultiplier, _minScale, _maxScale);

            _targetScale = _scalingMode switch
            {
                CharacterScalingMode.Uniform => Vector3.one * scaleMultiplier,
                CharacterScalingMode.Taller => new Vector3(1.0f, scaleMultiplier, 1.0f),
                CharacterScalingMode.Thicker => new Vector3(scaleMultiplier, 1.0f, scaleMultiplier),
                CharacterScalingMode.Both => new Vector3(scaleMultiplier, scaleMultiplier, scaleMultiplier),
                _ => Vector3.one * scaleMultiplier
            };
        }

        /// <summary>
        /// Karakterin lokal ölçeğini hedef boyuta doğru pürüzsüzce taşır (Lerp - DOTween aktif değilken yedek yumuşatma).
        /// </summary>
        private void ApplySmoothScaling()
        {
            Transform t = TargetTransform;
            if (!DOTween.IsTweening(t) && Vector3.Distance(t.localScale, _targetScale) > 0.001f)
            {
                t.localScale = Vector3.Lerp(t.localScale, _targetScale, _scaleLerpSpeed * Time.deltaTime);
            }
        }

        #region Odin Inspector Test Butonları
        [Button("Test: +20 Skor (Büyüt)", ButtonSizes.Small), FoldoutGroup("Odin Test Araçları")]
        private void TestAdd() => ApplyGateOperation(GateOperationType.Add, 20);

        [Button("Test: x2 Katla (Devasa Yap)", ButtonSizes.Small), FoldoutGroup("Odin Test Araçları")]
        private void TestMultiply() => ApplyGateOperation(GateOperationType.Multiply, 2);

        [Button("Test: -10 Küçült", ButtonSizes.Small), FoldoutGroup("Odin Test Araçları")]
        private void TestSubtract() => ApplyGateOperation(GateOperationType.Subtract, 10);

        [Button("Test: ÷2 Yarıya İndir", ButtonSizes.Small), FoldoutGroup("Odin Test Araçları")]
        private void TestDivide() => ApplyGateOperation(GateOperationType.Divide, 2);

        [Button("Boyutu Sıfırla (Reset)", ButtonSizes.Small), FoldoutGroup("Odin Test Araçları")]
        public void ResetScore()
        {
            _currentScore = _initialScore;
            RecalculateTargetScale();
            TargetTransform.localScale = _targetScale;
        }
        #endregion
    }
}
