using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using DroneController;

public class SimulationSceneManager : MonoBehaviour
{
    [Header("Menu Scene Name (Build Settings 이름과 동일)")]
    [SerializeField] private string menuSceneName = "TestMenu";

    private bool isPaused = false;
    private bool isStarting = false;   // 중복 시작 방지용

    [Header("Pause UI Panel")]
    public GameObject pausePanel;

    // Start UI
    public GameObject startPanel;
    public GameObject countdownTextObj;
    public GameObject failPanel;
    public GameObject clearPanel;
    public GameObject bestPanel;

    public Drone droneAgent;
    public DroneMovement playerDroneMovement;

    [Header("Racing Record")]
    public bool isRacing = false;
    public float recordTime = 0f;

    private void Awake()
    {
        if (string.IsNullOrEmpty(menuSceneName))
        {
            Debug.LogWarning("[SimulationSceneManager] menuSceneName 이 비어 있습니다. " +
                             "인스펙터에서 메뉴 씬 이름을 설정해주세요.");
        }
    }

    private void Start()
    {
        // 씬 들어올 때: 일단 자동조종 OFF, 입력 막기 (버튼 눌러서 시작하도록)
        if (droneAgent != null)
            droneAgent.debugAutoPilot = true;

        if (playerDroneMovement != null)
            playerDroneMovement.BlockInput = true;
    }

    private void Update()
    {
        if (isRacing)
            recordTime += Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    // ==============================
    // START 버튼
    // ==============================
    public void OnClickStartButton()
    {
        if (isStarting) return;     // 이미 시작 중이면 무시
        isStarting = true;

        if (startPanel != null)
            startPanel.SetActive(false);

        // 여기서는 오직 흐름 코루틴만 호출
        StartCoroutine(RestartFlowRoutine());
    }

    // 카운트다운은 "글자만" 담당
    private IEnumerator StartCountdownRoutine()
    {
        var textUI = countdownTextObj.GetComponent<TMPro.TextMeshProUGUI>();
        countdownTextObj.SetActive(true);

        textUI.text = "3";
        yield return new WaitForSecondsRealtime(1f);

        textUI.text = "2";
        yield return new WaitForSecondsRealtime(1f);

        textUI.text = "1";
        yield return new WaitForSecondsRealtime(1f);

        textUI.text = "Start!!";
        yield return new WaitForSecondsRealtime(1f);

        countdownTextObj.SetActive(false);

        // 레이싱 시간 측정 시작
        isRacing = true;
        recordTime = 0f;
    }

    // ==============================
    // PAUSE
    // ==============================
    public void TogglePause()
    {
        isPaused = !isPaused;

        if (pausePanel != null)
            pausePanel.SetActive(isPaused);

        Time.timeScale = isPaused ? 0f : 1f;

        Cursor.visible = isPaused;
        Cursor.lockState = isPaused ? CursorLockMode.None : CursorLockMode.Locked;
    }

    // ==============================
    // RESTART 버튼
    // ==============================
    public void ReStart()
    {
        if (isStarting) return;   // 이미 뭔가 시작 중이면 또 안 누르게
        isStarting = true;

        Time.timeScale = 1f;
        isPaused = false;
        pausePanel?.SetActive(false);
        failPanel?.SetActive(false);
        clearPanel?.SetActive(false);
        bestPanel?.SetActive(false);

        StartCoroutine(RestartFlowRoutine());
    }

    // 체크포인트 활성화
    private void ReactivateAllCheckpoints()
    {
        var allGoals = GameObject.FindGameObjectsWithTag("Goal");

        foreach (var g in allGoals)
            g.SetActive(true);
    }

    // 리지드바디 완전 정지
    private void FreezeRigidbodies(bool freeze)
    {
        if (droneAgent != null)
        {
            var rb = droneAgent.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = freeze;
                if (freeze)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }

        if (playerDroneMovement != null)
        {
            var rb = playerDroneMovement.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = freeze;
                if (freeze)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
        }
    }

    private IEnumerator RestartFlowRoutine()
    {
        // HomeManager 초기화
        HomeManager home = FindObjectOfType<HomeManager>();
        if (home != null)
            home.ResetState();

        // 입력 막기
        if (playerDroneMovement != null) playerDroneMovement.BlockInput = true;
        if (droneAgent != null) droneAgent.debugAutoPilot = false;

        // 1) 위치/회전 초기화
        if (droneAgent != null) droneAgent.ResetToInitialTransform();
        if (playerDroneMovement != null) playerDroneMovement.ResetToInitialTransform();

        // 2) 리지드바디 완전 정지 + 바로 Freeze ON
        FreezeRigidbodies(true);

        // ⭐ 3) AI 내부 상태 초기화 (Freeze 중에 해야 밀리지 않음)
        if (droneAgent != null)
        {
            droneAgent.EndEpisode();
            droneAgent.OnEpisodeBegin();  // force, pid 등 내부 로직 모두 이때 초기화됨
        }

        // 4) 안정화를 위한 시간 확보
        yield return new WaitForSecondsRealtime(0.3f);

        // 5) Freeze 해제
        FreezeRigidbodies(false);

        // 6) 체크포인트 초기화
        ReactivateAllCheckpoints();
        var playerCheck = FindObjectOfType<CheckPlayerCheckpoint>();
        if (playerCheck != null) playerCheck.ResetCheckpoints();

        // 7) 카운트다운
        yield return StartCoroutine(StartCountdownRoutine());

        // 8) 이동 허용
        if (playerDroneMovement != null) playerDroneMovement.BlockInput = false;
        if (droneAgent != null) droneAgent.debugAutoPilot = true;

        isStarting = false;
    }

    // ==============================
    // SCENE RELOAD / MENU
    // ==============================
    private IEnumerator RestartSceneSafely()
    {
        yield return new WaitForEndOfFrame();

        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene, LoadSceneMode.Single);
    }

    public void GotoMenu()
    {
        Time.timeScale = 1f;
        StartCoroutine(LoadMenuSafely());
    }

    private IEnumerator LoadMenuSafely()
    {
        yield return new WaitForEndOfFrame();

        if (!string.IsNullOrEmpty(menuSceneName))
            SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
        else
            Debug.LogError("[SimulationSceneManager] Menu Scene Name is empty! 인스펙터에서 menuSceneName을 설정하세요.");
    }
}
