using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GateRunner.Input
{
    /// <summary>
    /// Unity New Input System ile çalışan mobil dokunmatik (touch) ve fare (mouse) girdisi yöneticisi.
    /// Farklı ekran çözünürlüklerinde (DPI / Screen.width) tutarlı girdi üretmek için normalizasyon uygular.
    /// </summary>
    public class SwerveInputHandler : MonoBehaviour
    {
        [Header("--- İsteğe Bağlı Input Action Bağlantıları ---")]
        [Tooltip("Boş bırakılırsa otomatik olarak New Input System 'Pointer' (Touchscreen / Mouse) kullanılır.")]
        [SerializeField] private InputActionReference _touchPressAction;
        [SerializeField] private InputActionReference _touchDeltaAction;

        [Header("--- Hassasiyet Ayarı ---")]
        [Tooltip("Girdi ölçekleme katsayısı.")]
        [Range(0.1f, 5f)]
        [SerializeField] private float _inputScale = 1.0f;

        #region Events
        public event Action OnTouchStarted;
        public event Action OnTouchEnded;
        public event Action<float> OnSwerveDelta; // float: normalized delta X
        #endregion

        #region Properties
        public bool IsTouching { get; private set; }
        public float NormalizedDeltaX { get; private set; }
        #endregion

        private bool _wasTouchingLastFrame;

        private void OnEnable()
        {
            if (_touchPressAction != null) _touchPressAction.action.Enable();
            if (_touchDeltaAction != null) _touchDeltaAction.action.Enable();
        }

        private void OnDisable()
        {
            if (_touchPressAction != null) _touchPressAction.action.Disable();
            if (_touchDeltaAction != null) _touchDeltaAction.action.Disable();
            ResetInput();
        }

        private void Update()
        {
            ProcessInput();
        }

        private void ProcessInput()
        {
            bool isPressed = false;
            float rawDeltaX = 0f;

            // 1. Durum: Eğer Inspector'dan InputAction bağlanmışsa
            if (_touchPressAction != null && _touchDeltaAction != null)
            {
                isPressed = _touchPressAction.action.IsPressed();
                if (isPressed)
                {
                    Vector2 delta = _touchDeltaAction.action.ReadValue<Vector2>();
                    rawDeltaX = delta.x;
                }
            }
            // 2. Durum: Doğrudan New Input System Pointer (Dokunmatik ekran veya Mouse)
            else if (Pointer.current != null)
            {
                isPressed = Pointer.current.press.isPressed;
                if (isPressed)
                {
                    rawDeltaX = Pointer.current.delta.ReadValue().x;
                }
            }

            // Dokunma Başlama / Bitme Event'leri
            if (isPressed && !_wasTouchingLastFrame)
            {
                IsTouching = true;
                OnTouchStarted?.Invoke();
            }
            else if (!isPressed && _wasTouchingLastFrame)
            {
                IsTouching = false;
                OnTouchEnded?.Invoke();
            }

            _wasTouchingLastFrame = isPressed;
            IsTouching = isPressed;

            if (isPressed)
            {
                // Ekran genişliğine göre normalize et (Farklı telefon çözünürlüklerinde eşit hassasiyet sağlar)
                float screenFactor = Screen.width > 0 ? Screen.width : 1080f;
                NormalizedDeltaX = (rawDeltaX / screenFactor) * _inputScale;

                if (Mathf.Abs(NormalizedDeltaX) > Mathf.Epsilon)
                {
                    OnSwerveDelta?.Invoke(NormalizedDeltaX);
                }
            }
            else
            {
                NormalizedDeltaX = 0f;
            }
        }

        public void ResetInput()
        {
            IsTouching = false;
            _wasTouchingLastFrame = false;
            NormalizedDeltaX = 0f;
        }
    }
}
