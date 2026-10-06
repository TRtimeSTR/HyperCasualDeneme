using System.Collections.Generic;
using GateRunner.Collectibles;
using GateRunner.Gates;
using GateRunner.Pooling;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Level
{
    /// <summary>
    /// Seviyeyi oluşturan tekil yol modülü (segment).
    /// Kendi uzunluğunu bilir, kapı ve toplanabilir altın montaj yuvalarını (sockets) barındırır.
    /// </summary>
    [SelectionBase]
    public class RoadSegment : SerializedMonoBehaviour
    {
        [FoldoutGroup("Segment Boyutları")]
        [Tooltip("Bu yol parçasının Z eksenindeki uzunluğu (metre/birim).")]
        [SerializeField, MinValue(5f)] private float _length = 20.0f;

        [FoldoutGroup("Segment Montaj Yuvaları (Sockets)")]
        [Tooltip("Kapı çiftinin yerleştirileceği opsiyonel soket.")]
        [SerializeField] private Transform _gateSocket;

        [ShowInInspector, ReadOnly, FoldoutGroup("Bağlı Objeler")]
        public GatePair AttachedGatePair { get; private set; }

        private readonly List<Coin> _attachedCoins = new List<Coin>();

        public float Length => _length;
        public Transform GateSocket => _gateSocket != null ? _gateSocket : transform;

        /// <summary>
        /// Bu yol parçasına bir kapı çifti bağlar.
        /// </summary>
        public void AttachGatePair(GatePair gatePair)
        {
            AttachedGatePair = gatePair;
            if (gatePair != null)
            {
                gatePair.transform.SetParent(GateSocket);
                gatePair.transform.localPosition = Vector3.zero;
                gatePair.transform.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// Segment üzerine doğan bir altını bağlar.
        /// </summary>
        public void AttachCoin(Coin coin)
        {
            if (coin != null)
            {
                coin.transform.SetParent(transform);
                _attachedCoins.Add(coin);
            }
        }

        /// <summary>
        /// Segment havuza dönerken üzerinde kalan toplanmamış altınları havuza bırakır.
        /// </summary>
        public void ReleaseAttachedCoins()
        {
            for (int i = 0; i < _attachedCoins.Count; i++)
            {
                if (_attachedCoins[i] != null && _attachedCoins[i].gameObject.activeSelf)
                {
                    PoolManager.Instance?.ReleaseCoin(_attachedCoins[i]);
                }
            }
            _attachedCoins.Clear();
        }

        /// <summary>
        /// Segment havuza dönmeden önce üzerindeki bağlı objeleri çözer.
        /// </summary>
        public void DetachGatePair()
        {
            if (AttachedGatePair != null)
            {
                AttachedGatePair.transform.SetParent(null);
                AttachedGatePair = null;
            }
        }

        /// <summary>
        /// Havuzdan çekildiğinde segmenti sıfırlar.
        /// </summary>
        public void ResetSegment()
        {
            DetachGatePair();
            _attachedCoins.Clear();
        }

        #region Editor Gizmos
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
            Vector3 center = transform.position + Vector3.forward * (_length * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(8f, 0.2f, _length));
        }
        #endregion
    }
}
