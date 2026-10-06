using DG.Tweening;
using GateRunner.Managers;
using GateRunner.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Collectibles
{
    /// <summary>
    /// Yol üzerinde toplanabilen altın/puan objesi.
    /// Kendi ekseninde döner, hafif salınır ve toplandığında DOTween ile küçülerek havuza döner.
    /// </summary>
    [SelectionBase]
    public class Coin : SerializedMonoBehaviour
    {
        [FoldoutGroup("Değer & Puan")]
        [Tooltip("Toplandığında oyuncuya verilecek altın/skor miktarı.")]
        [SerializeField, MinValue(1)] private int _pointValue = 1;

        [FoldoutGroup("Görsel Animasyonlar")]
        [Tooltip("Kendi ekseninde saniyedeki dönüş hızı.")]
        [SerializeField] private float _rotationSpeed = 160.0f;

        [FoldoutGroup("Görsel Animasyonlar")]
        [Tooltip("Aşağı-yukarı süzülme (bobbing) yüksekliği.")]
        [SerializeField] private float _bobHeight = 0.15f;

        [FoldoutGroup("Görsel Animasyonlar")]
        [Tooltip("Aşağı-yukarı süzülme hızı.")]
        [SerializeField] private float _bobSpeed = 3.5f;

        [SerializeField] private Collider _triggerCollider;

        private Vector3 _startLocalPos;
        private bool _isCollected = false;

        private void Awake()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<Collider>();
            }
            _startLocalPos = transform.localPosition;
        }

        private void Update()
        {
            if (_isCollected) return;

            // Kendi Y ekseni etrafında dön
            transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.World);

            // Hafif süzülme (Bobbing efekti)
            float newY = _startLocalPos.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
            transform.localPosition = new Vector3(transform.localPosition.x, newY, transform.localPosition.z);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

            // Oyuncu kontrolü
            if (other.TryGetComponent<PlayerModifier>(out _) || other.GetComponentInParent<PlayerModifier>() != null)
            {
                Collect();
            }
        }

        /// <summary>
        /// Altın toplandığında çalışır: Skoru artırır, DOTween efekti oynatır ve havuza iade eder.
        /// </summary>
        public void Collect()
        {
            _isCollected = true;
            if (_triggerCollider != null) _triggerCollider.enabled = false;

            // Skor Yöneticisine bildir
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddCoin(_pointValue);
            }

            // DOTween toplama efekti: Hafif yukarı zıplayıp küçülerek yok olma
            transform.DOKill();
            transform.DOMoveY(transform.position.y + 0.8f, 0.22f).SetEase(Ease.OutQuad);
            transform.DOScale(Vector3.zero, 0.22f).SetEase(Ease.InBack).OnComplete(() =>
            {
                // PoolManager'a iade et
                if (Pooling.PoolManager.Instance != null)
                {
                    Pooling.PoolManager.Instance.ReleaseCoin(this);
                }
                else
                {
                    gameObject.SetActive(false);
                }
            });
        }

        /// <summary>
        /// Havuzdan yeniden çekildiğinde objeyi sıfırlar.
        /// </summary>
        public void ResetCoin()
        {
            _isCollected = false;
            transform.DOKill();
            transform.localScale = Vector3.one;
            _startLocalPos = transform.localPosition;

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
