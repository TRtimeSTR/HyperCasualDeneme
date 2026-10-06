using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Data
{
    /// <summary>
    /// Bir kapının matematiksel işlem türünü ve değerini tutan veri modeli.
    /// Hem ScriptableObject hem de Inspector üzerinden serileştirilebilir veri olarak kullanılabilir.
    /// </summary>
    [Serializable]
    public class GateData
    {
        [HorizontalGroup("GateDataGroup", 0.5f)]
        [HideLabel]
        [SerializeField] private GateOperationType _operationType = GateOperationType.Add;

        [HorizontalGroup("GateDataGroup")]
        [HideLabel]
        [MinValue(1)]
        [SerializeField] private int _value = 5;

        public GateOperationType OperationType => _operationType;
        public int Value => _value;

        public bool IsBuff => _operationType == GateOperationType.Add || _operationType == GateOperationType.Multiply;

        public GateData() { }

        public GateData(GateOperationType operationType, int value)
        {
            _operationType = operationType;
            _value = Mathf.Max(1, value);
        }

        /// <summary>
        /// Mevcut değer üzerinden kapı işlemini hesaplar.
        /// </summary>
        public int CalculateResult(int currentValue)
        {
            return _operationType switch
            {
                GateOperationType.Add => currentValue + _value,
                GateOperationType.Subtract => Mathf.Max(0, currentValue - _value),
                GateOperationType.Multiply => currentValue * _value,
                GateOperationType.Divide => _value > 0 ? Mathf.Max(1, Mathf.RoundToInt((float)currentValue / _value)) : currentValue,
                _ => currentValue
            };
        }

        /// <summary>
        /// Kapı üzerinde gösterilecek formatlanmış metni döndürür (Örn: +10, x2, -5, ÷3).
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
    }
}
