using GateRunner.Data;
using GateRunner.Input;
using UnityEngine;

namespace GateRunner.Movement
{
    /// <summary>
    /// Karakterin Z ekseninde sabit ileri koşmasını ve ekrandaki dokunmatik kaydırma girdileriyle
    /// X ekseninde (swerve) pürüzsüz hareket etmesini sağlayan ana hareket kontrolcüsü.
    /// Yol sınırlarını (-maxSwerveX, +maxSwerveX) kesin olarak korur ve viraj eğilme efekti uygular.
    /// </summary>
    [RequireComponent(typeof(SwerveInputHandler))]
    [SelectionBase]
    public class SwerveMovement : MonoBehaviour
    {
        [Header("--- Veri Konfigürasyonu ---")]
        [Tooltip("Hız, hassasiyet ve sınır değerlerini içeren ScriptableObject.")]
        [SerializeField] private RunnerData _runnerData;

        [Header("--- Görsel Model (Opsiyonel Lean/Eğilme İçin) ---")]
        [Tooltip("Eğilme rotasyonu uygulanacak çocuk (child) mesh objesi. Boş bırakılırsa ana transform döndürülür.")]
        [SerializeField] private Transform _visualModel;

        private SwerveInputHandler _inputHandler;
        private float _targetX;
        private float _currentForwardSpeed;
        private bool _canMove = true;

        #region Public Properties
        public bool CanMove => _canMove;
        public float TargetX => _targetX;
        public float CurrentForwardSpeed => _currentForwardSpeed;
        public RunnerData Data => _runnerData;
        #endregion

        private void Awake()
        {
            _inputHandler = GetComponent<SwerveInputHandler>();

            if (_runnerData != null)
            {
                _currentForwardSpeed = _runnerData.ForwardSpeed;
            }
            else
            {
                Debug.LogWarning($"[{nameof(SwerveMovement)}] RunnerData atanmamış! Lütfen bir RunnerData ScriptableObject bağlayın.", this);
            }

            _targetX = transform.position.x;
        }

