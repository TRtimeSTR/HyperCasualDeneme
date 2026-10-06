using System;
using GateRunner.Data;
using GateRunner.Movement;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Gates
{
    /// <summary>
    /// Yol üzerinde yan yana duran sol ve sağ kapı çiftini yönetir.
    /// Oyuncu bir kapıdan geçtiğinde ikiliyi koordine eder ve çifte tetiklemeyi önler.
    /// </summary>
    [SelectionBase]
    public class GatePair : SerializedMonoBehaviour
    {
        [FoldoutGroup("Kapı Referansları")]
        [Required("Sol kapı atanmalıdır.")]
        [SerializeField] private Gate _leftGate;

        [FoldoutGroup("Kapı Referansları")]
        [Required("Sağ kapı atanmalıdır.")]
        [SerializeField] private Gate _rightGate;

        #region Events
        public event Action<Gate, SwerveMovement> OnGatePassed;
        #endregion

        public Gate LeftGate => _leftGate;
        public Gate RightGate => _rightGate;

        private void OnEnable()
        {
            if (_leftGate != null) _leftGate.OnPlayerEntered += HandleGateEntered;
            if (_rightGate != null) _rightGate.OnPlayerEntered += HandleGateEntered;
        }

        private void OnDisable()
        {
            if (_leftGate != null) _leftGate.OnPlayerEntered -= HandleGateEntered;
            if (_rightGate != null) _rightGate.OnPlayerEntered -= HandleGateEntered;
        }

        /// <summary>
        /// Sol ve sağ kapıları verilen veriler ile ayarlar.
        /// </summary>
        public void Configure(GateData leftData, GateData rightData)
        {
            if (_leftGate != null) _leftGate.Configure(leftData);
            if (_rightGate != null) _rightGate.Configure(rightData);
        }

        private void HandleGateEntered(Gate triggeredGate, SwerveMovement player)
        {
            // Bir kapıdan geçildiğinde diğer kapıyı da tetiklenmiş say veya devre dışı bırak
            if (_leftGate != null && _leftGate != triggeredGate)
            {
                _leftGate.ResetGate();
                _leftGate.gameObject.SetActive(false);
            }

            if (_rightGate != null && _rightGate != triggeredGate)
            {
                _rightGate.ResetGate();
                _rightGate.gameObject.SetActive(false);
            }

            OnGatePassed?.Invoke(triggeredGate, player);
        }

        /// <summary>
        /// Havuzdan çekildiğinde her iki kapıyı da aktif edip sıfırlar.
        /// </summary>
        public void ResetPair()
        {
            if (_leftGate != null)
            {
                _leftGate.gameObject.SetActive(true);
                _leftGate.ResetGate();
            }

            if (_rightGate != null)
            {
                _rightGate.gameObject.SetActive(true);
                _rightGate.ResetGate();
            }
        }

        #region Odin Test Buttons
        [Button("Rastgele Kapı Çifti Oluştur", ButtonSizes.Medium), FoldoutGroup("Odin Test")]
        private void GenerateRandomPair()
        {
            GateData left = new GateData(GateOperationType.Add, UnityEngine.Random.Range(5, 20));
            GateData right = new GateData(GateOperationType.Multiply, UnityEngine.Random.Range(2, 4));
            Configure(left, right);
        }
        #endregion
    }
}
