using GateRunner.Movement;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Camera
{
    /// <summary>
    /// Hypercasual runner dinamiklerine özel pürüzsüz kamera takip sistemi.
    /// Z ekseninde oyuncuyu sabit mesafeyle takip ederken, X ekseninde (swerve)
    /// aşırı sarsılmaları engelleyen yumuşak sönümleme (damping) uygular.
    /// </summary>
    public class RunnerCamera : SerializedMonoBehaviour
    {
        [Title("Takip Hedefi & Mesafe", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Takip edilecek oyuncu Transform'u. Boşsa sahneden otomatik bulunur.")]
        [SerializeField] private Transform _target;

        [Tooltip("Kameranın oyuncuya göre konumsal mesafesi (X, Y, Z).")]
        [SerializeField] private Vector3 _offset = new Vector3(0f, 6.5f, -8.0f);

        [FoldoutGroup("Kamera Yumuşatma & Açı")]
        [Tooltip("İleri takip pürüzsüzlük hızı.")]
        [SerializeField, Range(2f, 30f)] private float _forwardFollowSpeed = 16.0f;

        [FoldoutGroup("Kamera Yumuşatma & Açı")]
        [Tooltip("Yatay (swerve) hareketlerde kameranın gecikme/sönümleme hızı. Düşük = daha sakin kamera.")]
        [SerializeField, Range(1f, 20f)] private float _horizontalFollowSpeed = 6.0f;

        [FoldoutGroup("Kamera Yumuşatma & Açı")]
        [Tooltip("Kameranın aşağıya bakış açısı (derece).")]
        [SerializeField, Range(5f, 45f)] private float _pitchAngle = 22.0f;

        private float _currentX;

        private void Start()
        {
            if (_target == null)
            {
                var player = FindAnyObjectByType<SwerveMovement>();
                if (player != null)
                {
                    _target = player.transform;
                }
            }

            if (_target != null)
            {
                _currentX = _target.position.x;
                SnapToTarget();
            }
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // Yatay eksende (X) yumuşak sönümleme
            _currentX = Mathf.Lerp(_currentX, _target.position.x, _horizontalFollowSpeed * Time.deltaTime);

            // Hedef pozisyonu hesapla
            Vector3 targetPosition = new Vector3(
                _currentX + _offset.x,
                _target.position.y + _offset.y,
                _target.position.z + _offset.z
            );

            // Z ve Y eksenlerinde pürüzsüz yaklaş
            transform.position = Vector3.Lerp(transform.position, targetPosition, _forwardFollowSpeed * Time.deltaTime);

            // Sabit dinamik bakış açısı
            transform.rotation = Quaternion.Euler(_pitchAngle, 0f, 0f);
        }

        public void SetTarget(Transform newTarget)
        {
            _target = newTarget;
            SnapToTarget();
        }

        private void SnapToTarget()
        {
            if (_target == null) return;
            _currentX = _target.position.x;
            transform.position = _target.position + _offset;
            transform.rotation = Quaternion.Euler(_pitchAngle, 0f, 0f);
        }
    }
}
