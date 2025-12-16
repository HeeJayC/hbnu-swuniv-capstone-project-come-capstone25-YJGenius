using DroneController;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class HomeManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject failPanel;
    public GameObject clearPanel;
    public TextMeshProUGUI recordText;

    [Header("New Record UI")]
    public GameObject newRecordPanel;         // 신기록 UI
    public GameObject saveRecordPanel;        // 레이서명 저장 UI
    public TMP_InputField inputRacerName;     // 이름 입력 InputField

    [Header("References")]
    public SimulationSceneManager sceneManager;
    public CheckPlayerCheckpoint playerCheckpoint;
    public DroneMovement playerMovement;
    public Drone agentDrone;
    public RecordManager recordManager;

    private bool isFinished = false;

    private void Awake()
    {
        if (sceneManager == null)
            sceneManager = FindObjectOfType<SimulationSceneManager>();

        if (recordManager == null)
            recordManager = RecordManager.Instance;

        if (playerMovement == null)
            playerMovement = FindObjectOfType<DroneMovement>();

        if (playerCheckpoint == null)
            playerCheckpoint = FindObjectOfType<CheckPlayerCheckpoint>();

        if (agentDrone == null)
            agentDrone = FindObjectOfType<Drone>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isFinished) return;

        // ---------------- Agent 도착 실패 ----------------
        if (other.CompareTag("Agent"))
        {
            isFinished = true;
            Time.timeScale = 0f;

            if (playerMovement != null)
                playerMovement.BlockInput = true;

            if (failPanel != null)
                failPanel.SetActive(true);

            Debug.Log("<color=red>[HomeManager] Agent reached → FAIL</color>");
            return;
        }

        // ---------------- Player 도착 ----------------
        if (other.CompareTag("Player"))
        {
            if (playerCheckpoint != null && playerCheckpoint.allChecked)
            {
                isFinished = true;
                HandlePlayerClear();
            }
        }
    }

    private void HandlePlayerClear()
    {
        Time.timeScale = 0f;

        if (playerMovement != null)
            playerMovement.BlockInput = true;

        if (clearPanel != null)
            clearPanel.SetActive(true);

        float time = sceneManager.recordTime;
        string formatted = FormatTime(time);

        if (recordText != null)
            recordText.text = formatted;

        // 현재 맵 이름 (씬 이름)
        string mapName = SceneManager.GetActiveScene().name;

        // ---------------- 기록 저장 로직 ----------------
        if (recordManager != null)
        {
            bool isBest = recordManager.TrySaveRecord(mapName, time);

            if (isBest)
            {
                Debug.Log("<color=green>[HomeManager] New Record!</color>");

                // 신기록 UI 띄우기
                if (newRecordPanel != null)
                    newRecordPanel.SetActive(true);
            }
        }

        Debug.Log("<color=green>[HomeManager] Player Clear Complete</color>");
    }

    // ---------------- 이름 저장 버튼 ----------------
    public void SaveRacerName()
    {
        string mapName = SceneManager.GetActiveScene().name;

        // Input field must exist
        if (inputRacerName == null)
        {
            Debug.LogWarning("SaveRacerName: inputRacerName is null. Not closing panel.");
            return;
        }

        string racer = inputRacerName.text;

        // Empty name → do not save, do not close panel
        if (string.IsNullOrEmpty(racer))
        {
            Debug.Log("SaveRacerName: Name is empty. Panel stays open.");
            return;
        }

        // Save record
        PlayerPrefs.SetString("BestRacer_" + mapName, racer);
        PlayerPrefs.Save();
        Debug.Log($"Racer '{racer}' saved for map {mapName}.");

        // Close panel only after valid save
        if (saveRecordPanel != null)
            saveRecordPanel.SetActive(false);
    }


    private string FormatTime(float t)
    {
        int minutes = (int)(t / 60f);
        int seconds = (int)(t % 60f);
        int ms = (int)((t - (int)t) * 1000);

        return $"{minutes:00}:{seconds:00}:{ms:000}";
    }

    public void ResetState()
{
    isFinished = false;

    // 혹시 UI 패널이 이미 열려 있으면 모두 닫아줌
    if (failPanel != null) failPanel.SetActive(false);
    if (clearPanel != null) clearPanel.SetActive(false);
    if (newRecordPanel != null) newRecordPanel.SetActive(false);
    if (saveRecordPanel != null) saveRecordPanel.SetActive(false);
}

}
