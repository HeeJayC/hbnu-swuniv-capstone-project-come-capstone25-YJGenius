using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using PA_DronePack;
using System.Collections.Generic;
using System.Collections;
// using Unity.Time;
/// <summary>
/// ML-Agents 기반 드론 에이전트 (안정화 버전)
/// - Goal/Obstacle 자동 인식 (손자 포함)
/// - 무한 로딩 방지
/// - Area 구조 안전 참조
/// - 관측/입력 축/안전장치/디버그 자동조종 보강
/// </summary>
public class Drone : Agent
{
    [Header("Debug AutoPilot Vertical")]
    [Tooltip("목표와 높이 차이(미터)를 입력(-1~1)으로 바꿀 스케일")]
    public float debugHeightGain = 0.35f;   // 1m 차이 -> 0.25 입력 정도

    [Tooltip("위/아래로 최대로 줄 수 있는 Lift 입력 크기")]
    public float debugMaxLift = 0.9f;       // 0.9 까지 위/아래

    [Tooltip("높이 차이가 클 때 전진을 얼마나 줄일지 (0이면 그대로, 1이면 완전 멈춤)")]
    public float debugHeightSlowdownFactor = 0.6f; // 60% 까지 감속

    [Tooltip("높이 차이를 정규화할 범위(이 값 이상이면 감속 최대로 적용)")]
    public float debugHeightSlowdownRange = 10f;   // 10m 이상 차이면 풀감속



    [Header("Debug AutoPilot Rotation")]
    public float debugRotSpeed = 5f;      // 드론이 진행 방향으로 도는 속도
    public float debugForwardSpeed = 1f;  // 항상 얼마나 앞으로 밀어줄지 (0~1)

    // ===== 튜닝 파라미터 =====
    [Header("Tuning")]

    [Header("Agent Speed")]
    public float forwardSpeed = 25f;    // 빠른 전진 (20~80 추천)
    public float strafeSpeed = 20f;
    public float liftSpeed = 15f;

    [Header("Debug")]
    
    public bool debugAutoPilot = false;  // 정책 우회 테스트용
    public float debugGainDrive = 1.5f;
    public float debugGainStrafe = 1.5f;
    public float debugGainLift = 1.0f;   // ✅ 누락되어 에러 발생한 변수

    public float arrivalRadius = 3.5f;          // 목표 도달 반경
    public float distanceBlowupFactor = 1.2f;   // 전역 폭주 방지 배수
    public float minPreDistForBlowup = 2.0f;    // 전역 폭주 최소 임계
    public int   warmupSteps = 10;              // 목표 설정 직후 유예 스텝
    public float overshootFactor = 1.5f;        // 최소거리 대비 오버슈트 배수(> 이면 종료)

    [Header("Scene References")]
    public Area area;
    public GameObject home;

    private PA_DroneController dcoScript;
    private Rigidbody agent_Rigidbody;
    private Transform agentTrans;
    private Transform homeTrans;

    // ===== Goal / Obstacle =====
    private GameObject[] goalRoots = new GameObject[0];
    public GameObject[] goals = new GameObject[0];
    public Transform[] goalTrans = new Transform[0];
    public int[] check = new int[0];
    public GameObject[] obstacle = new GameObject[0];
    public Transform[] ObstacleTrans = new Transform[0];

    // ===== 상태 변수 =====
    private int GoalSequence = 0;
    private int count = 0;
    private float preDist = 0f;
    private bool goHome = false;
    private bool isTouchedGoal = false;
    private bool isTouchedObstacle = false;
    private int stepSinceTargetSet = 0;

    // ✅ 현재 목표에 대한 최소 거리 기록(오버슈트 판정용)
    private float minDistToTarget = float.MaxValue;

    // ===== 통계 =====
    public int homeArrivedCount = 0;
    public int obstacleCollisionCount = 0;

    // ===== 결정 텀 =====
    public float DecisionWaitingTime = 5f;
    private float m_currentTime = 0f;
    private bool subscribed = false;

    // ========================= Initialize =========================
    private RayPerceptionSensorComponent3D _ray3d;

