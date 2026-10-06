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
    /// Yol parçalarının içereceği içerik kalıpları (Pattern).
    /// </summary>
    public enum SegmentPatternType
    {
        [InspectorName("Boş Güvenli Segment (Empty Safe)")]
        EmptyStart,

        [InspectorName("Seçim Kapısı (Gate Choice: 1 Buff, 1 Debuff)")]
        GateChoice,

        [InspectorName("Engel Tuzağı (Obstacle Hazard: Spikes)")]
        ObstacleHazard,

        [InspectorName("Ödül Şeridi (Coin Reward: Coin Line)")]
        CoinReward,

        [InspectorName("Karma Kalıp (Mixed: Obstacles + Coins)")]
        MixedHazardReward
    }

    /// <summary>
    /// Karakter ilerledikçe dinamik olarak yeni yol parçaları, matematiksel kapılar,
    /// toplanabilir altınlar ve tehlikeli dikenler üreten seviye üreticisi.
    /// Belirlenen segment sayısına ulaşıldığında bitiş çizgisini (FinishLine) doğurur.
    /// Dinamik kalıp (Pattern) sistemi ve seviye bazlı zorluk ölçeklendirmesi (Difficulty Scaling) içerir.
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

        [TabGroup("GeneratorTabs", "Kalıp Dizilimi (Patterns)", SdfIconType.Grid3x3GapFill)]
        [Tooltip("Yol parçalarının takip edeceği dinamik pattern sırası.")]
        [SerializeField]
        private List<SegmentPatternType> _patternSequence = new List<SegmentPatternType>
        {
            SegmentPatternType.GateChoice,
            SegmentPatternType.CoinReward,
            SegmentPatternType.ObstacleHazard,
            SegmentPatternType.GateChoice,
            SegmentPatternType.MixedHazardReward
        };

        [TabGroup("GeneratorTabs", "Zorluk Ölçeklendirmesi (Difficulty)", SdfIconType.GraphUp)]
        [Tooltip("Bölüm başına matematik kapısı değer artış katsayısı.")]
        [SerializeField, Range(0.1f, 1.0f)] private float _gateValueScalePerLevel = 0.35f;

        [TabGroup("GeneratorTabs", "Zorluk Ölçeklendirmesi (Difficulty)")]
        [Tooltip("Bölüm başına ek diken engeli doğma olasılığı artışı.")]
        [SerializeField, Range(0.05f, 0.35f)] private float _hazardChanceScalePerLevel = 0.12f;

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

            // Seviye İlerlemesi: Her yeni seviyede yol uzunluğunu kademeli olarak artır
            if (!_isEndlessMode && Managers.GameManager.Instance != null)
            {
                int currentLevel = Managers.GameManager.Instance.CurrentLevel;
                _totalLevelSegments = Mathf.Clamp(12 + (currentLevel * 2), 10, 45);
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
            else if (_totalSegmentsSpawned > 1 && (!_finishSpawned || _totalSegmentsSpawned < _totalLevelSegments))
            {
                // Kalıp (Pattern) tabanlı dinamik yol üretimi
                DispatchSegmentPattern(segment);
            }

            _activeSegments.Enqueue(segment);
            _nextSpawnZ += segment.Length;
        }

        private void DispatchSegmentPattern(RoadSegment segment)
        {
            if (_patternSequence == null || _patternSequence.Count == 0)
            {
                SpawnGatePairOnSegment(segment);
                return;
            }

            int patternIndex = (_totalSegmentsSpawned - 2) % _patternSequence.Count;
            SegmentPatternType pattern = _patternSequence[patternIndex];

            int currentLevel = Managers.GameManager.Instance != null ? Managers.GameManager.Instance.CurrentLevel : 1;
            float difficultyScale = 1.0f + ((currentLevel - 1) * _gateValueScalePerLevel);

            switch (pattern)
            {
                case SegmentPatternType.GateChoice:
                    SpawnGatePairOnSegment(segment);
                    break;

                case SegmentPatternType.ObstacleHazard:
                    SpawnObstacleHazard(segment, difficultyScale);
                    break;

                case SegmentPatternType.CoinReward:
                    SpawnCoinReward(segment);
                    break;

                case SegmentPatternType.MixedHazardReward:
                    SpawnMixedHazardReward(segment, difficultyScale);
                    break;

                case SegmentPatternType.EmptyStart:
                default:
                    // Güvenli boş geçiş segmenti
                    break;
            }
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

        /// <summary>
        /// Seçim Segmenti: Yan yana iki kapı üretir. Biri KESİNLİKLE pozitif (Buff), diğeri KESİNLİKLE negatif (Debuff)'tır.
        /// </summary>
        private void SpawnGatePairOnSegment(RoadSegment segment)
        {
            GatePair gatePair = PoolManager.Instance.GetGatePair(Vector3.zero, Quaternion.identity);

            GenerateBalancedGatePairData(out GateData leftData, out GateData rightData);
            gatePair.Configure(leftData, rightData);

            segment.AttachGatePair(gatePair);
        }

        /// <summary>
        /// Kesinlikle 1 Pozitif (Buff) ve 1 Negatif (Debuff) matematiksel kapı üretir.
        /// Zorluk derecesi arttıkça kapı değerleri dinamik olarak artar.
        /// </summary>
        private void GenerateBalancedGatePairData(out GateData leftData, out GateData rightData)
        {
            int currentLevel = Managers.GameManager.Instance != null ? Managers.GameManager.Instance.CurrentLevel : 1;
            float difficultyScale = 1.0f + ((currentLevel - 1) * _gateValueScalePerLevel);

            // 1. KESİNLİKLE Pozitif (Buff: Add veya Multiply)
            GateData buffData;
            bool isBuffMultiply = currentLevel >= 2 && Random.value < 0.35f;
            if (isBuffMultiply)
            {
                int multiplier = currentLevel >= 4 && Random.value < 0.35f ? 3 : 2;
                buffData = new GateData(GateOperationType.Multiply, multiplier);
            }
            else
            {
                int minAdd = Mathf.RoundToInt(8 * difficultyScale);
                int maxAdd = Mathf.RoundToInt(18 * difficultyScale);
                buffData = new GateData(GateOperationType.Add, Random.Range(minAdd, maxAdd + 1));
            }

            // 2. KESİNLİKLE Negatif (Debuff: Subtract veya Divide)
            GateData debuffData;
            bool isDebuffDivide = currentLevel >= 3 && Random.value < 0.30f;
            if (isDebuffDivide)
            {
                int divisor = currentLevel >= 5 && Random.value < 0.25f ? 3 : 2;
                debuffData = new GateData(GateOperationType.Divide, divisor);
            }
            else
            {
                int minSub = Mathf.RoundToInt(4 * difficultyScale);
                int maxSub = Mathf.RoundToInt(10 * difficultyScale);
                debuffData = new GateData(GateOperationType.Subtract, Random.Range(minSub, maxSub + 1));
            }

            // 3. Rastgele Sol / Sağ Dağılımı (%50 / %50)
            if (Random.value < 0.5f)
            {
                leftData = buffData;
                rightData = debuffData;
            }
            else
            {
                leftData = debuffData;
                rightData = buffData;
            }
        }

        /// <summary>
        /// Engel Segmenti: Farklı şeritlere yerleştirilmiş diken tuzakları üretir.
        /// Seviye arttıkça ikinci bir engel çıkma olasılığı yükselir, ancak en az bir şerit her zaman açık kalır.
        /// </summary>
        private void SpawnObstacleHazard(RoadSegment segment, float difficultyScale)
        {
            if (PoolManager.Instance == null) return;

            float[] lanes = { -2.2f, 0f, 2.2f };
            float segmentStartZ = segment.transform.position.z;

            // 1. Ana Diken Engeli
            int spikeLaneIndex = Random.Range(0, lanes.Length);
            Obstacle obs = PoolManager.Instance.GetObstacle(new Vector3(lanes[spikeLaneIndex], 0f, segmentStartZ + 8.0f), Quaternion.identity);
            if (obs != null)
            {
                segment.AttachObstacle(obs);
            }

            // 2. İkinci Diken Engeli (Kademeli zorluk)
            int currentLevel = Managers.GameManager.Instance != null ? Managers.GameManager.Instance.CurrentLevel : 1;
            float secondSpikeChance = Mathf.Clamp01(0.30f + ((currentLevel - 1) * _hazardChanceScalePerLevel));

            if (currentLevel >= 2 && Random.value < secondSpikeChance)
            {
                // Kalan 2 şeritten birini seç (En az bir şerit kesinlikle açık kalır)
                int offset = Random.value < 0.5f ? 1 : 2;
                int secondSpikeLane = (spikeLaneIndex + offset) % lanes.Length;

                Obstacle secondObs = PoolManager.Instance.GetObstacle(new Vector3(lanes[secondSpikeLane], 0f, segmentStartZ + 13.5f), Quaternion.identity);
                if (secondObs != null)
                {
                    segment.AttachObstacle(secondObs);
                }
            }
        }

        /// <summary>
        /// Ödül Segmenti: Oyuncuyu yönlendirecek şekilde düzgün sıralanmış altın dizisi üretir.
        /// </summary>
        private void SpawnCoinReward(RoadSegment segment)
        {
            if (PoolManager.Instance == null) return;

            float[] lanes = { -2.2f, 0f, 2.2f };
            int coinLaneIndex = Random.Range(0, lanes.Length);
            float coinLaneX = lanes[coinLaneIndex];
            float segmentStartZ = segment.transform.position.z;

            int coinCount = 4;
            float startZ = segmentStartZ + 4.5f;
            float spacing = 2.8f;

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

        /// <summary>
        /// Karma Kalıp: Hem bir şeritte diken engeli hem de güvenli şeride yönlendiren altın dizisi barındırır.
        /// </summary>
        private void SpawnMixedHazardReward(RoadSegment segment, float difficultyScale)
        {
            if (PoolManager.Instance == null) return;

            float[] lanes = { -2.2f, 0f, 2.2f };
            float segmentStartZ = segment.transform.position.z;

            int spikeLaneIndex = Random.Range(0, lanes.Length);
            Obstacle obs = PoolManager.Instance.GetObstacle(new Vector3(lanes[spikeLaneIndex], 0f, segmentStartZ + 9.0f), Quaternion.identity);
            if (obs != null)
            {
                segment.AttachObstacle(obs);
            }

            int safeLaneIndex = (spikeLaneIndex + 1) % lanes.Length;
            float safeX = lanes[safeLaneIndex];

            for (int i = 0; i < 3; i++)
            {
                Vector3 coinPos = new Vector3(safeX, 0.9f, segmentStartZ + 5.0f + (i * 3.0f));
                Coin coin = PoolManager.Instance.GetCoin(coinPos, Quaternion.identity);
                if (coin != null)
                {
                    segment.AttachCoin(coin);
                }
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
