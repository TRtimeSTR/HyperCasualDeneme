#if UNITY_EDITOR
using System.IO;
using GateRunner.Camera;
using GateRunner.Collectibles;
using GateRunner.Data;
using GateRunner.Gates;
using GateRunner.Level;
using GateRunner.Managers;
using GateRunner.Movement;
using GateRunner.Obstacles;
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
    /// Gate Runner projesinin tüm materyallerini, prefablerini (Road, Gate, Coin, Spike, FinishLine),
    /// VFX parçacık sistemlerini, tam teşekküllü UI Canvas'ını ve oynanabilir MainLevel sahnesini kuran sihirbaz.
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
                 "1. URP Materyallerini (Yol, Kapılar, Oyuncu, Altın, Dikenler, Bitiş Kemeri)\n" +
                 "2. 3D Low-Poly Prefableri (RoadSegment, GatePair, Coin, Spike, FinishLine)\n" +
                 "3. VFX Parçacık Sistemlerini (Altın Toplama, Diken Çarpışması, Konfeti Zafer Efekti)\n" +
                 "4. UI Canvas Sistemini (Skor, Altın, Seviye, Zafer Paneli, Yenilgi Paneli)\n" +
                 "5. Pürüzsüz Takip Kamerasını (RunnerCamera)\n" +
                 "6. GameManager ve PoolManager ile tam oynanabilir 'MainLevel' sahnesini kurar ve kaydeder.")]

        [Button("🎮 OYNANABİLİR MAINLEVEL SAHNESİNİ KUR (Setup All)", ButtonSizes.Large)]
        [GUIColor(0.2f, 0.85f, 0.35f)]
        public void SetupPlayableMainLevel()
        {
            CreateDirectories();
            RunnerData runnerData = CreateOrLoadRunnerData();

            // 1. Materyaller
            var materials = CreateMaterials();

            // 2. VFX Parçacık Prefableri
            ParticleSystem coinVfxPrefab = CreateParticlePrefab("VFX_CoinCollect", new Color(1f, 0.85f, 0.2f), 16, 4.5f, 0.4f, 0.5f);
            ParticleSystem spikeVfxPrefab = CreateParticlePrefab("VFX_SpikeHit", new Color(1f, 0.2f, 0.15f), 20, 6.0f, 0.35f, 0.5f);
            ParticleSystem confettiVfxPrefab = CreateParticlePrefab("VFX_Confetti", new Color(0.2f, 0.85f, 1f), 40, 8.0f, 1.2f, 1.5f);

            // 3. Oyun Objeleri Prefableri
            RoadSegment roadPrefab = CreateRoadSegmentPrefab(materials.roadMat);
            GatePair gatePairPrefab = CreateGatePairPrefab(materials.buffMat, materials.debuffMat);
            Coin coinPrefab = CreateCoinPrefab(materials.coinMat);
            Obstacle obstaclePrefab = CreateObstaclePrefab(materials.spikeMat, spikeVfxPrefab);
            FinishLine finishLinePrefab = CreateFinishLinePrefab(materials.finishMat, confettiVfxPrefab);

            // 4. Sahneyi İnşa Et
            CreateAndPopulateMainLevelScene(runnerData, roadPrefab, gatePairPrefab, coinPrefab, obstaclePrefab, finishLinePrefab, materials.playerMat);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#4CAF50><b>[Gate Runner Setup]</b></color> MainLevel sahnesi, GameManager, PoolManager, VFX ve UI sistemleri başarıyla kuruldu ve kaydedildi!");
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

        private struct MaterialSet
        {
            public Material roadMat;
            public Material buffMat;
            public Material debuffMat;
            public Material playerMat;
            public Material coinMat;
            public Material spikeMat;
            public Material finishMat;
        }

        private static MaterialSet CreateMaterials()
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            return new MaterialSet
            {
                roadMat = GetOrCreateMaterial("Assets/Materials/Mat_Road.mat", urpShader, new Color(0.18f, 0.2f, 0.25f)),
                buffMat = GetOrCreateMaterial("Assets/Materials/Mat_GateBuff.mat", urpShader, new Color(0.12f, 0.65f, 1.0f, 0.85f)),
                debuffMat = GetOrCreateMaterial("Assets/Materials/Mat_GateDebuff.mat", urpShader, new Color(1.0f, 0.25f, 0.25f, 0.85f)),
                playerMat = GetOrCreateMaterial("Assets/Materials/Mat_Player.mat", urpShader, new Color(1.0f, 0.5f, 0.05f)),
                coinMat = GetOrCreateMaterial("Assets/Materials/Mat_CoinGold.mat", urpShader, new Color(1.0f, 0.82f, 0.1f)),
                spikeMat = GetOrCreateMaterial("Assets/Materials/Mat_SpikeRed.mat", urpShader, new Color(0.95f, 0.15f, 0.15f)),
                finishMat = GetOrCreateMaterial("Assets/Materials/Mat_FinishGold.mat", urpShader, new Color(0.95f, 0.75f, 0.15f))
            };
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

        private static ParticleSystem CreateParticlePrefab(string name, Color color, int count, float speed, float lifetime, float duration)
        {
            string prefabPath = $"Assets/Prefabs/{name}.prefab";
            GameObject root = new GameObject(name);
            var ps = root.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = 0.25f;
            main.startColor = color;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            return prefab.GetComponent<ParticleSystem>();
        }

        private static RoadSegment CreateRoadSegmentPrefab(Material roadMat)
        {
            const string prefabPath = "Assets/Prefabs/RoadSegment.prefab";

            GameObject root = new GameObject("RoadSegment");
            var roadSegment = root.AddComponent<RoadSegment>();

            // Zemin
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Road_Mesh";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = new Vector3(0f, -0.2f, 10.0f);
            visual.transform.localScale = new Vector3(9.0f, 0.4f, 20.0f);
            if (visual.TryGetComponent<Renderer>(out var ren)) ren.sharedMaterial = roadMat;

            // Yan Sınır Korkulukları (Borders)
            GameObject leftBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftBorder.name = "Border_Left";
            leftBorder.transform.SetParent(root.transform);
            leftBorder.transform.localPosition = new Vector3(-4.6f, 0.2f, 10.0f);
            leftBorder.transform.localScale = new Vector3(0.3f, 0.5f, 20.0f);
            if (leftBorder.TryGetComponent<Renderer>(out var lRen)) lRen.sharedMaterial = roadMat;

            GameObject rightBorder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightBorder.name = "Border_Right";
            rightBorder.transform.SetParent(root.transform);
            rightBorder.transform.localPosition = new Vector3(4.6f, 0.2f, 10.0f);
            rightBorder.transform.localScale = new Vector3(0.3f, 0.5f, 20.0f);
            if (rightBorder.TryGetComponent<Renderer>(out var rRen)) rRen.sharedMaterial = roadMat;

            // Soket
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

            Gate leftGate = CreateSingleGate("Gate_Left", root.transform, new Vector3(-2.2f, 1.25f, 0f), buffMat);
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

            GameObject coinFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Generated/Coin_Gold.fbx");
            if (coinFbx != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(coinFbx, root.transform);
                visual.name = "Coin_Mesh";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * 0.9f;
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = coinMat;
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.name = "Coin_Mesh";
                visual.transform.SetParent(root.transform);
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                visual.transform.localScale = new Vector3(0.8f, 0.15f, 0.8f);
                if (visual.TryGetComponent<Renderer>(out var ren)) ren.sharedMaterial = coinMat;
            }

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

        private static Obstacle CreateObstaclePrefab(Material spikeMat, ParticleSystem hitVfxPrefab)
        {
            const string prefabPath = "Assets/Prefabs/Obstacle_Spike.prefab";

            GameObject root = new GameObject("Obstacle_Spike");
            var obstacle = root.AddComponent<Obstacle>();

            GameObject spikeFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Generated/Obstacle_Spike.fbx");
            if (spikeFbx != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(spikeFbx, root.transform);
                visual.name = "Spike_Mesh";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = spikeMat;
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                visual.name = "Spike_Mesh";
                visual.transform.SetParent(root.transform);
                visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                visual.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
                if (visual.TryGetComponent<Renderer>(out var ren)) ren.sharedMaterial = spikeMat;
            }

            var boxCol = root.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, 0.9f, 0f);
            boxCol.size = new Vector3(1.2f, 1.8f, 1.2f);

            var so = new SerializedObject(obstacle);
            so.FindProperty("_damage").intValue = 8;
            so.FindProperty("_triggerCollider").objectReferenceValue = boxCol;
            if (hitVfxPrefab != null) so.FindProperty("_hitVfx").objectReferenceValue = hitVfxPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            return prefab.GetComponent<Obstacle>();
        }

        private static FinishLine CreateFinishLinePrefab(Material finishMat, ParticleSystem confettiVfxPrefab)
        {
            const string prefabPath = "Assets/Prefabs/FinishLine.prefab";

            GameObject root = new GameObject("FinishLine");
            var finishLine = root.AddComponent<FinishLine>();

            GameObject archFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Generated/Finish_Arch.fbx");
            if (archFbx != null)
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(archFbx, root.transform);
                visual.name = "Arch_Mesh";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.sharedMaterial = finishMat;
            }
            else
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Arch_Mesh";
                visual.transform.SetParent(root.transform);
                visual.transform.localPosition = new Vector3(0f, 2.0f, 0f);
                visual.transform.localScale = new Vector3(7.0f, 4.0f, 0.5f);
                if (visual.TryGetComponent<Renderer>(out var ren)) ren.sharedMaterial = finishMat;
            }

            var boxCol = root.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.center = new Vector3(0f, 2.0f, 0f);
            boxCol.size = new Vector3(7.5f, 4.5f, 2.0f);

            var so = new SerializedObject(finishLine);
            so.FindProperty("_triggerCollider").objectReferenceValue = boxCol;
            if (confettiVfxPrefab != null) so.FindProperty("_confettiVfx").objectReferenceValue = confettiVfxPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            DestroyImmediate(root);
            return prefab.GetComponent<FinishLine>();
        }

        private static void CreateAndPopulateMainLevelScene(
            RunnerData runnerData,
            RoadSegment roadPrefab,
            GatePair gatePairPrefab,
            Coin coinPrefab,
            Obstacle obstaclePrefab,
            FinishLine finishLinePrefab,
            Material playerMat)
        {
            const string scenePath = "Assets/Scenes/MainLevel.unity";

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Directional Light
            GameObject lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<UnityEngine.Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.98f, 0.95f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 2. Camera & RunnerCamera
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var runnerCam = camGo.AddComponent<RunnerCamera>();

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
            poolSo.FindProperty("_obstaclePrefab").objectReferenceValue = obstaclePrefab;
            poolSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. LevelGenerator
            GameObject genGo = new GameObject("LevelGenerator");
            var levelGen = genGo.AddComponent<LevelGenerator>();
            var genSo = new SerializedObject(levelGen);
            genSo.FindProperty("_playerTransform").objectReferenceValue = playerGo.transform;
            genSo.FindProperty("_finishLinePrefab").objectReferenceValue = finishLinePrefab;
            genSo.FindProperty("_totalLevelSegments").intValue = 16;
            genSo.ApplyModifiedPropertiesWithoutUndo();

            // 6. UI Canvas & GameManager
            SetupUICanvas();

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

            // Level Text (TMP)
            GameObject levelTextGo = new GameObject("Text_Level");
            levelTextGo.transform.SetParent(canvasGo.transform, false);
            var levelRect = levelTextGo.AddComponent<RectTransform>();
            levelRect.anchorMin = new Vector2(0.5f, 1.0f);
            levelRect.anchorMax = new Vector2(0.5f, 1.0f);
            levelRect.pivot = new Vector2(0.5f, 1.0f);
            levelRect.anchoredPosition = new Vector2(0f, -40f);
            levelRect.sizeDelta = new Vector2(400f, 70f);

            var tmpLevel = levelTextGo.AddComponent<TextMeshProUGUI>();
            tmpLevel.text = "LEVEL 1";
            tmpLevel.fontSize = 44;
            tmpLevel.fontStyle = FontStyles.Bold;
            tmpLevel.alignment = TextAlignmentOptions.Center;
            tmpLevel.color = Color.white;

            // Score Banner Panel
            GameObject scorePanelGo = new GameObject("Panel_Score");
            scorePanelGo.transform.SetParent(canvasGo.transform, false);
            var panelRect = scorePanelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1.0f);
            panelRect.anchorMax = new Vector2(0.5f, 1.0f);
            panelRect.pivot = new Vector2(0.5f, 1.0f);
            panelRect.anchoredPosition = new Vector2(0f, -110f);
            panelRect.sizeDelta = new Vector2(500f, 100f);

            var scoreTextGo = new GameObject("Text_Score");
            scoreTextGo.transform.SetParent(scorePanelGo.transform, false);
            var scoreTextRect = scoreTextGo.AddComponent<RectTransform>();
            scoreTextRect.anchorMin = Vector2.zero;
            scoreTextRect.anchorMax = Vector2.one;
            scoreTextRect.sizeDelta = Vector2.zero;

            var tmpScore = scoreTextGo.AddComponent<TextMeshProUGUI>();
            tmpScore.text = "10";
            tmpScore.fontSize = 58;
            tmpScore.fontStyle = FontStyles.Bold;
            tmpScore.alignment = TextAlignmentOptions.Center;
            tmpScore.color = new Color(1f, 0.95f, 0.35f);

            // Altın Sayacı (TMP)
            GameObject coinTextGo = new GameObject("Text_Coin");
            coinTextGo.transform.SetParent(canvasGo.transform, false);
            var coinTextRect = coinTextGo.AddComponent<RectTransform>();
            coinTextRect.anchorMin = new Vector2(1f, 1f);
            coinTextRect.anchorMax = new Vector2(1f, 1f);
            coinTextRect.pivot = new Vector2(1f, 1f);
            coinTextRect.anchoredPosition = new Vector2(-40f, -60f);
            coinTextRect.sizeDelta = new Vector2(240f, 90f);

            var tmpCoin = coinTextGo.AddComponent<TextMeshProUGUI>();
            tmpCoin.text = "🪙 0";
            tmpCoin.fontSize = 44;
            tmpCoin.fontStyle = FontStyles.Bold;
            tmpCoin.alignment = TextAlignmentOptions.Right;
            tmpCoin.color = Color.white;

            // ScoreManager
            var scoreManager = canvasGo.AddComponent<ScoreManager>();
            var sSo = new SerializedObject(scoreManager);
            sSo.FindProperty("_scoreText").objectReferenceValue = tmpScore;
            sSo.FindProperty("_coinText").objectReferenceValue = tmpCoin;
            sSo.ApplyModifiedPropertiesWithoutUndo();

            // Victory Panel
            (GameObject victoryPanel, Button nextBtn) = CreateResultPanel(canvasGo.transform, "Panel_Victory", "BÖLÜM TAMAMLANDI!", "SONRAKİ BÖLÜM", new Color(0.1f, 0.75f, 0.3f, 0.92f));

            // Game Over Panel
            (GameObject gameOverPanel, Button retryBtn) = CreateResultPanel(canvasGo.transform, "Panel_GameOver", "BÖLÜM BAŞARISIZ!", "TEKRAR DENE", new Color(0.85f, 0.2f, 0.2f, 0.92f));

            // GameManager
            GameObject gmGo = new GameObject("GameManager");
            var gameManager = gmGo.AddComponent<GameManager>();
            var gmSo = new SerializedObject(gameManager);
            gmSo.FindProperty("_victoryPanel").objectReferenceValue = victoryPanel;
            gmSo.FindProperty("_gameOverPanel").objectReferenceValue = gameOverPanel;
            gmSo.FindProperty("_levelText").objectReferenceValue = tmpLevel;
            gmSo.FindProperty("_nextLevelButton").objectReferenceValue = nextBtn;
            gmSo.FindProperty("_retryButton").objectReferenceValue = retryBtn;
            gmSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static (GameObject, Button) CreateResultPanel(Transform parent, string name, string title, string buttonLabel, Color bgColor)
        {
            GameObject panelGo = new GameObject(name);
            panelGo.transform.SetParent(parent, false);
            var rect = panelGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(700f, 500f);

            var img = panelGo.AddComponent<Image>();
            img.color = bgColor;

            // Title
            GameObject titleGo = new GameObject("Text_Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -60f);
            titleRect.sizeDelta = new Vector2(650f, 120f);

            var tmpTitle = titleGo.AddComponent<TextMeshProUGUI>();
            tmpTitle.text = title;
            tmpTitle.fontSize = 52;
            tmpTitle.fontStyle = FontStyles.Bold;
            tmpTitle.alignment = TextAlignmentOptions.Center;
            tmpTitle.color = Color.white;

            // Button
            GameObject btnGo = new GameObject("Button_Action");
            btnGo.transform.SetParent(panelGo.transform, false);
            var btnRect = btnGo.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.anchoredPosition = new Vector2(0f, 60f);
            btnRect.sizeDelta = new Vector2(450f, 110f);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = Color.white;
            var btn = btnGo.AddComponent<Button>();

            // Button Text
            GameObject btnTextGo = new GameObject("Text_Label");
            btnTextGo.transform.SetParent(btnGo.transform, false);
            var btnTextRect = btnTextGo.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;

            var tmpBtn = btnTextGo.AddComponent<TextMeshProUGUI>();
            tmpBtn.text = buttonLabel;
            tmpBtn.fontSize = 40;
            tmpBtn.fontStyle = FontStyles.Bold;
            tmpBtn.alignment = TextAlignmentOptions.Center;
            tmpBtn.color = new Color(0.15f, 0.15f, 0.15f);

            panelGo.SetActive(false);
            return (panelGo, btn);
        }
    }
}
#endif
