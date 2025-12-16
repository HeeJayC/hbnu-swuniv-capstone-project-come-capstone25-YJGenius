using UnityEngine;

public class RecordManager : MonoBehaviour
{
    public static RecordManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 각 맵의 최고 기록 가져오기
    public float GetBestTime(string mapName)
    {
        return PlayerPrefs.GetFloat("BestTime_" + mapName, float.MaxValue);
    }

    // 기록이 더 좋다면 저장
    public bool TrySaveRecord(string mapName, float newTime)
    {

        float oldBest = GetBestTime(mapName);

        Debug.Log($"[RecordManager] {mapName} 기록 체크 → 기존: {FormatTime(oldBest)} / 신규: {FormatTime(newTime)}");

        if (newTime < oldBest)
        {
            PlayerPrefs.SetFloat("BestTime_" + mapName, newTime);
            PlayerPrefs.Save();

            Debug.Log($"<color=green>[RecordManager] 신기록 달성! 저장됨 → {FormatTime(newTime)}</color>");
            return true;
        }

        Debug.Log($"<color=yellow>[RecordManager] 기존 기록이 더 좋음 → 기록 유지</color>");
        return false;
    }

    private string FormatTime(float t)
    {
        if (t == float.MaxValue)
            return "--:--:---";  // 기록 없음 표시

        int min = (int)(t / 60f);
        int sec = (int)(t % 60f);
        int ms = (int)((t - (int)t) * 1000f);

        return $"{min:00}:{sec:00}:{ms:000}";
    }


}
