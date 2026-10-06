#if UNITY_EDITOR
using System.IO;
using GateRunner.Collectibles;
using GateRunner.Data;
using GateRunner.Gates;
using GateRunner.Level;
using GateRunner.Managers;
using GateRunner.Movement;
using GateRunner.Pooling;
using GateRunner.Player;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GateRunner.Editor
{
    /// <summary>
    /// Gate Runner projesinin tüm materyallerini, prefablerini, UI sistemini
    /// ve oynanabilir MainLevel sahnesini tek tıkla kuran kurulum sihirbazı.
    /// </summary>
    public class GateRunnerSetupWizard : OdinEditorWindow
    {
        [MenuItem("Tools/Gate Runner/🎮 Oynanabilir MainLevel Sahnesini Kur (Tek Tık)", priority = 0)]
        public static void SetupMainLevelDirect()
        {
            var wizard = CreateInstance<GateRunnerSetupWizard>();
            wizard.SetupPlayableMainLevel();
            DestroyImmediate(wizard);
        }

        [MenuItem("Tools/Gate Runner/⚡ Setup Wizard (Odin)", priority = 1)]
        private static void OpenWindow()
        {
            GetWindow<GateRunnerSetupWizard>("Gate Runner Setup").Show();
        }

        [Title("Gate Runner - Tam Oynanabilir Sahne Kurulum Sihirbazı", TitleAlignment = TitleAlignments.Centered)]
        [InfoBox("Bu sihirbaz;\n" +
                 "1. URP Materyallerini (Yol, Kapılar, Oyuncu, Altın)\n" +
                 "2. RoadSegment, GatePair ve Coin Prefablerini\n" +
                 "3. Skor & Altın UI Canvas sistemini (DOTween Punch animasyonlu)\n" +
                 "4. Pürüzsüz Takip Kamerasını (RunnerCamera)\n" +
                 "5. Tam oynanabilir 'MainLevel' sahnesini kurar ve kaydeder.")]

        [Button("🎮 OYNANABİLİR MAINLEVEL SAHNESİNİ KUR (Setup All)", ButtonSizes.Large)]
        [GUIColor(0.2f, 0.85f, 0.35f)]
        public void SetupPlayableMainLevel()
        {
            CreateDirectories();
            RunnerData runnerData = CreateOrLoadRunnerData();
            (Material roadMat, Material buffMat, Material debuffMat, Material playerMat, Material coinMat) = CreateMaterials();

            RoadSegment roadPrefab = CreateRoadSegmentPrefab(roadMat);
            GatePair gatePairPrefab = CreateGatePairPrefab(buffMat, debuffMat);
            Coin coinPrefab = CreateCoinPrefab(coinMat);

            CreateAndPopulateMainLevelScene(runnerData, roadPrefab, gatePairPrefab, coinPrefab, playerMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Tebrikler!", "MainLevel sahnesi, UI Canvas, Coin havuzu ve oyuncu kontrolleri başarıyla kuruldu!\n\nArtık Unity'de 'Play' tuşuna basarak oyunu hemen oynayabilirsiniz!", "Harika");
        }

        private static void CreateDirectories()
        {
            if (!Directory.Exists("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!Directory.Exists("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!Directory.Exists("Assets/Settings")) AssetDatabase.CreateFolder("Assets", "Settings");
            if (!Directory.Exists("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
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

        private static (Material, Material, Material, Material, Material) CreateMaterials()
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            Material roadMat = GetOrCreateMaterial("Assets/Materials/Mat_Road.mat", urpShader, new Color(0.18f, 0.2f, 0.25f));
            Material buffMat = GetOrCreateMaterial("Assets/Materials/Mat_GateBuff.mat", urpShader, new Color(0.12f, 0.65f, 1.0f, 0.85f));
            Material debuffMat = GetOrCreateMaterial("Assets/Materials/Mat_GateDebuff.mat", urpShader, new Color(1.0f, 0.25f, 0.25f, 0.85f));
            Material playerMat = GetOrCreateMaterial("Assets/Materials/Mat_Player.mat", urpShader, new Color(1.0f, 0.5f, 0.05f));
            Material coinMat = GetOrCreateMaterial("Assets/Materials/Mat_CoinGold.mat", urpShader, new Color(1.0f, 0.82f, 0.1f));

            return (roadMat, buffMat, debuffMat, playerMat, coinMat);
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
            col.size = new Vector3(1f, 1f, 3.5f);

            var renderer = gateGo.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;

            // TextMeshPro
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

            var mathGate = gateGo.AddComponent<MathGate>();
            var mSo = new SerializedObject(mathGate);
            mSo.FindProperty("_textMesh").objectReferenceValue = tmp;
            mSo.FindProperty("_gateRenderer").objectReferenceValue = renderer;
            mSo.FindProperty("_triggerCollider").objectReferenceValue = col;
            mSo.ApplyModifiedPropertiesWithoutUndo();

            return gate;
        }

        private static Coin CreateCoinPrefab(Material coinMat)
        {
            const string prefabPath = "Assets/Prefabs/Coin.prefab";

            GameObject root = new GameObject("Coin");
            var coin = root.AddComponent<Coin>();

            // Blender'dan ürettiğimiz modeli yükle veya primitive silindir oluştur
            GameObject coinFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Generated/Coin_Gold.fbx");
            GameObject visual;

            if (coinFbx != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(coinFbx, root.transform);
                visual.name = "Coin_Mesh";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * 0.9f;

                if (visual.TryGetComponent<Renderer>(out var ren)) ren.sharedMaterial = coinMat;
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = coinMat;
            }
            else
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.name = "Coin_Mesh";
                visual.transform.SetParent(root.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = new Vector3(0.8f, 0.15f, 0.8f);
                if (visual.TryGetComponent<Renderer>(out var ren)) ren.sharedMaterial = coinMat;
            }

            // Tetikleyici Sphere Collider
            var sphereCol = root.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.7f;

            var coinSo = new SerializedObject(coin);
            coinSo.FindProperty("_triggerCollider").objectReferenceValue = sphereCol;
            coinSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            return prefab.GetComponent<Coin>();
        }

        private static void CreateAndPopulateMainLevelScene(
            RunnerData runnerData,
            RoadSegment roadPrefab,
            GatePair gatePairPrefab,
            Coin coinPrefab,
            Material playerMat)
        {
            const string scenePath = "Assets/Scenes/MainLevel.unity";

            // Yeni sahne oluştur
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Directional Light
            GameObject lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<UnityEngine.Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1.3f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Main Camera & RunnerCamera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var runnerCam = camGo.AddComponent<GateRunner.Camera.RunnerCamera>();

            // 3. Player
            GameObject playerGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerGo.name = "Player";
            playerGo.transform.position = new Vector3(0f, 1f, 0f);
            if (playerGo.TryGetComponent<Renderer>(out var pRen)) pRen.sharedMaterial = playerMat;

            var movement = playerGo.AddComponent<SwerveMovement>();
            var modifier = playerGo.AddComponent<PlayerModifier>();

            var pSo = new SerializedObject(movement);
            pSo.FindProperty("_runnerData").objectReferenceValue = runnerData;
            pSo.ApplyModifiedPropertiesWithoutUndo();

            runnerCam.SetTarget(playerGo.transform);

            // 4. PoolManager
            GameObject poolGo = new GameObject("PoolManager");
            var poolManager = poolGo.AddComponent<PoolManager>();
            var poolSo = new SerializedObject(poolManager);
            poolSo.FindProperty("_roadSegmentPrefab").objectReferenceValue = roadPrefab;
            poolSo.FindProperty("_gatePairPrefab").objectReferenceValue = gatePairPrefab;
            poolSo.FindProperty("_coinPrefab").objectReferenceValue = coinPrefab;
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. LevelGenerator
            GameObject genGo = new GameObject("LevelGenerator");
            var levelGen = genGo.AddComponent<LevelGenerator>();
            var genSo = new SerializedObject(levelGen);
            genSo.FindProperty("_playerTransform").objectReferenceValue = playerGo.transform;
            genSo.ApplyModifiedPropertiesWithoutUndo();

            // 6. UI Canvas (Score & Coins)
            SetupUICanvas();

            // Sahneyi kaydet
            EditorSceneManager.SaveScene(scene, scenePath);
        }

        private static void SetupUICanvas()
        {
            // Canvas Root
            GameObject canvasGo = new GameObject("UI_Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Event System
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();

            // Score Banner Panel
            GameObject scorePanelGo = new GameObject("Panel_Score");
            scorePanelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = scorePanelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1.0f);
            panelRect.anchorMax = new Vector2(0.5f, 1.0f);
            panelRect.pivot = new Vector2(0.5f, 1.0f);
            panelRect.anchoredPosition = new Vector2(0f, -60f);
            panelRect.sizeDelta = new Vector2(500f, 140f);

            // Skor Metni (TMP)
            GameObject scoreTextGo = new GameObject("Text_Score");
            scoreTextGo.transform.SetParent(scorePanelGo.transform, false);
            var scoreTextRect = scoreTextGo.AddComponent<RectTransform>();
            scoreTextRect.anchorMin = Vector2.zero;
            scoreTextRect.anchorMax = Vector2.one;
            scoreTextRect.sizeDelta = Vector2.zero;

            var tmpScore = scoreTextGo.AddComponent<TextMeshProUGUI>();
            tmpScore.text = "SKOR: 0";
            tmpScore.fontSize = 54;
            tmpScore.fontStyle = FontStyles.Bold;
            tmpScore.alignment = TextAlignmentOptions.Center;
            tmpScore.color = new Color(1f, 0.95f, 0.4f);

            // Altın Sayacı (TMP)
            GameObject coinTextGo = new GameObject("Text_Coin");
            coinTextGo.transform.SetParent(canvasGo.transform, false);
            var coinTextRect = coinTextGo.AddComponent<RectTransform>();
            coinTextRect.anchorMin = new Vector2(1f, 1f);
            coinTextRect.anchorMax = new Vector2(1f, 1f);
            coinTextRect.pivot = new Vector2(1f, 1f);
            coinTextRect.anchoredPosition = new Vector2(-40f, -60f);
            coinTextRect.sizeDelta = new Vector2(240f, 100f);

            var tmpCoin = coinTextGo.AddComponent<TextMeshProUGUI>();
            tmpCoin.text = "🪙 0";
            tmpCoin.fontSize = 48;
            tmpCoin.fontStyle = FontStyles.Bold;
            tmpCoin.alignment = TextAlignmentOptions.Right;
            tmpCoin.color = Color.white;

            // ScoreManager Bağlantısı
            var scoreManager = canvasGo.AddComponent<ScoreManager>();
            var sSo = new SerializedObject(scoreManager);
            sSo.FindProperty("_scoreText").objectReferenceValue = tmpScore;
            sSo.FindProperty("_coinText").objectReferenceValue = tmpCoin;
            sSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
