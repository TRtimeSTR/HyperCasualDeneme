using Sirenix.OdinInspector;
using UnityEngine;

namespace GateRunner.Data
{
    /// <summary>
    /// Runner karakterinin hareket, hız, sınır ve oyun hissi parametrelerini tutan Scriptable Object.
    /// Odin Inspector ile zenginleştirilmiş, tasarımcı dostu arayüze sahiptir.
    /// </summary>
    [CreateAssetMenu(fileName = "RunnerData", menuName = "GateRunner/Data/Runner Data", order = 1)]
    public class RunnerData : SerializedScriptableObject
    {
        [TabGroup("Tabs", "Hareket (Movement)", SdfIconType.Speedometer)]
        [Title("İleri Koşu Parametreleri", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Karakterin varsayılan ileri koşu hızı (birim/saniye).")]
        [SerializeField, MinValue(1f)] private float _forwardSpeed = 8.0f;

        [TabGroup("Tabs", "Hareket (Movement)")]
        [Tooltip("Güçlendirmelerle ulaşılabilecek maksimum ileri koşu hızı.")]
        [SerializeField, MinValue(1f)] private float _maxForwardSpeed = 16.0f;

        [TabGroup("Tabs", "Hareket (Movement)")]
        [Tooltip("Minimum ileri koşu hızı.")]
        [SerializeField, MinValue(0.5f)] private float _minForwardSpeed = 3.0f;

        [TabGroup("Tabs", "Yatay (Swerve)", SdfIconType.ArrowsExpand)]
        [Title("Swerve Hassasiyeti ve Sınırlar", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Dokunmatik kaydırma (swipe/drag) hassasiyeti.")]
        [SerializeField, Range(0.5f, 5f)] private float _swerveSensitivity = 2.2f;

        [TabGroup("Tabs", "Yatay (Swerve)")]
        [Tooltip("Karakterin gidebileceği yolun maksimum yatay sınırı (-X ve +X).")]
        [SerializeField, MinValue(1f)] private float _maxSwerveX = 4.0f;

        [TabGroup("Tabs", "Yatay (Swerve)")]
        [Tooltip("Yatay pozisyon hedefine ulaşma pürüzsüzlük katsayısı (Lerp/Damping).")]
        [SerializeField, Range(5f, 40f)] private float _swerveSmoothSpeed = 16.0f;

        [TabGroup("Tabs", "Game Feel (Juice)", SdfIconType.Stars)]
        [Title("Viraj ve Dönüş Eğilme Efekti", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Karakter sağa/sola kayarken gövdesini viraja doğru eğsin mi?")]
        [SerializeField] private bool _enableLeaning = true;

        [TabGroup("Tabs", "Game Feel (Juice)")]
        [ShowIf("_enableLeaning")]
        [Tooltip("Yatay hareket sırasında uygulanacak maksimum eğilme açısı (derece).")]
        [SerializeField, Range(0f, 45f)] private float _maxLeanAngle = 18.0f;

        [TabGroup("Tabs", "Game Feel (Juice)")]
        [ShowIf("_enableLeaning")]
        [Tooltip("Eğilme açısının sıfırlanma ve hedefe ulaşma yumuşaklığı.")]
        [SerializeField, Range(1f, 30f)] private float _leanSmoothSpeed = 10.0f;

        [TabGroup("Tabs", "Büyüme (Scale)", SdfIconType.AspectRatio)]
        [Title("Karakter Ölçek Sınırları", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Kapılardan geçişte küçülebileceği minimum lokal ölçek.")]
        [SerializeField, MinValue(0.2f)] private float _minScale = 0.5f;

        [TabGroup("Tabs", "Büyüme (Scale)")]
        [Tooltip("Kapılardan geçişte büyüyebileceği maksimum lokal ölçek.")]
        [SerializeField, MinValue(1f)] private float _maxScale = 3.5f;

        [TabGroup("Tabs", "Büyüme (Scale)")]
        [Tooltip("Ölçek geçişlerindeki pürüzsüzlük hızı.")]
        [SerializeField, Range(1f, 25f)] private float _scaleLerpSpeed = 8.0f;

        #region Public Properties (Encapsulation)

        public float ForwardSpeed => _forwardSpeed;
        public float MaxForwardSpeed => _maxForwardSpeed;
        public float MinForwardSpeed => _minForwardSpeed;

        public float SwerveSensitivity => _swerveSensitivity;
        public float MaxSwerveX => _maxSwerveX;
        public float SwerveSmoothSpeed => _swerveSmoothSpeed;

        public bool EnableLeaning => _enableLeaning;
        public float MaxLeanAngle => _maxLeanAngle;
        public float LeanSmoothSpeed => _leanSmoothSpeed;

        public float MinScale => _minScale;
        public float MaxScale => _maxScale;
        public float ScaleLerpSpeed => _scaleLerpSpeed;

        #endregion
    }
}
