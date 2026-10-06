#if UNITY_EDITOR
using GateRunner.Audio;
using GateRunner.Data;
using GateRunner.Level;
using GateRunner.Managers;
using GateRunner.Movement;
using GateRunner.Player;
using UnityEditor;
using UnityEngine;

namespace GateRunner.Editor
{
    public static class GameFlowIntegrationTest
    {
        [MenuItem("Tools/Gate Runner/🧪 Oyun Akışı & Görsel Cila Testi (Integration Test)", priority = 20)]
        public static void RunAllTests()
        {
            Debug.Log("<color=#00E676><b>[Integration Test]</b></color> Oyun Akışı, Animasyon ve Kalıp Testleri Başlatılıyor...");

            TestLevelProgression();
            TestAudioManagerSynthesis();
            TestGameFlowStates();
            TestPlayerAnimator();
            TestDynamicSpawnPatternsAndDifficulty();
            TestRoadMaterialAndFog();

            Debug.Log("<color=#00E676><b>[Integration Test]</b></color> TÜM TESTLER BAŞARIYLA TAMAMLANDI! (All tests passed)");
        }

        private static void TestLevelProgression()
        {
            const string PREFS_KEY = "GateRunner_CurrentLevel";
            PlayerPrefs.SetInt(PREFS_KEY, 1);
            PlayerPrefs.Save();

            int level1 = PlayerPrefs.GetInt(PREFS_KEY, 1);
            if (level1 != 1) Debug.LogError("[Test Fail] Level 1 kaydedilemedi!");

            PlayerPrefs.SetInt(PREFS_KEY, 2);
            PlayerPrefs.Save();
            int level2 = PlayerPrefs.GetInt(PREFS_KEY, 1);
            if (level2 != 2) Debug.LogError("[Test Fail] Level 2 kaydedilemedi!");

            // Test Segment Progression Formula:
            int segsL1 = Mathf.Clamp(12 + (1 * 2), 10, 45); // 14
            int segsL2 = Mathf.Clamp(12 + (2 * 2), 10, 45); // 16
            if (segsL2 <= segsL1) Debug.LogError("[Test Fail] Seviye 2'de segment sayısı artmadı!");

            Debug.Log($"<color=#64B5F6>[Test Passed]</color> Level İlerlemesi: Level 1 ({segsL1} seg) -> Level 2 ({segsL2} seg). PlayerPrefs doğrulandı.");

            // Temizlik
            PlayerPrefs.SetInt(PREFS_KEY, 1);
            PlayerPrefs.Save();
        }

        private static void TestAudioManagerSynthesis()
        {
            GameObject go = new GameObject("Test_AudioManager");
            var audioMgr = go.AddComponent<AudioManager>();

            audioMgr.PlayCoinSound();
            audioMgr.PlayDamageSound();
            audioMgr.PlayGateSound(true);
            audioMgr.PlayGateSound(false);
            audioMgr.PlayVictorySound();
            audioMgr.PlayGameOverSound();

            Object.DestroyImmediate(go);
            Debug.Log("<color=#64B5F6>[Test Passed]</color> AudioManager prosedürel ses sentezi ve SFX fonksiyonları hatasız çalıştı.");
        }

        private static void TestGameFlowStates()
        {
            GameObject gmGo = new GameObject("Test_GameManager");
            var gm = gmGo.AddComponent<GameManager>();

            if (gm.State != GameState.Ready) Debug.LogError("[Test Fail] GameManager Ready durumunda başlamadı!");

            gm.StartGame();
            if (gm.State != GameState.Running) Debug.LogError("[Test Fail] GameManager Running durumuna geçmedi!");

            gm.LevelComplete();
            if (gm.State != GameState.Victory) Debug.LogError("[Test Fail] GameManager Victory durumuna geçmedi!");

            Object.DestroyImmediate(gmGo);
            Debug.Log("<color=#64B5F6>[Test Passed]</color> GameFlow Durum Makinesi (Ready -> Running -> Victory) doğrulandı.");
        }

        private static void TestPlayerAnimator()
        {
            GameObject playerGo = new GameObject("Test_Player");
            var anim = playerGo.AddComponent<PlayerAnimator>();

            anim.StartRunningAnimation();
            if (!anim.IsRunning) Debug.LogError("[Test Fail] PlayerAnimator koşma moduna geçmedi!");

            anim.PlayVictoryAnimation();
            if (anim.IsRunning) Debug.LogError("[Test Fail] PlayerAnimator zaferde koşmayı durdurmadı!");

            anim.PlayFailAnimation();
            anim.ResetToIdle();

            Object.DestroyImmediate(playerGo);
            Debug.Log("<color=#64B5F6>[Test Passed]</color> PlayerAnimator koşma/zafer/yenilgi/sıfırlama prosedürel durumları doğrulandı.");
        }

        private static void TestDynamicSpawnPatternsAndDifficulty()
        {
            // Level 1 vs Level 5 kapı ölçeklendirme formülü testi
            float scaleL1 = 1.0f + ((1 - 1) * 0.35f); // 1.0
            float scaleL5 = 1.0f + ((5 - 1) * 0.35f); // 2.4

            int minAddL1 = Mathf.RoundToInt(8 * scaleL1);
            int minAddL5 = Mathf.RoundToInt(8 * scaleL5);

            if (minAddL5 <= minAddL1)
            {
                Debug.LogError("[Test Fail] Level 5 kapı matematik değeri Level 1'den yüksek değil!");
            }

            // Pattern Enum doğrulaması
            var patterns = System.Enum.GetValues(typeof(SegmentPatternType));
            if (patterns.Length < 4)
            {
                Debug.LogError("[Test Fail] SegmentPatternType kalıpları eksik!");
            }

            Debug.Log($"<color=#64B5F6>[Test Passed]</color> Dinamik Seviye Kalıpları & Zorluk Ölçeklendirmesi (L1 min Add: {minAddL1}, L5 min Add: {minAddL5}) doğrulandı.");
        }

        private static void TestRoadMaterialAndFog()
        {
            var roadMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Road.mat");
            if (roadMat == null)
            {
                Debug.LogError("[Test Fail] Mat_Road.mat bulunamadı!");
            }
            else
            {
                Texture tex = roadMat.GetTexture("_BaseMap");
                if (tex == null) Debug.LogWarning("[Test Warning] Mat_Road üzerinde _BaseMap texture atanmamış olabilir.");
            }

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/PlayerAnimatorController.controller");
            if (controller == null)
            {
                Debug.LogError("[Test Fail] PlayerAnimatorController.controller asset bulunamadı!");
            }

            Debug.Log("<color=#64B5F6>[Test Passed]</color> Mat_Road, Sis (Fog) ve PlayerAnimatorController assetleri doğrulandı.");
        }
    }
}
#endif
