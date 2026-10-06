using GateRunner.Gates;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Level
{
    /// <summary>
    /// Seviyeyi oluşturan tekil yol modülü (segment).
    /// Kendi uzunluğunu bilir, kapı ve engel montaj yuvalarını (sockets) barındırır.
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
