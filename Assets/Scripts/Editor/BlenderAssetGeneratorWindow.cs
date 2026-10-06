#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GateRunner.Editor
{
    public enum BlenderAssetType
    {
        [InspectorName("Diken Engel (Spike)")]
        Spike,

        [InspectorName("Matematik Kapısı (MathGate)")]
        MathGate,

        [InspectorName("Toplanabilir Altın (Coin)")]
        Coin
    }

    /// <summary>
    /// Bilgisayardaki Blender'ı arka planda (headless) çalıştırarak prosedürel
    /// low-poly 3D modeller üreten ve Unity'ye aktaran Odin Inspector editör penceresi.
    /// </summary>
    public class BlenderAssetGeneratorWindow : OdinEditorWindow
    {
        private const string PREFS_BLENDER_PATH_KEY = "GateRunner_BlenderExecutablePath";

        [MenuItem("Tools/Gate Runner/🎨 Blender Asset Generator", priority = 10)]
        public static void OpenWindow()
        {
            var window = GetWindow<BlenderAssetGeneratorWindow>("Blender Asset Generator");
            window.minSize = new Vector2(480, 520);
            window.Show();
        }

        [TabGroup("Tabs", "Model Parametreleri", SdfIconType.BoxSeam)]
        [Title("Prosedürel Mesh Ayarları", TitleAlignment = TitleAlignments.Centered)]
        [EnumToggleButtons]
        [SerializeField] private BlenderAssetType _assetType = BlenderAssetType.Spike;

        [TabGroup("Tabs", "Model Parametreleri")]
        [Tooltip("Modelin dosya ve obje adı.")]
        [SerializeField] private string _assetName = "Obstacle_Spike_01";

        [TabGroup("Tabs", "Model Parametreleri")]
        [Range(0.2f, 10.0f)]
        [SerializeField] private float _width = 1.5f;

        [TabGroup("Tabs", "Model Parametreleri")]
        [Range(0.2f, 10.0f)]
        [SerializeField] private float _height = 2.0f;

        [TabGroup("Tabs", "Model Parametreleri")]
        [Tooltip("Üretim tamamlandığında modeli sahneye doğrudan yerleştirsin mi?")]
        [SerializeField] private bool _autoInstantiateInScene = true;

        [TabGroup("Tabs", "Sistem & Yollar", SdfIconType.Gear)]
        [Title("Blender & Dosya Yolları", TitleAlignment = TitleAlignments.Centered)]
        [Tooltip("Bilgisayarınızda kurulu blender.exe dosyasının tam yolu.")]
        [Sirenix.OdinInspector.FilePath(Extensions = "exe")]
        [SerializeField] private string _blenderPath;

        [TabGroup("Tabs", "Sistem & Yollar")]
        [Tooltip("Python üretim betiğinin proje içindeki konumu.")]
        [SerializeField] private string _scriptRelativePath = "BlenderScripts/generate_assets.py";

        [TabGroup("Tabs", "Sistem & Yollar")]
        [Tooltip("Üretilen .fbx modellerinin kaydedileceği Unity proje klasörü.")]
        [SerializeField] private string _outputFolder = "Assets/Models/Generated";

        protected override void OnEnable()
        {
            base.OnEnable();
            LoadOrDetectBlenderPath();
            UpdateDefaultName();
        }

        private void OnValidate()
        {
            UpdateDefaultName();
        }

        private void UpdateDefaultName()
        {
            if (string.IsNullOrEmpty(_assetName) || _assetName.StartsWith("Obstacle_") || _assetName.StartsWith("Gate_") || _assetName.StartsWith("Coin_"))
            {
                _assetName = _assetType switch
                {
                    BlenderAssetType.Spike => "Obstacle_Spike",
                    BlenderAssetType.MathGate => "Gate_Frame",
                    BlenderAssetType.Coin => "Coin_Gold",
                    _ => "GeneratedAsset"
                };
            }
        }

        [TabGroup("Tabs", "Model Parametreleri")]
        [Button("🔨 MESH ÜRET (Generate Mesh)", ButtonSizes.Large)]
        [GUIColor(0.15f, 0.75f, 0.35f)]
        public void GenerateMesh()
        {
            if (!ValidateSetup()) return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string pythonScriptPath = Path.Combine(projectRoot, _scriptRelativePath);
            string outputDirectory = Path.Combine(projectRoot, _outputFolder);
            string fullOutputPath = Path.Combine(outputDirectory, $"{_assetName}.fbx");
            string unityRelativeAssetPath = $"{_outputFolder}/{_assetName}.fbx";

            if (!File.Exists(pythonScriptPath))
            {
                EditorUtility.DisplayDialog("Hata", $"Python betiği bulunamadı:\n{pythonScriptPath}", "Tamam");
                return;
            }

            // Argümanları hazırla: blender -b -P BlenderScripts/generate_assets.py -- --type ...
            string arguments = $"-b -P \"{pythonScriptPath}\" -- --type {_assetType} --width {_width} --height {_height} --name \"{_assetName}\" --output \"{fullOutputPath}\"";

            try
            {
                EditorUtility.DisplayProgressBar("Blender Mesh Üretimi", $"{_assetName} oluşturuluyor...", 0.4f);

                var startInfo = new ProcessStartInfo
                {
                    FileName = _blenderPath,
                    Arguments = arguments,
                    WorkingDirectory = projectRoot,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                EditorUtility.ClearProgressBar();

                if (process.ExitCode == 0 && File.Exists(fullOutputPath))
                {
                    Debug.Log($"<color=#4CAF50><b>[Blender Bridge]</b></color> Model başarıyla oluşturuldu:\n{fullOutputPath}\n{stdout}");

                    // Unity AssetDatabase'i tazele
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                    var importedModel = AssetDatabase.LoadAssetAtPath<GameObject>(unityRelativeAssetPath);
                    if (importedModel != null)
                    {
                        EditorGUIUtility.PingObject(importedModel);
                        Selection.activeObject = importedModel;

                        if (_autoInstantiateInScene)
                        {
                            GameObject sceneInstance = (GameObject)PrefabUtility.InstantiatePrefab(importedModel);
                            sceneInstance.name = _assetName;
                            sceneInstance.transform.position = new Vector3(0f, 0f, 6.0f);
                            Undo.RegisterCreatedObjectUndo(sceneInstance, "Spawn Blender Asset");
                            Selection.activeGameObject = sceneInstance;
                        }
                    }

                    EditorUtility.DisplayDialog("Başarılı!", $"{_assetName}.fbx başarıyla üretildi ve projeye aktarıldı!", "Harika");
                }
                else
                {
                    Debug.LogError($"<color=#F44336><b>[Blender Bridge Hatası]</b></color> Çıkış Kodu: {process.ExitCode}\nStderr: {stderr}\nStdout: {stdout}");
                    EditorUtility.DisplayDialog("Blender Hatası", $"Model üretilirken bir hata oluştu:\n{stderr}", "Kapat");
                }
            }
            catch (Exception ex)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("İşlem Hatası", ex.Message, "Kapat");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private bool ValidateSetup()
        {
            if (string.IsNullOrEmpty(_blenderPath) || !File.Exists(_blenderPath))
            {
                EditorUtility.DisplayDialog("Blender Bulunamadı", "Lütfen 'Sistem & Yollar' sekmesinden geçerli bir blender.exe dosya yolu seçin.", "Tamam");
                return false;
            }

            EditorPrefs.SetString(PREFS_BLENDER_PATH_KEY, _blenderPath);
            return true;
        }

        [TabGroup("Tabs", "Sistem & Yollar")]
        [Button("🔍 Blender Yolunu Otomatik Bul", ButtonSizes.Medium)]
        public void LoadOrDetectBlenderPath()
        {
            // 1. Kaydedilmiş tercihi kontrol et
            string savedPath = EditorPrefs.GetString(PREFS_BLENDER_PATH_KEY, "");
            if (!string.IsNullOrEmpty(savedPath) && File.Exists(savedPath))
            {
                _blenderPath = savedPath;
                return;
            }

            // 2. Yaygın Windows yollarını kontrol et (Steam, Program Files)
            string[] potentialPaths = new[]
            {
                @"C:\Emre\Programlar\Steam\steamapps\common\Blender\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 5.0\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 4.3\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe",
                @"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe"
            };

            foreach (var path in potentialPaths)
            {
                if (File.Exists(path))
                {
                    _blenderPath = path;
                    EditorPrefs.SetString(PREFS_BLENDER_PATH_KEY, _blenderPath);
                    return;
                }
            }
        }
    }
}
#endif
