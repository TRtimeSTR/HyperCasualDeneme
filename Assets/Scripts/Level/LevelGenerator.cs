using System.Collections.Generic;
using GateRunner.Data;
using GateRunner.Gates;
using GateRunner.Movement;
using GateRunner.Pooling;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Level
{
    /// <summary>
    /// Karakter ilerledikçe dinamik olarak yeni yol parçaları ve matematiksel kapılar üreten,
    /// arkada kalanları PoolManager üzerinden havuza geri gönderen sonsuz seviye üreticisi.
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

        [TabGroup("GeneratorTabs", "Kapı Ayarları (Gates)", SdfIconType.DoorOpen)]
        [Tooltip("İlk kapı çifti kaçıncı yol parçasından itibaren çıkmaya başlasın?")]
        [SerializeField, MinValue(1)] private int _firstGateSegmentIndex = 2;

        [TabGroup("GeneratorTabs", "Kapı Ayarları (Gates)")]
        [Tooltip("Kaç yol parçasında bir kapı çifti doğurulsun? (Örn: 2 = her 2 parçada bir)")]
        [SerializeField, MinValue(1)] private int _gateSegmentInterval = 2;

        [TabGroup("GeneratorTabs", "Kapı Ayarları (Gates)")]
        [Tooltip("Kapı çiftlerinden birinin pozitif (Buff) olma garantisi.")]
        [SerializeField] private bool _guaranteeOneBuff = true;

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

        private void Start()
        {
            if (_playerTransform == null)
            {
                var player = FindFirstObjectByType<SwerveMovement>();
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

            // Oyuncunun önünü sürekli dolu tut
            while (_nextSpawnZ < _playerTransform.position.z + _forwardSpawnDistance)
            {
                SpawnNextSegment();
            }

            // Oyuncunun arkasında kalan eski parçaları havuza geri gönder
            RecycleOldSegments();
        }

        /// <summary>
        /// Seviye başında oyuncunun önüne başlangıç yol parçalarını dizer.
        /// </summary>
        private void GenerateInitialTrack()
        {
            _nextSpawnZ = 0.0f;
            _totalSegmentsSpawned = 0;

            for (int i = 0; i < _initialSegmentCount; i++)
            {
                SpawnNextSegment();
            }
        }

        /// <summary>
        /// Havuzdan bir sonraki yol parçasını alır ve gerekiyorsa üzerine kapı çifti yerleştirir.
        /// </summary>
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

            // Kapı doğurma kontrolü
            if (ShouldSpawnGateOnSegment(_totalSegmentsSpawned))
            {
                SpawnGatePairOnSegment(segment);
            }

            _activeSegments.Enqueue(segment);
            _nextSpawnZ += segment.Length;
        }

        private bool ShouldSpawnGateOnSegment(int segmentIndex)
        {
            if (segmentIndex < _firstGateSegmentIndex) return false;
            return (segmentIndex - _firstGateSegmentIndex) % _gateSegmentInterval == 0;
        }

        /// <summary>
        /// Segment üzerindeki montaj yuvasına bir kapı çifti doğurur ve dengeli matematiksel veriler atar.
        /// </summary>
        private void SpawnGatePairOnSegment(RoadSegment segment)
        {
            GatePair gatePair = PoolManager.Instance.GetGatePair(Vector3.zero, Quaternion.identity);

            // Dengeli ve eğlenceli kapı seçenekleri oluştur (+15 vs x2 veya +20 vs -10)
            GenerateBalancedGatePairData(out GateData leftData, out GateData rightData);
            gatePair.Configure(leftData, rightData);

            segment.AttachGatePair(gatePair);
        }

        /// <summary>
        /// Hypercasual gate runner dinamiğine uygun sol ve sağ kapı matematik verileri üretir.
        /// </summary>
        private void GenerateBalancedGatePairData(out GateData leftData, out GateData rightData)
        {
            int scenario = Random.Range(0, 3);

            switch (scenario)
            {
                // Senaryo 0: İki Farklı Pozitif Seçenek (Taktiksel Seçim: +Toplama vs xÇarpma)
                case 0:
                    leftData = new GateData(GateOperationType.Add, Random.Range(10, 35));
                    rightData = new GateData(GateOperationType.Multiply, Random.Range(2, 4));
                    break;

                // Senaryo 1: Bir Büyük Pozitif vs Bir Negatif Engel (+20 vs -10)
                case 1:
                    leftData = new GateData(GateOperationType.Add, Random.Range(15, 30));
                    rightData = new GateData(GateOperationType.Subtract, Random.Range(5, 15));
                    break;

                // Senaryo 2: Bir Çarpma vs Bir Bölme (x2 vs ÷2)
                case 2:
                default:
                    leftData = new GateData(GateOperationType.Multiply, Random.Range(2, 3));
                    rightData = new GateData(GateOperationType.Divide, 2);
                    break;
            }

            // Rastgele sağ/sol yer değiştir (böylece ödül her zaman solda veya sağda kalmaz)
            if (Random.value > 0.5f)
            {
                (leftData, rightData) = (rightData, leftData);
            }
        }

        /// <summary>
        /// Karakterin arkasında kalan eski parçaları sırayla kontrol edip havuza bırakır.
        /// </summary>
        private void RecycleOldSegments()
        {
            while (_activeSegments.Count > 0)
            {
                RoadSegment oldestSegment = _activeSegments.Peek();
                float segmentZEnd = oldestSegment.transform.position.z + oldestSegment.Length;

                if (_playerTransform.position.z - segmentZEnd > _recycleDistanceBehind)
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

        #region Odin Inspector Tools
        [Button("Seviyeyi Yeniden Başlat (Reset)", ButtonSizes.Medium), FoldoutGroup("Seviye Kontrolleri")]
        public void ResetGenerator()
        {
            while (_activeSegments.Count > 0)
            {
                RoadSegment segment = _activeSegments.Dequeue();
                PoolManager.Instance?.ReleaseRoadSegment(segment);
            }

            GenerateInitialTrack();
        }
        #endregion
    }
}
