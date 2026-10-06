using GateRunner.Collectibles;
using GateRunner.Gates;
using GateRunner.Level;
using GateRunner.Obstacles;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Pool;

namespace GateRunner.Pooling
{
    /// <summary>
    /// UnityEngine.Pool API'sini kullanarak RoadSegment, GatePair, Coin ve Obstacle objelerinin
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

        [FoldoutGroup("Prefab Tanımları")]
        [Required("Coin prefab'i atanmalıdır.")]
        [AssetsOnly]
        [SerializeField] private Coin _coinPrefab;

        [FoldoutGroup("Prefab Tanımları")]
        [AssetsOnly]
        [SerializeField] private Obstacle _obstaclePrefab;

        [FoldoutGroup("Havuz Kapasite Ayarları")]
        [MinValue(5), MaxValue(50)]
        [SerializeField] private int _defaultCapacity = 15;

        [FoldoutGroup("Havuz Kapasite Ayarları")]
        [MinValue(10), MaxValue(200)]
        [SerializeField] private int _maxPoolSize = 80;

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

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Havuz İstatistikleri")]
        public int CoinActiveCount => _coinPool?.CountActive ?? 0;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Havuz İstatistikleri")]
        public int CoinInactiveCount => _coinPool?.CountInactive ?? 0;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Havuz İstatistikleri")]
        public int ObstacleActiveCount => _obstaclePool?.CountActive ?? 0;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Havuz İstatistikleri")]
        public int ObstacleInactiveCount => _obstaclePool?.CountInactive ?? 0;
        #endregion

        [System.NonSerialized] private ObjectPool<RoadSegment> _roadPool;
        [System.NonSerialized] private ObjectPool<GatePair> _gatePool;
        [System.NonSerialized] private ObjectPool<Coin> _coinPool;
        [System.NonSerialized] private ObjectPool<Obstacle> _obstaclePool;

        [System.NonSerialized] private Transform _roadContainer;
        [System.NonSerialized] private Transform _gateContainer;
        [System.NonSerialized] private Transform _coinContainer;
        [System.NonSerialized] private Transform _obstacleContainer;

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

            _coinContainer = new GameObject("[Pool_Coins]").transform;
            _coinContainer.SetParent(transform);

            _obstacleContainer = new GameObject("[Pool_Obstacles]").transform;
            _obstacleContainer.SetParent(transform);
        }

        private void InitializePools()
        {
            _roadPool = new ObjectPool<RoadSegment>(
                createFunc: CreateRoadSegment,
                actionOnGet: OnGetRoadSegment,
                actionOnRelease: OnReleaseRoadSegment,
                actionOnDestroy: OnDestroyPoolItem,
                collectionCheck: _collectionCheck,
                defaultCapacity: _defaultCapacity,
                maxSize: _maxPoolSize
            );

            _gatePool = new ObjectPool<GatePair>(
                createFunc: CreateGatePair,
                actionOnGet: OnGetGatePair,
                actionOnRelease: OnReleaseGatePair,
                actionOnDestroy: OnDestroyPoolItem,
                collectionCheck: _collectionCheck,
                defaultCapacity: _defaultCapacity,
                maxSize: _maxPoolSize
            );

            if (_coinPrefab != null)
            {
                _coinPool = new ObjectPool<Coin>(
                    createFunc: CreateCoin,
                    actionOnGet: OnGetCoin,
                    actionOnRelease: OnReleaseCoin,
                    actionOnDestroy: OnDestroyPoolItem,
                    collectionCheck: _collectionCheck,
                    defaultCapacity: _defaultCapacity * 2,
                    maxSize: _maxPoolSize * 2
                );
            }

            if (_obstaclePrefab != null)
            {
                _obstaclePool = new ObjectPool<Obstacle>(
                    createFunc: CreateObstacle,
                    actionOnGet: OnGetObstacle,
                    actionOnRelease: OnReleaseObstacle,
                    actionOnDestroy: OnDestroyPoolItem,
                    collectionCheck: _collectionCheck,
                    defaultCapacity: _defaultCapacity,
                    maxSize: _maxPoolSize
                );
            }
        }

        #region RoadSegment Callbacks
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
            if (segment.AttachedGatePair != null)
            {
                ReleaseGatePair(segment.AttachedGatePair);
                segment.DetachGatePair();
            }

            segment.ReleaseAttachedCoins();
            segment.ReleaseAttachedObstacles();

            segment.gameObject.SetActive(false);
            segment.transform.SetParent(_roadContainer);
        }
        #endregion

        #region GatePair Callbacks
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
        #endregion

        #region Coin Callbacks
        private Coin CreateCoin()
        {
            Coin coin = Instantiate(_coinPrefab, _coinContainer);
            coin.gameObject.SetActive(false);
            return coin;
        }

        private void OnGetCoin(Coin coin)
        {
            coin.ResetCoin();
        }

        private void OnReleaseCoin(Coin coin)
        {
            coin.gameObject.SetActive(false);
            coin.transform.SetParent(_coinContainer);
        }
        #endregion

        #region Obstacle Callbacks
        private Obstacle CreateObstacle()
        {
            Obstacle obs = Instantiate(_obstaclePrefab, _obstacleContainer);
            obs.gameObject.SetActive(false);
            return obs;
        }

        private void OnGetObstacle(Obstacle obs)
        {
            obs.ResetObstacle();
        }

        private void OnReleaseObstacle(Obstacle obs)
        {
            obs.gameObject.SetActive(false);
            obs.transform.SetParent(_obstacleContainer);
        }
        #endregion

        private void OnDestroyPoolItem(Component item)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }

        #region Public API
        public RoadSegment GetRoadSegment(Vector3 position, Quaternion rotation)
        {
            if (_roadPool == null) InitializePools();
            RoadSegment segment = _roadPool.Get();
            segment.transform.position = position;
            segment.transform.rotation = rotation;
            return segment;
        }

        public void ReleaseRoadSegment(RoadSegment segment)
        {
            if (segment == null) return;
            _roadPool.Release(segment);
        }

        public GatePair GetGatePair(Vector3 position, Quaternion rotation)
        {
            if (_gatePool == null) InitializePools();
            GatePair pair = _gatePool.Get();
            pair.transform.position = position;
            pair.transform.rotation = rotation;
            return pair;
        }

        public void ReleaseGatePair(GatePair pair)
        {
            if (pair == null) return;
            _gatePool.Release(pair);
        }

        public Coin GetCoin(Vector3 position, Quaternion rotation)
        {
            if (_coinPool == null) InitializePools();
            if (_coinPool == null) return null;

            Coin coin = _coinPool.Get();
            coin.transform.position = position;
            coin.transform.rotation = rotation;
            return coin;
        }

        public void ReleaseCoin(Coin coin)
        {
            if (coin == null || _coinPool == null) return;
            _coinPool.Release(coin);
        }

        public Obstacle GetObstacle(Vector3 position, Quaternion rotation)
        {
            if (_obstaclePool == null) InitializePools();
            if (_obstaclePool == null) return null;

            Obstacle obs = _obstaclePool.Get();
            obs.transform.position = position;
            obs.transform.rotation = rotation;
            return obs;
        }

        public void ReleaseObstacle(Obstacle obs)
        {
            if (obs == null || _obstaclePool == null) return;
            _obstaclePool.Release(obs);
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
            _coinPool?.Clear();
            _obstaclePool?.Clear();
        }
        #endregion
    }
}