    [Header("Debug AutoPilot Avoidance")]
    [Tooltip("앞쪽/좌우/위쪽으로 장애물을 탐색하는 최대 거리")]
    public float avoidCheckDist = 70f;

    [Tooltip("회피 벡터를 목표 방향에 얼마나 섞을지 (값이 클수록 더 강하게 회피)")]
    public float avoidStrength = 50.0f;

    [Tooltip("SphereCast에 사용할 반지름")]
    public float avoidSphereRadius = 0.4f;

    [Tooltip("회피 대상으로 인식할 레이어 마스크 (예: Default, Obstacle 등)")]
    public LayerMask avoidLayerMask;


    private Vector3 initialPos;
    private Quaternion initialRot;

    public void HardResetForNewScene()
    {
        // 1) 내부 플래그/상태 초기화
        isTouchedGoal = false;
        isTouchedObstacle = false;
        goHome = false;
        GoalSequence = 0;
        count = 0;
        stepSinceTargetSet = 0;
        minDistToTarget = float.MaxValue;

        // 2) 물리 정지 + 위치/회전 초기 위치로 되돌리기
        if (agent_Rigidbody != null)
        {
            agent_Rigidbody.velocity = Vector3.zero;
            agent_Rigidbody.angularVelocity = Vector3.zero;
        }
        agentTrans.SetPositionAndRotation(initialPos, initialRot);

        // 3) Goal / 체크 배열 초기화
        ReactivateAllGoals();   // 이미 위에 정의해둔 함수 재사용

        // 4) 에피소드 강제 종료 → 다음 스텝에서 OnEpisodeBegin() 다시 호출됨
        debugAutoPilot = false;
        EndEpisode();

        Debug.Log("[Drone] 🧹 HardResetForNewScene 실행 완료");
    }

    public override void Initialize()
    {
        dcoScript = GetComponent<PA_DroneController>();
        agentTrans = transform;
        agent_Rigidbody = GetComponent<Rigidbody>();

        // ❌ 자동 생성 금지 — ▶ 수동으로 붙여둔 Sensor만 가져오기
        _ray3d = GetComponent<RayPerceptionSensorComponent3D>();
        if (_ray3d == null)
        {
            Debug.LogError("[Drone] ❌ RayPerceptionSensorComponent3D is missing! " +
                        "Add one manually to the Drone object.");
            return;
        }

        // Sensor 설정 업데이트만 수행
        _ray3d.SensorName = "ray3d";
        _ray3d.RayLength = 20f;
        _ray3d.SphereCastRadius = 0.2f;
        _ray3d.RayLayerMask = LayerMask.GetMask("Default", "Obstacle");
        _ray3d.RaysPerDirection = 7;
        _ray3d.MaxRayDegrees = 80f;
        _ray3d.ObservationStacks = 2;
        _ray3d.DetectableTags = new List<string> { "Goal", "Obstacle", "Home" };

        if (!subscribed && Academy.IsInitialized)
        {
            Academy.Instance.AgentPreStep += WaitTimeInference;
            subscribed = true;
        }

        if (home != null)
            homeTrans = home.transform;

        TryRefreshSceneRefs();
        Debug.Log($"[Drone] ✅ Initialize 완료: Goals={goals.Length}, Obstacles={obstacle.Length}, RaySensor=ON");

        initialPos = agentTrans.position;
        initialRot = agentTrans.rotation;
    }

    private void OnDisable()
    {
        if (subscribed && Academy.IsInitialized)
        {
            Academy.Instance.AgentPreStep -= WaitTimeInference;
            subscribed = false;
        }
    }

    // ===== 추가: 모든 Goal 활성화 + 색상/체크 초기화 =====
    private void ReactivateAllGoals()
    {
        if (goals == null) return;

        for (int i = 0; i < goals.Length; i++)
        {
            if (goals[i] != null)
                goals[i].SetActive(true);          // ✅ 비활성화된 체크포인트 복구

            if (check != null && i < check.Length)
                check[i] = 0;
        }
    }


