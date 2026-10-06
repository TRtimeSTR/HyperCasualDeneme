using GateRunner.Gates;
using GateRunner.Level;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Pool;

namespace GateRunner.Pooling
{
    /// <summary>
    /// UnityEngine.Pool API'sini kullanarak RoadSegment ve GatePair objelerinin
    /// yüksek performanslı bellek havuzlamasını (Object Pooling) yöneten merkezi sınıf.
    /// Odin Inspector ile canlı havuz durumunu ve yönetim araçlarını sunar.
    /// </summary>
    public class PoolManager : SerializedMonoBehaviour
    {
        public static PoolManager Instance { get; private set; }

        [FoldoutGroup("Prefab Tanımları")]
        [Required("RoadSegment prefab'i atanmalıdır.")]
        [AssetsOnly]
        [SerializeField] private RoadSegment _roadSegmentPrefab;

        [FoldoutGroup("Prefab Tanımları")]
        [Required("GatePair prefab'i atanmalıdır.")]
        [AssetsOnly]
        [SerializeField] private GatePair _gatePairPrefab;

        [FoldoutGroup("Havuz Kapasite Ayarları")]
        [MinValue(5), MaxValue(50)]
        [SerializeField] private int _defaultCapacity = 12;

        [FoldoutGroup("Havuz Kapasite Ayarları")]
        [MinValue(10), MaxValue(200)]
        [SerializeField] private int _maxPoolSize = 60;

        [FoldoutGroup("Havuz Kapasite Ayarları")]
        [Tooltip("Aynı objenin iki kez havuza bırakılmasını denetleyen güvenlik kontrolü (Collection Check).")]
        [SerializeField] private bool _collectionCheck = true;

        #region Live Pool Statistics (Odin Inspector)
        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Havuz İstatistikleri")]
        public int RoadActiveCount => _roadPool?.CountActive ?? 0;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Havuz İstatistikleri")]
        public int RoadInactiveCount => _roadPool?.CountInactive ?? 0;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Havuz İstatistikleri")]
        public int GateActiveCount => _gatePool?.CountActive ?? 0;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Havuz İstatistikleri")]
        public int GateInactiveCount => _gatePool?.CountInactive ?? 0;
        #endregion

        private ObjectPool<RoadSegment> _roadPool;
        private ObjectPool<GatePair> _gatePool;

        private Transform _roadContainer;
        private Transform _gateContainer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeHierarchyContainers();
            InitializePools();
        }

        private void InitializeHierarchyContainers()
        {
            _roadContainer = new GameObject("[Pool_RoadSegments]").transform;
            _roadContainer.SetParent(transform);

            _gateContainer = new GameObject("[Pool_GatePairs]").transform;
            _gateContainer.SetParent(transform);
        }

        private void InitializePools()
        {
            // UnityEngine.Pool.ObjectPool kurulumu (RoadSegment)
            _roadPool = new ObjectPool<RoadSegment>(
                createFunc: CreateRoadSegment,
                actionOnGet: OnGetRoadSegment,
                actionOnRelease: OnReleaseRoadSegment,
                actionOnDestroy: OnDestroyPoolItem,
                collectionCheck: _collectionCheck,
                defaultCapacity: _defaultCapacity,
                maxSize: _maxPoolSize
            );

            // UnityEngine.Pool.ObjectPool kurulumu (GatePair)
            _gatePool = new ObjectPool<GatePair>(
                createFunc: CreateGatePair,
                actionOnGet: OnGetGatePair,
                actionOnRelease: OnReleaseGatePair,
                actionOnDestroy: OnDestroyPoolItem,
                collectionCheck: _collectionCheck,
                defaultCapacity: _defaultCapacity,
                maxSize: _maxPoolSize
            );
        }

        #region RoadSegment Pool Callbacks
        private RoadSegment CreateRoadSegment()
        {
            RoadSegment segment = Instantiate(_roadSegmentPrefab, _roadContainer);
            segment.gameObject.SetActive(false);
            return segment;
        }

        private void OnGetRoadSegment(RoadSegment segment)
        {
            segment.ResetSegment();
            segment.gameObject.SetActive(true);
        }

        private void OnReleaseRoadSegment(RoadSegment segment)
        {
            // Eğer segment üzerinde hala bağlı bir kapı çifti varsa önce kapıyı havuza döndür
            if (segment.AttachedGatePair != null)
            {
                ReleaseGatePair(segment.AttachedGatePair);
                segment.DetachGatePair();
            }

            segment.gameObject.SetActive(false);
            segment.transform.SetParent(_roadContainer);
        }
        #endregion

        #region GatePair Pool Callbacks
        private GatePair CreateGatePair()
        {
            GatePair pair = Instantiate(_gatePairPrefab, _gateContainer);
            pair.gameObject.SetActive(false);
            return pair;
        }

        private void OnGetGatePair(GatePair pair)
        {
            pair.ResetPair();
            pair.gameObject.SetActive(true);
        }

        private void OnReleaseGatePair(GatePair pair)
        {
            pair.ResetPair();
            pair.gameObject.SetActive(false);
            pair.transform.SetParent(_gateContainer);
        }

        private void OnDestroyPoolItem(Component item)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        #endregion

        #region Public API
        /// <summary>
        /// Havuzdan bir yol parçası talep eder.
        /// </summary>
        public RoadSegment GetRoadSegment(Vector3 position, Quaternion rotation)
        {
            if (_roadPool == null) InitializePools();

            RoadSegment segment = _roadPool.Get();
            segment.transform.position = position;
            segment.transform.rotation = rotation;
            return segment;
        }

        /// <summary>
        /// Kullanımı biten yol parçasını havuza iade eder.
        /// </summary>
        public void ReleaseRoadSegment(RoadSegment segment)
        {
            if (segment == null) return;
            _roadPool.Release(segment);
        }

        /// <summary>
        /// Havuzdan bir kapı çifti talep eder.
        /// </summary>
        public GatePair GetGatePair(Vector3 position, Quaternion rotation)
        {
            if (_gatePool == null) InitializePools();

            GatePair pair = _gatePool.Get();
            pair.transform.position = position;
            pair.transform.rotation = rotation;
            return pair;
        }

        /// <summary>
        /// Kullanımı biten kapı çiftini havuza iade eder.
        /// </summary>
        public void ReleaseGatePair(GatePair pair)
        {
            if (pair == null) return;
            _gatePool.Release(pair);
        }
        #endregion

        #region Odin Inspector Tools
        [Button("Havuzları Ön Isıt (Prewarm)", ButtonSizes.Medium), FoldoutGroup("Havuz Yönetimi")]
        public void PrewarmPools()
        {
            if (!Application.isPlaying) return;

            var tempRoads = new RoadSegment[_defaultCapacity];
            var tempGates = new GatePair[_defaultCapacity];

            for (int i = 0; i < _defaultCapacity; i++)
            {
                tempRoads[i] = _roadPool.Get();
                tempGates[i] = _gatePool.Get();
            }

            for (int i = 0; i < _defaultCapacity; i++)
            {
                _roadPool.Release(tempRoads[i]);
                _gatePool.Release(tempGates[i]);
            }
        }

        [Button("Havuzları Boşalt (Clear)", ButtonSizes.Medium), FoldoutGroup("Havuz Yönetimi")]
        public void ClearPools()
        {
            _roadPool?.Clear();
            _gatePool?.Clear();
        }
        #endregion
    }
}
