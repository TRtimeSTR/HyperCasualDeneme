using GateRunner.Managers;
using GateRunner.Movement;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Level
{
    /// <summary>
    /// Yol materyalinin UV Texture Offset değerini (UV Scrolling) karakterin hızına göre
    /// sürekli geriye doğru kaydırarak akıcı ve dinamik bir hız hissi (Speed Feel) üreten kontrolcü.
    /// </summary>
    public class RoadMaterialScroller : SerializedMonoBehaviour
    {
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        [Title("Materyal & Hız Ayarları", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Kaydırılacak yol materyali (Mat_Road). Boş bırakılırsa Resources/Materials içinden otomatik yüklenir.")]
        [SerializeField] private Material _roadMaterial;

        [Tooltip("Karakter hareket kontrolcüsü. Boş bırakılırsa sahnede otomatik bulunur.")]
        [SerializeField] private SwerveMovement _player;

        [Tooltip("Kaydırma hız çarpanı.")]
        [SerializeField, Range(0.01f, 0.5f)] private float _scrollMultiplier = 0.05f;

        [ShowInInspector, ReadOnly, FoldoutGroup("Canlı Durum")]
        private float _currentOffsetY = 0f;

        private bool _hasPropertyBaseMap;
        private bool _hasPropertyMainTex;

        private void Awake()
        {
            if (_player == null)
            {
                _player = FindAnyObjectByType<SwerveMovement>();
            }

            if (_roadMaterial == null)
            {
                var renderer = GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    _roadMaterial = renderer.sharedMaterial;
                }
            }

            if (_roadMaterial != null)
            {
                _hasPropertyBaseMap = _roadMaterial.HasProperty(BaseMapId);
                _hasPropertyMainTex = _roadMaterial.HasProperty(MainTexId);
            }
        }

        private void Update()
        {
            if (_roadMaterial == null) return;

            // Karakter koşmuyorsa (Ready / Victory / GameOver) kaydırma yapma
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Running)
            {
                return;
            }

            float forwardSpeed = _player != null ? _player.CurrentForwardSpeed : 10.0f;
            _currentOffsetY += forwardSpeed * _scrollMultiplier * Time.deltaTime;

            if (_currentOffsetY > 1000f) _currentOffsetY -= 1000f; // Taşmayı önle

            Vector2 offset = new Vector2(0f, _currentOffsetY);

            if (_hasPropertyBaseMap)
            {
                _roadMaterial.SetTextureOffset(BaseMapId, offset);
            }
            if (_hasPropertyMainTex)
            {
                _roadMaterial.SetTextureOffset(MainTexId, offset);
            }
        }

        private void OnDestroy()
        {
            // Editörden çıkarken veya sahne kapanırken materyal offsetini sıfırla
            if (_roadMaterial != null)
            {
                if (_hasPropertyBaseMap) _roadMaterial.SetTextureOffset(BaseMapId, Vector2.zero);
                if (_hasPropertyMainTex) _roadMaterial.SetTextureOffset(MainTexId, Vector2.zero);
            }
        }
    }
}