    // ========================= Episode Begin =========================
    public override void OnEpisodeBegin()
    {
        StartCoroutine(DelayedAreaSetting());

        // 초기화
        count = 0;
        GoalSequence = 0;
        goHome = false;
        isTouchedGoal = false;
        isTouchedObstacle = false;
        stepSinceTargetSet = 0;
        minDistToTarget = float.MaxValue;

        // Area 초기화
        // if (area != null)
            // area.AreaSetting();

        // Goal / Obstacle 재수집
        bool success = TryRefreshSceneRefs();
        if (!success)
        {
            Debug.LogWarning("[Drone] ⚠️ Goal/Obstacle을 찾지 못했습니다. 대기 상태로 유지합니다.");
            return;
        }

        ReactivateAllGoals();

        // ✅ 시작 위치 이동 (위로 20m, 앞으로 10m)
        Vector3 startPos = agentTrans.position;
        agent_Rigidbody.angularVelocity = Vector3.zero;

        // 색상 초기화
        for (int i = 0; i < goals.Length; i++)
        {
            check[i] = 0;
            var rends = goalRoots[i].GetComponentsInChildren<Renderer>(true);
            // foreach (var r in rends) r.material.color = Color.red;
        }

        // 첫 번째 목표 설정
        GoalSequence = FindNearestUnvisited(agentTrans.position);
        if (GoalSequence < 0) GoalSequence = 0;

        preDist = Vector3.Distance(agentTrans.position, goalTrans[GoalSequence].position);
        minDistToTarget = preDist;                // ✅ 최소거리 초기화
        stepSinceTargetSet = 0;

        // ⭐ 여기 추가 ⭐
        // var firstTarget = goalTrans[GoalSequence];
        // Vector3 dir = (firstTarget.position - agentTrans.position).normalized;
        // agentTrans.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);

        agent_Rigidbody.velocity = Vector3.zero;
        agent_Rigidbody.angularVelocity = Vector3.zero;

        agentTrans.position = initialPos;
        agentTrans.rotation = initialRot;


        Debug.Log($"[Drone] 🚀 Episode 시작 | 드론 초기 이동 완료 | Goal={goals.Length}, Obstacle={obstacle.Length}, First={GoalSequence}");
        targetElapsedTime = 0f;

    }

    // ⭐ 드론을 씬의 초기 배치 위치/회전으로 되돌리는 메소드
    public void ResetToInitialTransform()
    {
        if (agent_Rigidbody != null)
        {
            agent_Rigidbody.velocity = Vector3.zero;
            agent_Rigidbody.angularVelocity = Vector3.zero;
        }

        agentTrans.SetPositionAndRotation(initialPos, initialRot);

        // 내부 상태 초기화 (선택)
        isTouchedGoal = false;
        isTouchedObstacle = false;
        goHome = false;
        GoalSequence = 0;

        // 목표 거리 초기화
        minDistToTarget = float.MaxValue;

        Debug.Log("[Drone] 🔄 ResetToInitialTransform 적용됨");
    }

    private IEnumerator DelayedAreaSetting()
    {
        yield return null; 
        if (area != null)
            area.AreaSetting(); 
    }

    // ========================= Observations =========================
    // public override void CollectObservations(VectorSensor sensor)
    // {
    //     var target = GetCurrentTarget();
    //     if (target == null)
    //     {
    //         sensor.AddObservation(Vector3.zero);
    //     }
    //     else
    //     {
    //         sensor.AddObservation(target.position - agentTrans.position);
    //     }

    //     sensor.AddObservation(agent_Rigidbody.velocity);
    //     sensor.AddObservation(agent_Rigidbody.angularVelocity);
    // }

    public override void CollectObservations(VectorSensor sensor)
    {
        var target = GetCurrentTarget();
        if (target == null)
        {
            sensor.AddObservation(Vector3.zero);              // 기존 그대로
            sensor.AddObservation(0f);
        }
        else
        {
            Vector3 toTarget = target.position - agentTrans.position;
            sensor.AddObservation(toTarget.normalized);       // ⭐ 추가 1: 정규화된 방향
            sensor.AddObservation(toTarget.magnitude);        // ⭐ 추가 2: 거리
        }

        // ⭐ 추가 3: 드론의 local orientation (정면/오른쪽/위)
        sensor.AddObservation(agentTrans.forward);
        sensor.AddObservation(agentTrans.right);
        sensor.AddObservation(agentTrans.up);

        // 기존 관측
        sensor.AddObservation(agent_Rigidbody.velocity);
        sensor.AddObservation(agent_Rigidbody.angularVelocity);
    }

