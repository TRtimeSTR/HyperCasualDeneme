using DG.Tweening;
using GateRunner.Audio;
using GateRunner.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Obstacles
{
    /// <summary>
    /// Yol üzerindeki tehlikeli engeller (Diken/Spike vb.).
    /// Karakter çarptığında puan ve boyut kaybettirir, sarsılma ve parçacık efekti üretir.
    /// </summary>
    [SelectionBase]
    public class Obstacle : SerializedMonoBehaviour
    {
        [FoldoutGroup("Engel Parametreleri")]
        [Tooltip("Karaktere çarptığında düşürülecek skor/boyut hasarı.")]
        [SerializeField, MinValue(1)] private int _damage = 8;

        [FoldoutGroup("Engel Parametreleri")]
        [Tooltip("Çarpışma anında oynatılacak opsiyonel parçacık efekti (VFX).")]
        [SerializeField] private ParticleSystem _hitVfx;

        [FoldoutGroup("Animasyon")]
        [Tooltip("Çarpma anında engelin sarsılma şiddeti.")]
        [SerializeField] private float _shakeIntensity = 0.3f;

        [SerializeField] private Collider _triggerCollider;

        private bool _hasTriggered = false;
        private Vector3 _initialLocalScale = Vector3.one;

        public int Damage => _damage;

        private void Awake()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<Collider>();
            }
            _initialLocalScale = transform.localScale;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_hasTriggered) return;

            var playerModifier = other.GetComponent<PlayerModifier>() ?? other.GetComponentInParent<PlayerModifier>();
            if (playerModifier != null)
            {
                _hasTriggered = true;
                if (_triggerCollider != null) _triggerCollider.enabled = false;

                // Karakteri küçült / hasar ver
                playerModifier.TakeDamage(_damage);

                // Ses ve görsel geri bildirim
                AudioManager.Instance?.PlayDamageSound();
                PlayHitFeedback();
            }
        }

        private void PlayHitFeedback()
        {
            if (_hitVfx != null)
            {
                _hitVfx.transform.position = transform.position + Vector3.up * 0.5f;
                _hitVfx.Play();
            }

            // Engelin kendisini anlık sars ve hafif küçült
            transform.DOKill();
            transform.DOShakeScale(0.3f, _shakeIntensity, 10, 90f);
        }

        /// <summary>
        /// Havuzdan tekrar çekildiğinde engeli sıfırlar.
        /// </summary>
        public void ResetObstacle()
        {
            _hasTriggered = false;
            transform.DOKill();
            transform.localScale = _initialLocalScale;

            if (_triggerCollider != null)
            {
                _triggerCollider.enabled = true;
            }
            gameObject.SetActive(true);
        }

        private void OnDestroy()
        {
            transform.DOKill();
        }
    }
}
