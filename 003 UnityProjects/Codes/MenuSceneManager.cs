using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuSceneManager : MonoBehaviour
{
    // Best Record Time UI
    public TextMeshProUGUI bestRecord_Plane;
    public TextMeshProUGUI bestRecord_Forest;
    public TextMeshProUGUI bestRecord_City;
    public TextMeshProUGUI bestRecord_Arena;

    // Best Racer Name UI
    public TextMeshProUGUI bestRacer_Plane;
    public TextMeshProUGUI bestRacer_Forest;
    public TextMeshProUGUI bestRacer_City;
    public TextMeshProUGUI bestRacer_Arena;

    [Header("Scene Names (Build Settings와 동일하게)")]
    [SerializeField] private string planeSceneName = "01_PlainSimulation";
    [SerializeField] private string forestSceneName = "02_ForestSimulation";
    [SerializeField] private string citySceneName = "03_CitySimulation";
    [SerializeField] private string arenaSceneName = "04_ArenaSimulation";

    public static class PlayModeFlags
    {
        public static bool AutoStart = false;
    }

    private void Awake()
    {
        // 이제 UNITY_EDITOR 관련 코드는 필요 없음
    }

    private void Start()
    {
        UpdateAllBestRecordUI();
    }

    private void LoadGameScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[MenuSceneManager] sceneName이 비어 있습니다. 인스펙터에서 씬 이름을 설정하세요.");
            return;
        }

        if (RecordManager.Instance != null)
            Debug.Log("[MenuSceneManager] RecordManager OK");

        PlayModeFlags.AutoStart = true;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    public void GotoPlane() => LoadGameScene(planeSceneName);
    public void GotoForest() => LoadGameScene(forestSceneName);
    public void GotoCity() => LoadGameScene(citySceneName);
    public void GotoArena() => LoadGameScene(arenaSceneName);

    // -----------------------------
    // Update UI for Best Records
    // -----------------------------
    private void UpdateAllBestRecordUI()
    {
        UpdateRecord(bestRecord_Plane, bestRacer_Plane, planeSceneName);
        UpdateRecord(bestRecord_Forest, bestRacer_Forest, forestSceneName);
        UpdateRecord(bestRecord_City, bestRacer_City, citySceneName);
        UpdateRecord(bestRecord_Arena, bestRacer_Arena, arenaSceneName);
    }

    private void UpdateRecord(TextMeshProUGUI timeUI, TextMeshProUGUI racerUI, string mapName)
    {
        if (timeUI == null || racerUI == null || RecordManager.Instance == null)
            return;

        float best = RecordManager.Instance.GetBestTime(mapName);
        string bestRacer = PlayerPrefs.GetString("BestRacer_" + mapName, "");

        if (best == float.MaxValue)
            timeUI.text = "< None >";
        else
            timeUI.text = FormatTime(best);

        racerUI.text = string.IsNullOrEmpty(bestRacer) ? "< None >" : bestRacer;
    }

    private string FormatTime(float t)
    {
        int min = (int)(t / 60f);
        int sec = (int)(t % 60f);
        int ms = (int)((t - (int)t) * 1000f);
        return $"{min:00}:{sec:00}:{ms:000}";
    }

    public void ResetAllRecords()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("All records cleared.");

        UpdateAllBestRecordUI();
    }
}