    // 목표 도달 제한 시간(초)
    private float targetTimeLimit = 180f;

    // 목표 설정 이후 경과 시간
    private float targetElapsedTime = 0f;


    // ========================= Actions =========================
    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        var target = GetCurrentTarget();
        if (target == null) return;   // 목표 없으면 아무것도 안 함

        // --- 입력 취득 ---
        var act = actionBuffers.ContinuousActions;
        float inX = Mathf.Clamp(act[0], -1f, 1f); // Strafe (좌우, local X)
        float inY = Mathf.Clamp(act[1], -1f, 1f); // Lift   (상하, local Y)
        float inZ = Mathf.Clamp(act[2], -1f, 1f); // Drive  (전후, local Z)

        // --- 드론 구동 ---
        if (dcoScript != null)
        {
            if (debugAutoPilot)
            {
                DebugAutoPilotToward(target); // ✅ 정책 우회: 실제로 목표로 가는지 바로 검증
            }
            else
            {
                // ✅ 축 재매핑(전진=Z, 좌우=X, 상하=Y)
                dcoScript.StrafeInput(inX);
                dcoScript.LiftInput(inY);
                dcoScript.DriveInput(inZ);
            }
        }

        // 장애물 충돌 처리
        if (isTouchedObstacle)
        {
            AddReward(-5f);
            // Vector3 back = -agentTrans.forward * 100f;
            // agent_Rigidbody.velocity = back;
            obstacleCollisionCount++;
            Debug.Log("[Drone] 🚧 장애물 충돌");
            isTouchedObstacle = false;
            // EndEpisode();
            return;
        }

        // 거리 계산
        float distance = Vector3.Distance(target.position, agentTrans.position);
        AddReward(-0.001f); // 시간 패널티

        // ✅ 최소 거리 갱신
        if (distance < minDistToTarget)
            minDistToTarget = distance;

        // ✅ 전역 폭주 방지(워밍업 후)
        stepSinceTargetSet++;
        if (stepSinceTargetSet > warmupSteps)
        {
            float blowupThresh = Mathf.Max(preDist * distanceBlowupFactor, minPreDistForBlowup);
            if (distance > blowupThresh)
            {
                // AddReward(-2f);
                // Debug.Log($"[Drone] ⚠️ 너무 멀어짐 (d={distance:F2} > th={blowupThresh:F2}) → 재시작");
                Debug.Log($"[Drone] ⚠️ 너무 멀어짐 → 재시작");
                EndEpisode();
                return;
            }
        }

        // ✅ 오버슈트 종료(최소거리의 overshootFactor 배 이상 멀어지면 종료)
        if (stepSinceTargetSet > warmupSteps && distance > minDistToTarget * overshootFactor)
        {
            AddReward(-2f);
            Debug.Log($"[Drone] ⛔ 최소거리 이상으로 멀어짐 --> 오버슈트 종료 ");
            EndEpisode();
            return;
        }

        // ✅ 목표 도달 처리(가변 반경)
        // if (isTouchedGoal || distance <= arrivalRadius)
        if (isTouchedGoal)
        {
            check[GoalSequence] = 1;
            count++;
            AddReward(30f);
            isTouchedGoal = false;

            var rends = goalRoots[GoalSequence].GetComponentsInChildren<Renderer>(true);
            // foreach (var r in rends) r.material.color = Color.green;
            // Goal 없어짐
            // goals[GoalSequence].SetActive(false);


            int next = FindNearestUnvisited(agentTrans.position);
            if (next == -1)
            {
                goHome = true;
                AddReward(20f);
                Debug.Log("[Drone] ✅ 모든 Goal 도달 완료.");

                // ⭐ Goal → Home 목표 전환 시 거리 기준 초기화 ⭐
                preDist = Vector3.Distance(agentTrans.position, homeTrans.position);
                minDistToTarget = preDist;
                stepSinceTargetSet = 0;
            }

            else
            {
                GoalSequence = next;
                preDist = Vector3.Distance(agentTrans.position, goalTrans[next].position);
                minDistToTarget = preDist;    // ✅ 새 목표에 대한 최소거리 초기화
                stepSinceTargetSet = 0;       // ✅ 유예 스텝 리셋
            }
        }
        else
        {
            // ----------------------------------
            // (A) 거리 감소 보상
            // ----------------------------------
            float distDiff = preDist - distance;
            AddReward(distDiff * 0.0005f);   // ← 핵심 계수(0.03~0.08 조절 가능)

            // ----------------------------------
            // (B) 방향 정렬 보상 (forward vs target)
            // ----------------------------------
            Vector3 toTargetDir = (target.position - agentTrans.position).normalized;
            float dot = Vector3.Dot(agentTrans.forward, toTargetDir); // -1~1
            AddReward(dot * 0.0002f);

            // ----------------------------------
            // (C) 거리 기반 수렴 보상 (정규화)
            // ----------------------------------
            float normalizedDistance = Mathf.Clamp01(distance / 100f); // 0~100m 기준
            AddReward((1f - normalizedDistance) * 0.0001f);
        }

