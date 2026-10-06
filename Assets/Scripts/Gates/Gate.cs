using System;
using GateRunner.Data;
using GateRunner.Movement;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace GateRunner.Gates
{
    /// <summary>
    /// Oyuncunun içinden geçtiğinde matematiksel işlem uygulayan tekil kapı bileşeni.
    /// Renk, metin ve tetikleyici durumunu yönetir, havuzlama (pooling) için sıfırlanabilir.
    /// </summary>
    [SelectionBase]
    public class Gate : SerializedMonoBehaviour
    {
        [FoldoutGroup("Görsel Bileşenler")]
        [Tooltip("İşlem metninin gösterileceği TextMeshPro bileşeni.")]
        [SerializeField] private TMP_Text _textMesh;

        [FoldoutGroup("Görsel Bileşenler")]
        [Tooltip("Kapının renk temasının uygulanacağı ana MeshRenderer.")]
        [SerializeField] private Renderer _gateRenderer;

        [FoldoutGroup("Renk Teması")]
        [SerializeField] private Color _buffColor = new Color(0.12f, 0.65f, 1.0f, 0.85f); // Canlı Mavi / Yeşil
        [FoldoutGroup("Renk Teması")]
        [SerializeField] private Color _debuffColor = new Color(1.0f, 0.25f, 0.25f, 0.85f); // Kırmızı

        [FoldoutGroup("Tetikleyici Ayarları")]
        [SerializeField] private Collider _triggerCollider;

        [ShowInInspector, ReadOnly, FoldoutGroup("Durum")]
        private GateData _currentData = new GateData(GateOperationType.Add, 10);

        [ShowInInspector, ReadOnly, FoldoutGroup("Durum")]
        private bool _isTriggered = false;

        #region Events
        public event Action<Gate, SwerveMovement> OnPlayerEntered;
        #endregion

        #region Properties
        public GateData Data => _currentData;
        public bool IsTriggered => _isTriggered;
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
        }

        /// <summary>
        /// Kapıyı yeni matematiksel veri ile yapılandırır ve görselini günceller.
        /// </summary>
        public void Configure(GateData data)
        {
            _currentData = data ?? new GateData(GateOperationType.Add, 5);
            _isTriggered = false;

            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = true;
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Metin ve materyal rengini veriye göre günceller.
        /// </summary>
        public void UpdateVisuals()
        {
            if (_textMesh != null)
            {
                _textMesh.text = _currentData.GetFormattedText();
            }

            if (_gateRenderer != null)
            {
                if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
                _gateRenderer.GetPropertyBlock(_propBlock);
                Color targetColor = _currentData.IsBuff ? _buffColor : _debuffColor;
                _propBlock.SetColor("_BaseColor", targetColor);
                _propBlock.SetColor("_Color", targetColor);
                _gateRenderer.SetPropertyBlock(_propBlock);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTriggered) return;

            // Oyuncu kontrolcüsünü kontrol et
            if (other.TryGetComponent<SwerveMovement>(out var playerMovement) ||
                other.GetComponentInParent<SwerveMovement>() is { } parentMovement && (playerMovement = parentMovement) != null)
            {
                _isTriggered = true;
                if (_triggerCollider != null)
                {
                    _triggerCollider.enabled = false;
                }

                OnPlayerEntered?.Invoke(this, playerMovement);
            }
        }

        /// <summary>
        /// Havuzdan yeniden çekildiğinde (Re-use) kapıyı sıfırlar.
        /// </summary>
        public void ResetGate()
        {
            _isTriggered = false;
            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = true;
            }
        }

        #region Odin Test Buttons
        [Button("Test Buff (+25)", ButtonSizes.Small), FoldoutGroup("Odin Test Araçları")]
        private void TestSetBuff()
        {
            Configure(new GateData(GateOperationType.Add, 25));
        }

        [Button("Test Multiply (x2)", ButtonSizes.Small), FoldoutGroup("Odin Test Araçları")]
        private void TestSetMultiply()
        {
            Configure(new GateData(GateOperationType.Multiply, 2));
        }

        [Button("Test Debuff (-10)", ButtonSizes.Small), FoldoutGroup("Odin Test Araçları")]
        private void TestSetDebuff()
        {
            Configure(new GateData(GateOperationType.Subtract, 10));
        }
        #endregion
    }
}
