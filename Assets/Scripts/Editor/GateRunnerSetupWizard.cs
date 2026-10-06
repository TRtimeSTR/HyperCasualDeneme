#if UNITY_EDITOR
using System.IO;
using GateRunner.Data;
using GateRunner.Gates;
using GateRunner.Level;
using GateRunner.Movement;
using GateRunner.Pooling;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GateRunner.Editor
{
    /// <summary>
    /// Gate Runner projesinin tüm materyallerini, prefablerini ve sahne bileşenlerini
    /// tek tıkla otomatik kuran Odin Inspector destekli kurulum sihirbazı.
    /// </summary>
    public class GateRunnerSetupWizard : OdinEditorWindow
    {
        [MenuItem("Tools/Gate Runner/⚡ Setup Wizard (Odin)", priority = 1)]
        private static void OpenWindow()
        {
            GetWindow<GateRunnerSetupWizard>("Gate Runner Setup").Show();
        }

        [MenuItem("Tools/Gate Runner/Hızlı Kurulum (Otomatik Sahne & Prefab)", priority = 2)]
        public static void QuickSetupDirect()
        {
            var wizard = CreateInstance<GateRunnerSetupWizard>();
            wizard.SetupAll();
            DestroyImmediate(wizard);
        }

        [Title("Gate Runner - Otomatik Sahne & Prefab Kurulum Sihirbazı", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Bu sihirbaz;\n" +
                 "1. URP Materyallerini (Yol, Kapılar, Oyuncu)\n" +
                 "2. RoadSegment ve GatePair Prefablerini\n" +
                 "3. Sahnede Player, PoolManager ve LevelGenerator objelerini\n" +
                 "tek tıkla kurar ve birbirine bağlar.")]

        [Button("⚡ HER ŞEYİ OTOMATİK KUR (Setup All)", ButtonSizes.Large)]
        [GUIColor(0.2f, 0.8f, 0.3f)]
        public void SetupAll()
        {
            CreateDirectories();
            RunnerData runnerData = CreateOrLoadRunnerData();
            (Material roadMat, Material buffMat, Material debuffMat, Material playerMat) = CreateMaterials();
            RoadSegment roadPrefab = CreateRoadSegmentPrefab(roadMat);
            GatePair gatePairPrefab = CreateGatePairPrefab(buffMat, debuffMat);

            SetupSceneHierarchy(runnerData, roadPrefab, gatePairPrefab, playerMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("Başarılı!", "Gate Runner seviye üretimi, havuzlama ve sahne bileşenleri başarıyla kuruldu!", "Tamam");
        }

        private static void CreateDirectories()
        {
            if (!Directory.Exists("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!Directory.Exists("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!Directory.Exists("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
        }

        private static RunnerData CreateOrLoadRunnerData()
        {
            const string path = "Assets/Settings/DefaultRunnerData.asset";
            var data = AssetDatabase.LoadAssetAtPath<RunnerData>(path);
            if (data == null)
            {
                data = CreateInstance<RunnerData>();
                AssetDatabase.CreateAsset(data, path);
            }
            return data;
        }

        private static (Material, Material, Material, Material) CreateMaterials()
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material roadMat = GetOrCreateMaterial("Assets/Materials/Mat_Road.mat", urpShader, new Color(0.18f, 0.2f, 0.25f));
            Material buffMat = GetOrCreateMaterial("Assets/Materials/Mat_GateBuff.mat", urpShader, new Color(0.12f, 0.65f, 1.0f, 0.85f));
            Material debuffMat = GetOrCreateMaterial("Assets/Materials/Mat_GateDebuff.mat", urpShader, new Color(1.0f, 0.25f, 0.25f, 0.85f));
            Material playerMat = GetOrCreateMaterial("Assets/Materials/Mat_Player.mat", urpShader, new Color(1.0f, 0.5f, 0.05f));

            return (roadMat, buffMat, debuffMat, playerMat);
        }

        private static Material GetOrCreateMaterial(string path, Shader shader, Color color)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static RoadSegment CreateRoadSegmentPrefab(Material roadMat)
        {
            const string prefabPath = "Assets/Prefabs/RoadSegment.prefab";

            GameObject root = new GameObject("RoadSegment");
            var roadSegment = root.AddComponent<RoadSegment>();

            // Görsel Zemin
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Road_Mesh";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = new Vector3(0f, -0.2f, 10.0f);
            visual.transform.localScale = new Vector3(9.0f, 0.4f, 20.0f);
            if (visual.TryGetComponent<Renderer>(out var ren)) ren.sharedMaterial = roadMat;

            // Kapı Montaj Soketi
            GameObject socket = new GameObject("GateSocket");
            socket.transform.SetParent(root.transform);
            socket.transform.localPosition = new Vector3(0f, 0f, 10.0f);

            var so = new SerializedObject(roadSegment);
            so.FindProperty("_length").floatValue = 20.0f;
            so.FindProperty("_gateSocket").objectReferenceValue = socket.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            return prefab.GetComponent<RoadSegment>();
        }

        private static GatePair CreateGatePairPrefab(Material buffMat, Material debuffMat)
        {
            const string prefabPath = "Assets/Prefabs/GatePair.prefab";

            GameObject root = new GameObject("GatePair");
            var gatePair = root.AddComponent<GatePair>();

            // Sol Kapı
            Gate leftGate = CreateSingleGate("Gate_Left", root.transform, new Vector3(-2.2f, 1.25f, 0f), buffMat);
            // Sağ Kapı
            Gate rightGate = CreateSingleGate("Gate_Right", root.transform, new Vector3(2.2f, 1.25f, 0f), buffMat);

            var so = new SerializedObject(gatePair);
            so.FindProperty("_leftGate").objectReferenceValue = leftGate;
            so.FindProperty("_rightGate").objectReferenceValue = rightGate;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            return prefab.GetComponent<GatePair>();
        }

        private static Gate CreateSingleGate(string name, Transform parent, Vector3 localPos, Material mat)
        {
            GameObject gateGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateGo.name = name;
            gateGo.transform.SetParent(parent);
            gateGo.transform.localPosition = localPos;
            gateGo.transform.localScale = new Vector3(3.8f, 2.5f, 0.2f);

            var col = gateGo.GetComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1f, 1f, 3.5f); // Rahat algılama için Z genişliği

            var renderer = gateGo.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;

            // TextMeshPro oluştur
            GameObject textGo = new GameObject("Text_Value");
            textGo.transform.SetParent(gateGo.transform);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            textGo.transform.localScale = Vector3.one * 0.15f;

            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.text = "+10";
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            var gate = gateGo.AddComponent<Gate>();
            var so = new SerializedObject(gate);
            so.FindProperty("_textMesh").objectReferenceValue = tmp;
            so.FindProperty("_gateRenderer").objectReferenceValue = renderer;
            so.FindProperty("_triggerCollider").objectReferenceValue = col;
            so.ApplyModifiedPropertiesWithoutUndo();

            return gate;
        }

        private static void SetupSceneHierarchy(RunnerData runnerData, RoadSegment roadPrefab, GatePair gatePairPrefab, Material playerMat)
        {
            // 1. Oyuncu (Player)
            var player = FindFirstObjectByType<SwerveMovement>();
            if (player == null)
            {
                GameObject playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                playerGo.name = "Player";
                playerGo.transform.position = new Vector3(0f, 1f, 0f);
                if (playerGo.TryGetComponent<Renderer>(out var pRen)) pRen.sharedMaterial = playerMat;

                player = playerGo.AddComponent<SwerveMovement>();
            }

            var pSo = new SerializedObject(player);
            pSo.FindProperty("_runnerData").objectReferenceValue = runnerData;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            // 2. PoolManager
            var poolManager = FindFirstObjectByType<PoolManager>();
            if (poolManager == null)
            {
                GameObject poolGo = new GameObject("PoolManager");
                poolManager = poolGo.AddComponent<PoolManager>();
            }

            var poolSo = new SerializedObject(poolManager);
            poolSo.FindProperty("_roadSegmentPrefab").objectReferenceValue = roadPrefab;
            poolSo.FindProperty("_gatePairPrefab").objectReferenceValue = gatePairPrefab;
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            // 3. LevelGenerator
            var levelGen = FindFirstObjectByType<LevelGenerator>();
            if (levelGen == null)
            {
                GameObject genGo = new GameObject("LevelGenerator");
                levelGen = genGo.AddComponent<LevelGenerator>();
            }

            var genSo = new SerializedObject(levelGen);
            genSo.FindProperty("_playerTransform").objectReferenceValue = player.transform;
            genSo.ApplyModifiedPropertiesWithoutUndo();

            // 4. Kamera Konumlandırma (Hypercasual Runner Açısı)
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                mainCam.transform.position = new Vector3(0f, 6.5f, -8.0f);
                mainCam.transform.rotation = Quaternion.Euler(22.0f, 0f, 0f);
            }
        }
    }
}
#endif