        private void OnEnable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnSwerveDelta += HandleSwerveDelta;
            }
        }

        private void OnDisable()
        {
            if (_inputHandler != null)
            {
                _inputHandler.OnSwerveDelta -= HandleSwerveDelta;
            }
        }

        private void Update()
        {
            if (!_canMove || _runnerData == null) return;

            MoveForward();
            ApplySmoothSwerve();

            if (_runnerData.EnableLeaning)
            {
                ApplyLeaningEffect();
            }
        }

        /// <summary>
        /// Karakteri Z ekseninde (ileri) sabit hızla yürütür.
        /// </summary>
        private void MoveForward()
        {
            Vector3 forwardMove = Vector3.forward * (_currentForwardSpeed * Time.deltaTime);
            transform.position += forwardMove;
        }

        /// <summary>
        /// SwerveInputHandler'dan gelen normalize kaydırma girdisini hedef X pozisyonuna ekler ve sınırlar.
        /// </summary>
        /// <param name="normalizedDeltaX">Ekran genişliğine göre normalize edilmiş yatay delta.</param>
        private void HandleSwerveDelta(float normalizedDeltaX)
        {
            if (!_canMove || _runnerData == null) return;

            // Ekrandan gelen girdiyi yol genişliği ve hassasiyet ile çarparak hedef konuma ekle
            float roadWidth = _runnerData.MaxSwerveX * 2.0f;
            _targetX += normalizedDeltaX * _runnerData.SwerveSensitivity * roadWidth;

            // Sınırları aşmaması için X pozisyonunu kesin olarak kelepçele (Clamp)
            _targetX = Mathf.Clamp(_targetX, -_runnerData.MaxSwerveX, _runnerData.MaxSwerveX);
        }

        /// <summary>
        /// Mevcut X pozisyonunu hedef X'e Lerp ile pürüzsüzce yaklaştırır.
        /// </summary>
        private void ApplySmoothSwerve()
        {
            Vector3 currentPos = transform.position;

            // Hedefe doğru pürüzsüz yaklaşım
            float smoothX = Mathf.Lerp(
                currentPos.x,
                _targetX,
                _runnerData.SwerveSmoothSpeed * Time.deltaTime
            );

            // Garanti kelepçeleme
            smoothX = Mathf.Clamp(smoothX, -_runnerData.MaxSwerveX, _runnerData.MaxSwerveX);

            transform.position = new Vector3(smoothX, currentPos.y, currentPos.z);
        }

        /// <summary>
        /// Karakter sağa/sola kayarken viraja doğru dinamik eğilme (bank/lean) hissi verir.
        /// </summary>
        private void ApplyLeaningEffect()
        {
            Transform targetTransform = _visualModel != null ? _visualModel : transform;

            // Mevcut pozisyon ile hedef pozisyon arasındaki farka göre eğilme açısı belirle
            float deltaDistance = _targetX - transform.position.x;
            float targetAngleZ = -Mathf.Clamp(
                deltaDistance * 12.0f,
                -_runnerData.MaxLeanAngle,
                _runnerData.MaxLeanAngle
            );

            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngleZ);

            targetTransform.localRotation = Quaternion.Slerp(
                targetTransform.localRotation,
                targetRotation,
                _runnerData.LeanSmoothSpeed * Time.deltaTime
            );
        }

        #region Public Control API

        /// <summary>
        /// Karakterin hareketini durdurur veya yeniden başlatır (Örn: Bölüm bittiğinde veya Game Over durumunda).
        /// </summary>
        public void SetMovementState(bool canMove)
        {
            _canMove = canMove;
            if (!_canMove)
            {
                _inputHandler?.ResetInput();
                // Dönüşü pürüzsüzce sıfırla
                if (_visualModel != null)
                {
                    _visualModel.localRotation = Quaternion.identity;
                }
            }
        }

        /// <summary>
        /// Kapılardan veya güçlendirmelerden (buff/debuff) gelen hız değişimini ayarlar.
        /// </summary>
        public void SetForwardSpeed(float newSpeed)
        {
            if (_runnerData == null) return;
            _currentForwardSpeed = Mathf.Clamp(newSpeed, _runnerData.MinForwardSpeed, _runnerData.MaxForwardSpeed);
        }

        /// <summary>
        /// Karakterin hedef X pozisyonunu sıfırlar veya belirli bir noktaya ışınlar.
        /// </summary>
        public void ResetPosition(Vector3 newPosition)
        {
            transform.position = newPosition;
            _targetX = Mathf.Clamp(newPosition.x, -_runnerData.MaxSwerveX, _runnerData.MaxSwerveX);
        }

        #endregion

        #region Editor Gizmos
        private void OnDrawGizmosSelected()
        {
            if (_runnerData == null) return;

            // Scene ekranında yol sınırlarını görsel çizgi olarak göster
            Gizmos.color = Color.cyan;
            Vector3 currentPos = transform.position;

            Vector3 leftBoundaryStart = new Vector3(-_runnerData.MaxSwerveX, currentPos.y, currentPos.z - 5f);
            Vector3 leftBoundaryEnd = new Vector3(-_runnerData.MaxSwerveX, currentPos.y, currentPos.z + 25f);
            Gizmos.DrawLine(leftBoundaryStart, leftBoundaryEnd);

            Vector3 rightBoundaryStart = new Vector3(_runnerData.MaxSwerveX, currentPos.y, currentPos.z - 5f);
            Vector3 rightBoundaryEnd = new Vector3(_runnerData.MaxSwerveX, currentPos.y, currentPos.z + 25f);
            Gizmos.DrawLine(rightBoundaryStart, rightBoundaryEnd);

            // Hedef X konumunu gösteren küçük bir işaretçi
            Gizmos.color = Color.yellow;
            Vector3 targetIndicator = new Vector3(_targetX, currentPos.y + 0.1f, currentPos.z);
            Gizmos.DrawWireSphere(targetIndicator, 0.25f);
        }
        #endregion
    }
}
