using DG.Tweening;
using GateRunner.Managers;
using GateRunner.Player;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Level
{
    /// <summary>
    /// Seviyenin sonundaki bitiş çizgisi kemeri (Finish Line).
    /// Karakter bu çizgiyi geçtiğinde zafer durumu tetiklenir, konfeti patlatılır ve bölüm tamamlanır.
    /// </summary>
    [SelectionBase]
    public class FinishLine : SerializedMonoBehaviour
    {
        [FoldoutGroup("Efekt & Referanslar")]
        [Tooltip("Bitiş çizgisi geçildiğinde patlatılacak konfeti / zafer parçacık efekti.")]
        [SerializeField] private ParticleSystem _confettiVfx;

        [FoldoutGroup("Efekt & Referanslar")]
        [Tooltip("Kemerin üzerinde sallanacak zafer flaması veya tabelası.")]
        [SerializeField] private Transform _victoryBanner;

        [SerializeField] private Collider _triggerCollider;

        private bool _isTriggered = false;

        private void Awake()
        {
            if (_triggerCollider == null)
            {
                _triggerCollider = GetComponent<Collider>();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isTriggered) return;

            var player = other.GetComponent<PlayerModifier>() ?? other.GetComponentInParent<PlayerModifier>();
            if (player != null && player.IsAlive)
            {
                _isTriggered = true;
                if (_triggerCollider != null) _triggerCollider.enabled = false;

                // Oyuncu kutlaması
                player.CelebrateVictory();

                // Konfeti parçacığı
                if (_confettiVfx != null)
                {
                    _confettiVfx.Play();
                }

                // Bayrak sallama animasyonu
                if (_victoryBanner != null)
                {
                    _victoryBanner.DOPunchScale(Vector3.one * 0.35f, 0.6f, 6, 0.5f);
                }

                // GameManager ile seviye tamamlama
                GameManager.Instance?.LevelComplete();
            }
        }

        public void ResetFinishLine()
        {
            _isTriggered = false;
            if (_triggerCollider != null) _triggerCollider.enabled = true;
            if (_confettiVfx != null) _confettiVfx.Stop();
        }
    }
}
