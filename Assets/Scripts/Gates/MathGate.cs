using System;
using GateRunner.Data;
using GateRunner.Player;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace GateRunner.Gates
{
    /// <summary>
    /// Karakterin içinden geçtiğinde boyutunu değiştiren matematiksel kapı bileşeni.
    /// Odin Inspector'ın OnValueChanged özelliği sayesinde işlem veya değer değiştiğinde
    /// materyal rengini ve metnini Editör ve Çalışma zamanında anlık günceller.
    /// </summary>
    [ExecuteAlways]
    [SelectionBase]
    public class MathGate : SerializedMonoBehaviour
    {
        [Title("Matematiksel İşlem Ayarları", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Kapının oyuncuya uygulayacağı işlem türü (+, -, x, ÷).")]
        [EnumToggleButtons]
        [OnValueChanged(nameof(UpdateVisuals))]
        [SerializeField] private GateOperationType _operationType = GateOperationType.Add;

        [Tooltip("İşlemde kullanılacak sayısal değer.")]
        [MinValue(1)]
        [OnValueChanged(nameof(UpdateVisuals))]
        [SerializeField] private int _value = 10;

        [FoldoutGroup("Görsel & Metin Bileşenleri")]
        [Tooltip("İşlemin ekranda yazdırılacağı TextMeshPro bileşeni.")]
        [OnValueChanged(nameof(UpdateVisuals))]
        [SerializeField] private TMP_Text _textMesh;

        [FoldoutGroup("Görsel & Metin Bileşenleri")]
        [Tooltip("Kapı renginin uygulanacağı ana MeshRenderer.")]
        [OnValueChanged(nameof(UpdateVisuals))]
        [SerializeField] private Renderer _gateRenderer;

        [FoldoutGroup("Renk Teması (Odin Otomatik Renklendirme)")]
        [Tooltip("Pozitif işlemde (Buff) kullanılacak canlı mavi/yeşil tema.")]
        [OnValueChanged(nameof(UpdateVisuals))]
        [SerializeField] private Color _buffColor = new Color(0.12f, 0.65f, 1.0f, 0.85f);

        [FoldoutGroup("Renk Teması (Odin Otomatik Renklendirme)")]
        [Tooltip("Negatif işlemde (Debuff) kullanılacak kırmızı tema.")]
        [OnValueChanged(nameof(UpdateVisuals))]
        [SerializeField] private Color _debuffColor = new Color(1.0f, 0.22f, 0.22f, 0.85f);

        [FoldoutGroup("Fizik & Tetikleyici")]
        [SerializeField] private Collider _triggerCollider;

        [ShowInInspector, ReadOnly, FoldoutGroup("Durum Bilgisi")]
        private bool _isTriggered = false;

        #region Events
        public event Action<MathGate, PlayerModifier> OnGateTriggered;
        #endregion

        #region Public Properties
        public GateOperationType OperationType => _operationType;
        public int Value => _value;
        public bool IsTriggered => _isTriggered;
        public bool IsBuff => _operationType == GateOperationType.Add || _operationType == GateOperationType.Multiply;
        #endregion

        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<Collider>();
            }

            if (_propBlock == null)
            {
                _propBlock = new MaterialPropertyBlock();
            }

            UpdateVisuals();
        }

        private void OnEnable()
        {
            UpdateVisuals();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            UpdateVisuals();
        }
#endif

        /// <summary>
        /// Kapıyı yeni işlem ve değer ile yapılandırır (Örn: Seviye üreticisi tarafından çağrılır).
        /// </summary>
        public void Configure(GateOperationType operationType, int value)
        {
            _operationType = operationType;
            _value = Mathf.Max(1, value);
            _isTriggered = false;

            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = true;
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Odin OnValueChanged ile tetiklenen görsel güncelleme metodu.
        /// İşlem türüne göre metni (+10, x2 vb.) ve materyal rengini anında yeniler.
        /// </summary>
        public void UpdateVisuals()
        {
            // 1. TextMeshPro Metnini Formatla
            if (_textMesh != null)
            {
                _textMesh.text = GetFormattedText();
            }

            // 2. Materyal Rengini Otomatik Belirle (Buff = Mavi/Yeşil, Debuff = Kırmızı)
            if (_gateRenderer != null)
            {
                if (_propBlock == null)
                {
                    _propBlock = new MaterialPropertyBlock();
                }

                _gateRenderer.GetPropertyBlock(_propBlock);
                Color targetColor = IsBuff ? _buffColor : _debuffColor;
                _propBlock.SetColor("_BaseColor", targetColor);
                _propBlock.SetColor("_Color", targetColor);
                _gateRenderer.SetPropertyBlock(_propBlock);
            }
        }

        /// <summary>
        /// Formatlanmış matematiksel metni üretir.
        /// </summary>
        public string GetFormattedText()
        {
            return _operationType switch
            {
                GateOperationType.Add => $"+{_value}",
                GateOperationType.Subtract => $"-{_value}",
                GateOperationType.Multiply => $"x{_value}",
                GateOperationType.Divide => $"÷{_value}",
                _ => $"{_value}"
            };
        }

        /// <summary>
        /// Karakter kapıdan geçtiğinde güvenli tetikleme gerçekleştirir (Çift tetiklenme korumalı).
        /// </summary>
        public bool Trigger(PlayerModifier player)
        {
            if (_isTriggered) return false;

            _isTriggered = true;

            // Çift tetiklemeyi önlemek için collider'ı anında kapat
            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = false;
            }

            OnGateTriggered?.Invoke(this, player);
            return true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTriggered) return;

            // Oyuncu üzerinde PlayerModifier bileşeni var mı kontrol et
            if (other.TryGetComponent<PlayerModifier>(out var playerModifier) ||
                other.GetComponentInParent<PlayerModifier>() is { } parentModifier && (playerModifier = parentModifier) != null)
            {
                if (Trigger(playerModifier))
                {
                    playerModifier.ApplyGateOperation(_operationType, _value);
                }
            }
        }

        /// <summary>
        /// Havuzlama (Object Pooling) için kapıyı sıfırlar.
        /// </summary>
        public void ResetGate()
        {
            _isTriggered = false;
            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = true;
            }
            UpdateVisuals();
        }

        #region Odin Inspector Test Araçları
        [Button("+25 Ekle (Buff)", ButtonSizes.Small), FoldoutGroup("Hızlı Test Butonları")]
        private void SetQuickBuffAdd() => Configure(GateOperationType.Add, 25);

        [Button("x2 Çarp (Buff)", ButtonSizes.Small), FoldoutGroup("Hızlı Test Butonları")]
        private void SetQuickBuffMultiply() => Configure(GateOperationType.Multiply, 2);

        [Button("-10 Çıkar (Debuff)", ButtonSizes.Small), FoldoutGroup("Hızlı Test Butonları")]
        private void SetQuickDebuffSubtract() => Configure(GateOperationType.Subtract, 10);

        [Button("÷2 Böl (Debuff)", ButtonSizes.Small), FoldoutGroup("Hızlı Test Butonları")]
        private void SetQuickDebuffDivide() => Configure(GateOperationType.Divide, 2);
        #endregion
    }
}