        if (StepCount > 2000) 
        {
            // EndEpisode();
        }

        // ======================= 20초 타임아웃 검사 =======================
        targetElapsedTime += Time.deltaTime;

        if (targetElapsedTime >= targetTimeLimit)
        {
            AddReward(-10f);
            Debug.Log("[Drone] ⏳ 20초 안에 체크포인트에 도달하지 못해 에피소드 종료");
            EndEpisode();
            return;
        }



    }

    // ========================= Trigger =========================
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Goal"))
        {
            // 현재 목표가 아니면 무시
            if (other.transform != goalTrans[GoalSequence])
                return;
            targetElapsedTime = 0f;
            isTouchedGoal = true;
        }

        if (other.CompareTag("Obstacle"))
            isTouchedObstacle = true;

        // ✅ 모든 Goal 완료 후 Home 으로 돌아오는 단계
        if (other.CompareTag("Home") && goHome)
        {
            AddReward(50f);
            homeArrivedCount++;
            Debug.Log("[Drone] 🏠 Home 도착 -> 에피소드 종료");

            EndEpisode();

            // if (!debugAutoPilot)       // 아래 2번 내용과 연결
            //     EndEpisode();
        }
    }


    // ========================= Scene 탐색 =========================
    private bool TryRefreshSceneRefs()
    {
        if (area == null)
        {
            Debug.LogError("[Drone] Area 참조가 없습니다.");
            return false;
        }

        Transform goalParent =
            (area.goalParent != null ? area.goalParent :
            area.transform.Find("Environment/Goal")) ??
            area.transform.Find("Goal");

        if (goalParent == null)
        {
            Debug.LogError("[Drone] Goal 부모를 찾지 못했습니다.");
            return false;
        }

        var all = goalParent.GetComponentsInChildren<Transform>(true);
        var list = new List<Transform>();
        foreach (var t in all)
        {
            var col = t.GetComponent<Collider>();
            bool nameOK = t.name.Equals("CheckPoint", System.StringComparison.OrdinalIgnoreCase);
            bool tagOK = t.CompareTag("Goal");
            if ((nameOK || tagOK) && col != null && col.isTrigger)
                list.Add(t);
        }

        int gCount = list.Count;
        goals = new GameObject[gCount];
        goalTrans = new Transform[gCount];
        goalRoots = new GameObject[gCount];
        check = new int[gCount];

        for (int i = 0; i < gCount; i++)
        {
            var trg = list[i];
            goals[i] = trg.gameObject;
            goalTrans[i] = trg;

            Transform root = trg;
            while (root.parent != null && root.parent != goalParent)
                root = root.parent;
            goalRoots[i] = root.gameObject;
            check[i] = 0;
        }

        Debug.Log($"[Drone] ✅ Goal 갱신 완료: {gCount}개 인식");

        // Obstacle도 갱신
        Transform obsParent =
            (area.obstacleParent != null ? area.obstacleParent :
            area.transform.Find("Environment/Obstacle")) ??
            area.transform.Find("Obstacle");

        if (obsParent != null)
        {
            var obsAll = obsParent.GetComponentsInChildren<Transform>(true);
            var obsList = new List<Transform>();
            foreach (var t in obsAll)
                if (t.CompareTag("Obstacle"))
                    obsList.Add(t);

            obstacle = new GameObject[obsList.Count];
            ObstacleTrans = new Transform[obsList.Count];
            for (int i = 0; i < obsList.Count; i++)
            {
                obstacle[i] = obsList[i].gameObject;
                ObstacleTrans[i] = obsList[i];
            }
            Debug.Log($"[Drone] ✅ Obstacle 갱신 완료: {obsList.Count}개 인식");
        }

        if (home != null)
            homeTrans = home.transform;

        return gCount > 0;
    }

    // ========================= Helper =========================
    private int FindNearestUnvisited(Vector3 pos)
    {
        int nearest = -1;
        float best = float.MaxValue;
        for (int i = 0; i < goalTrans.Length; i++)
        {
            if (check[i] == 1) continue;
            float d = Vector3.SqrMagnitude(goalTrans[i].position - pos);
            if (d < best)
            {
                best = d;
                nearest = i;
            }
        }
        return nearest;
    }

    private void DebugAutoPilotToward(Transform target)
    {
        // ======== 1) Terrain / 지형 감지 ========
        float desiredMinAltitude = 5.0f;        // 지면과 최소 5m 유지
        // float desiredMaxAltitude = 40.0f;       // 너무 높이 떠도 내려오기
        float desiredSlopeFollow = 15.0f;

        float altitude = float.MaxValue;
        float terrainY = -99999f;

        int groundMask = LayerMask.GetMask("Ground", "Terrain");

        if (Physics.Raycast(agentTrans.position, Vector3.down,
            out RaycastHit groundHit, 2000f, groundMask))
        {
            terrainY = groundHit.point.y;
            altitude = agentTrans.position.y - terrainY;

            if (altitude < desiredMinAltitude)
            {
                dcoScript.LiftInput(+1f);
                dcoScript.DriveInput(0f);
                return;
            }
        }

        // ======== 2) 목표 방향 계산 ========
        Vector3 toTarget = target.position - agentTrans.position;
        if (toTarget.sqrMagnitude < 1e-4f) return;

        Vector3 dirToTarget = toTarget.normalized;

        // 장애물 회피 벡터
        // Vector3 avoid = ComputeAvoidanceVector();
        var (avoidVec, bestWeight) = ComputeAvoidanceVector();
        Vector3 desiredDir = (dirToTarget + avoidVec * avoidStrength).normalized;

        // Vector3 desiredDir = (dirToTarget + avoid * avoidStrength).normalized;
        
        // ======== ⛔ 장애물에 너무 가까우면 강제 탈출 모드 ========
        // if (bestWeight > 0.65f)   // 0~1 중 0.65 이상은 '매우 가까움'
        // {
        //     // 1) 강하게 위로 상승
        //     float escapeUp = Mathf.Lerp(2f, 8f, bestWeight);

        //     // 2) 강하게 뒤로 빼기
        //     float escapeBack = Mathf.Lerp(4f, 12f, bestWeight);

        //     // 3) 좌우 랜덤으로 튕기기 (막힌 구조에서 탈출 용도)
        //     float escapeSide = (Random.value > 0.5f ? 1f : -1f) * Mathf.Lerp(2f, 6f, bestWeight);

        //     Vector3 escapeVel =
        //         agentTrans.up * escapeUp +
        //         -agentTrans.forward * escapeBack +
        //         agentTrans.right * escapeSide;

        //     agent_Rigidbody.velocity = escapeVel;
        //     return;   // ⛔ 강제 탈출 중엔 다른 로직 무시
        // }


        // ======== 3) 드론 회전(수평 Y축만 회전) ========
        Vector3 flatDir = new Vector3(desiredDir.x, 0f, desiredDir.z);
        if (flatDir.sqrMagnitude > 0.001f)
        {
            Quaternion q = Quaternion.LookRotation(flatDir, Vector3.up);
            agentTrans.rotation = Quaternion.Slerp(agentTrans.rotation, q,
                debugRotSpeed * Time.deltaTime);
        }

        // ======== 4) 로컬 기준 이동 벡터 계산 ========
        Vector3 localDir = agentTrans.InverseTransformDirection(desiredDir);


        // ======== 5) 고도 제어 (목표와의 높이 차이) ========
        float heightError = target.position.y - agentTrans.position.y;

        // 산 오르막에서는 고도 차이를 자연스럽게 따라가기
        float lift = Mathf.Clamp(heightError * 0.2f, -1f, 1f);

        // 지면이 너무 가까우면 lift 강제상승이 priority
        if (altitude < desiredMinAltitude + 2f)
            lift = Mathf.Max(lift, 0.8f);

        // forwardSpeed *= (1f - bestWeight * 0.5f);
        // forwardSpeed *= (1f - bestWeight);

        Vector3 v =
            agentTrans.forward * forwardSpeed +
            agentTrans.right * (localDir.x * strafeSpeed) +
            agentTrans.up * (lift * liftSpeed);

        agent_Rigidbody.velocity = v;
    }



    // ========================= Decision Timing =========================

    private (Vector3 avoid, float weight) ComputeAvoidanceVector()
    {
        Vector3 avoid = Vector3.zero;
        float bestWeight = 0f;
        Vector3 origin = agentTrans.position;

        // 앞으로 / 앞+오른쪽 / 앞+왼쪽 / 앞+위 대각선 방향으로 체크
        // Vector3[] dirs = new Vector3[]
        // {
        //     agentTrans.forward,
        //     (agentTrans.forward + agentTrans.right).normalized,
        //     (agentTrans.forward - agentTrans.right).normalized,
        //     (agentTrans.forward + agentTrans.up * 0.5f).normalized,
        //     (agentTrans.forward - agentTrans.up).normalized,  // 아래 방향 산/지면 체크

        // };
        Vector3[] dirs = new Vector3[]
        {
            agentTrans.forward,
            (agentTrans.forward + agentTrans.right).normalized,
            (agentTrans.forward - agentTrans.right).normalized,
            (agentTrans.forward + agentTrans.up).normalized,
            (agentTrans.forward - agentTrans.up).normalized,

            agentTrans.up,
            -agentTrans.up,
            agentTrans.right,
            -agentTrans.right
        };


        // float bestWeight = 0f;

        foreach (var d in dirs)
        {
            // int groundMask = LayerMask.GetMask("Default", "Ground", "Terrain");
            int avoidMask = LayerMask.GetMask("Default", "Obstacle", "Ground", "Terrain");

            if (Physics.SphereCast(
                    origin,
                    avoidSphereRadius,
                    d,
                    out RaycastHit hit,
                    avoidCheckDist,
                    // avoidLayerMask,
                    avoidMask,
                    QueryTriggerInteraction.Ignore))
            {
                // 가까울수록 가중치 크게 (0 ~ 1)
                float weight = 1f - (hit.distance / avoidCheckDist);

                // 장애물 반대 방향 (수평만 사용해서 옆으로 피하기)
                Vector3 away = (origin - hit.point);
                away.y = 0f;                  // ⬅⬅ 고도 변화는 빼기
                if (away.sqrMagnitude < 1e-6f)
                    continue;

                away.Normalize();

                // 가장 강한(가까운) 장애물 기준으로 회피
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    avoid = away * weight;
                }
            }
        }

        // return avoid; // 길이: 0 ~ 1 정도
        return (avoid, bestWeight);
    }

    public void WaitTimeInference(int action)
    {
        if (Academy.Instance.IsCommunicatorOn)
            RequestDecision();
        else
            RequestDecision(); // 단순화: 프레임 기반 추론
    }
    // ===== Helper Target =====
    private Transform GetCurrentTarget()
    {
        if (goHome && homeTrans != null)
        {
            // ✅ 모든 Goal 처리 후에는 Home 으로 귀환
            return homeTrans;
        }

        if (goalTrans == null || goalTrans.Length == 0)
            return null;

        return goalTrans[Mathf.Clamp(GoalSequence, 0, goalTrans.Length - 1)];
    }

    public int GetCheckedCount()
    {
        int count = 0;
        for (int i = 0; i < check.Length; i++)
            if (check[i] == 1) count++;
        return count;
    }

    public int GetTotalCount()
    {
        return check.Length;
    }


}
