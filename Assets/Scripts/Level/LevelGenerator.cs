using System.Collections.Generic;
using GateRunner.Collectibles;
using GateRunner.Data;
using GateRunner.Gates;
using GateRunner.Movement;
using GateRunner.Obstacles;
using GateRunner.Pooling;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Level
{
    /// <summary>
    /// Karakter ilerledikçe dinamik olarak yeni yol parçaları, matematiksel kapılar,
    /// toplanabilir altınlar ve tehlikeli dikenler üreten seviye üreticisi.
    /// Belirlenen segment sayısına ulaşıldığında bitiş çizgisini (FinishLine) doğurur.
    /// </summary>
    public class LevelGenerator : SerializedMonoBehaviour
    {
        [TabGroup("GeneratorTabs", "Genel Ayarlar", SdfIconType.Gear)]
        [Tooltip("Takip edilecek oyuncu transform'u. Boş bırakılırsa sahnedeki SwerveMovement otomatik bulunur.")]
        [SerializeField] private Transform _playerTransform;

        [TabGroup("GeneratorTabs", "Genel Ayarlar")]
        [Tooltip("Oyun başında oyuncunun önüne kaç adet başlangıç yol parçası yerleştirilsin?")]
        [SerializeField, MinValue(3), MaxValue(20)] private int _initialSegmentCount = 6;

        [TabGroup("GeneratorTabs", "Genel Ayarlar")]
        [Tooltip("Oyuncunun ne kadar ilerisine kadar yol üretilmeye devam edilsin (metre)?")]
        [SerializeField, MinValue(20f)] private float _forwardSpawnDistance = 70.0f;

        [TabGroup("GeneratorTabs", "Genel Ayarlar")]
        [Tooltip("Oyuncunun ne kadar arkasında kalan yol parçaları havuza geri gönderilsin (metre)?")]
        [SerializeField, MinValue(10f)] private float _recycleDistanceBehind = 25.0f;

        [TabGroup("GeneratorTabs", "Bölüm & Bitiş (Finish)", SdfIconType.FlagFill)]
        [Tooltip("Sonsuz mod aktifse bitiş çizgisi çıkmaz, yol sürekli üretilir.")]
        [SerializeField] private bool _isEndlessMode = false;

        [TabGroup("GeneratorTabs", "Bölüm & Bitiş (Finish)")]
        [Tooltip("Seviyenin toplam kaç yol segmentinden oluşacağı.")]
        [SerializeField, MinValue(6), MaxValue(50), HideIf(nameof(_isEndlessMode))]
        private int _totalLevelSegments = 16;

        [TabGroup("GeneratorTabs", "Bölüm & Bitiş (Finish)")]
        [Tooltip("Bölüm sonunda doğurulacak bitiş çizgisi prefab'i.")]
        [SerializeField, HideIf(nameof(_isEndlessMode))]
        private FinishLine _finishLinePrefab;

        [TabGroup("GeneratorTabs", "Kapı Ayarları (Gates)", SdfIconType.DoorOpen)]
        [Tooltip("İlk kapı çifti kaçıncı yol parçasından itibaren çıkmaya başlasın?")]
        [SerializeField, MinValue(1)] private int _firstGateSegmentIndex = 2;

        [TabGroup("GeneratorTabs", "Kapı Ayarları (Gates)")]
        [Tooltip("Kaç yol parçasında bir kapı çifti doğurulsun? (Örn: 2 = her 2 parçada bir)")]
        [SerializeField, MinValue(1)] private int _gateSegmentInterval = 2;

        #region Live Stats (Odin)
        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı İstatistikler")]
        public int ActiveSegmentCount => _activeSegments.Count;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı İstatistikler")]
        public float NextSpawnZ => _nextSpawnZ;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı İstatistikler")]
        public int TotalSegmentsSpawned => _totalSegmentsSpawned;
        #endregion

        private readonly Queue<RoadSegment> _activeSegments = new Queue<RoadSegment>();
        private float _nextSpawnZ = 0.0f;
        private int _totalSegmentsSpawned = 0;
        private bool _finishSpawned = false;

        private void Start()
        {
            if (_playerTransform == null)
            {
                var player = FindAnyObjectByType<SwerveMovement>();
                if (player != null)
                {
                    _playerTransform = player.transform;
                }
            }

            GenerateInitialTrack();
        }

        private void Update()
        {
            if (_playerTransform == null) return;

            // Sonsuz değilse ve bitiş doğurulduysa ve yeterli yol varsa daha fazla üretme
            if (!_isEndlessMode && _finishSpawned && _nextSpawnZ > _playerTransform.position.z + _forwardSpawnDistance)
            {
                RecycleOldSegments();
                return;
            }

            // Oyuncunun önünü sürekli dolu tut
            while (_nextSpawnZ < _playerTransform.position.z + _forwardSpawnDistance)
            {
                if (!_isEndlessMode && _totalSegmentsSpawned >= _totalLevelSegments + 2)
                {
                    break;
                }
                SpawnNextSegment();
            }

            // Oyuncunun arkasında kalan eski parçaları havuza geri gönder
            RecycleOldSegments();
        }

        private void GenerateInitialTrack()
        {
            _nextSpawnZ = 0.0f;
            _totalSegmentsSpawned = 0;
            _finishSpawned = false;

            for (int i = 0; i < _initialSegmentCount; i++)
            {
                SpawnNextSegment();
            }
        }

        private void SpawnNextSegment()
        {
            if (PoolManager.Instance == null)
            {
                Debug.LogWarning($"[{nameof(LevelGenerator)}] PoolManager sahnede bulunamadı!", this);
                return;
            }

            Vector3 spawnPos = new Vector3(0f, 0f, _nextSpawnZ);
            RoadSegment segment = PoolManager.Instance.GetRoadSegment(spawnPos, Quaternion.identity);

            _totalSegmentsSpawned++;

            // Bitiş çizgisi kontrolü
            if (!_isEndlessMode && _totalSegmentsSpawned == _totalLevelSegments)
            {
                SpawnFinishLineOnSegment(segment);
            }
            else if (ShouldSpawnGateOnSegment(_totalSegmentsSpawned))
            {
                SpawnGatePairOnSegment(segment);
            }
            else if (_totalSegmentsSpawned > 1 && (!_finishSpawned || _totalSegmentsSpawned < _totalLevelSegments))
            {
                // Engel ve altın yerleşimi: Oyuncuyu yönlendiren klasik hypercasual düzen
                SpawnObstaclesAndCoinsOnSegment(segment);
            }

            _activeSegments.Enqueue(segment);
            _nextSpawnZ += segment.Length;
        }

        private void SpawnFinishLineOnSegment(RoadSegment segment)
        {
            _finishSpawned = true;
            if (_finishLinePrefab != null)
            {
                Vector3 finishPos = segment.transform.position + new Vector3(0f, 0f, segment.Length * 0.5f);
                FinishLine finishInstance = Instantiate(_finishLinePrefab, finishPos, Quaternion.identity, segment.transform);
                finishInstance.ResetFinishLine();
            }
        }

        private void SpawnObstaclesAndCoinsOnSegment(RoadSegment segment)
        {
            if (PoolManager.Instance == null) return;

            float[] lanes = { -2.2f, 0f, 2.2f };
            int spikeLaneIndex = Random.Range(0, lanes.Length);
            float spikeLaneX = lanes[spikeLaneIndex];

            // 1. Diken / Engel yerleştir (1-2 adet)
            float segmentStartZ = segment.transform.position.z;
            Obstacle obs = PoolManager.Instance.GetObstacle(new Vector3(spikeLaneX, 0f, segmentStartZ + 8f), Quaternion.identity);
            if (obs != null)
            {
                segment.AttachObstacle(obs);
            }

            // 2. Güvenli şeritlerden birine altın dizisi yerleştir (Ödül & Yönlendirme)
            int coinLaneIndex = (spikeLaneIndex + Random.Range(1, 3)) % lanes.Length;
            float coinLaneX = lanes[coinLaneIndex];

            int coinCount = 3;
            float startZ = segmentStartZ + 4.0f;
            float spacing = 3.0f;

            for (int i = 0; i < coinCount; i++)
            {
                Vector3 coinPos = new Vector3(coinLaneX, 0.9f, startZ + (i * spacing));
                Coin coin = PoolManager.Instance.GetCoin(coinPos, Quaternion.identity);
                if (coin != null)
                {
                    segment.AttachCoin(coin);
                }
            }
        }

        private bool ShouldSpawnGateOnSegment(int segmentIndex)
        {
            if (segmentIndex < _firstGateSegmentIndex) return false;
            if (!_isEndlessMode && segmentIndex >= _totalLevelSegments - 1) return false;
            return (segmentIndex - _firstGateSegmentIndex) % _gateSegmentInterval == 0;
        }

        private void SpawnGatePairOnSegment(RoadSegment segment)
        {
            GatePair gatePair = PoolManager.Instance.GetGatePair(Vector3.zero, Quaternion.identity);

            GenerateBalancedGatePairData(out GateData leftData, out GateData rightData);
            gatePair.Configure(leftData, rightData);

            segment.AttachGatePair(gatePair);
        }

        private void GenerateBalancedGatePairData(out GateData leftData, out GateData rightData)
        {
            int scenario = Random.Range(0, 3);

            switch (scenario)
            {
                case 0:
                    leftData = new GateData(GateOperationType.Add, Random.Range(10, 35));
                    rightData = new GateData(GateOperationType.Multiply, Random.Range(2, 4));
                    break;

                case 1:
                    leftData = new GateData(GateOperationType.Add, Random.Range(15, 30));
                    rightData = new GateData(GateOperationType.Subtract, Random.Range(5, 15));
                    break;

                case 2:
                default:
                    leftData = new GateData(GateOperationType.Multiply, Random.Range(2, 3));
                    rightData = new GateData(GateOperationType.Divide, 2);
                    break;
            }
        }

        private void RecycleOldSegments()
        {
            if (_playerTransform == null || PoolManager.Instance == null) return;

            while (_activeSegments.Count > 0)
            {
                RoadSegment oldestSegment = _activeSegments.Peek();
                float segmentEndZ = oldestSegment.transform.position.z + oldestSegment.Length;

                if (segmentEndZ < _playerTransform.position.z - _recycleDistanceBehind)
                {
                    _activeSegments.Dequeue();
                    PoolManager.Instance.ReleaseRoadSegment(oldestSegment);
                }
                else
                {
                    break;
                }
            }
        }

        #region Odin Inspector Test
        [Button("Seviyeyi Sıfırla (Reset Track)", ButtonSizes.Small), TabGroup("GeneratorTabs", "Genel Ayarlar")]
        public void ResetTrack()
        {
            if (PoolManager.Instance == null) return;

            while (_activeSegments.Count > 0)
            {
                RoadSegment segment = _activeSegments.Dequeue();
                PoolManager.Instance.ReleaseRoadSegment(segment);
            }

            GenerateInitialTrack();
        }
        #endregion
    }
}
