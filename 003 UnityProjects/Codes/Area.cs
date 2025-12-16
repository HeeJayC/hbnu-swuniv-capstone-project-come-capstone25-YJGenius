using UnityEngine;
using Unity.MLAgents;
using System;
using System.Collections.Generic;

public class Area : MonoBehaviour
{
    public GameObject DroneAgent;

    // (선택) 부모 빈 오브젝트 직접 지정
    public Transform goalParent;      // ex) Plane/Goal
    public Transform obstacleParent;  // ex) Plane/Obstacle

    // ====== Goal 스캔 옵션 ======
    [Header("Goal Scan Options")]
    public string goalTriggerName = "CheckPoint";  // 손자 오브젝트 이름
    public string goalTag = "Goal";                // 트리거에 붙일 태그(권장)
    public bool   requireTriggerCollider = true;   // 트리거 콜라이더 필수 여부

    // 자동 채움 대상
    public GameObject[] Goals = new GameObject[0];     // 실제 트리거(=CheckPoint)
    public GameObject[] Obstacle = new GameObject[0];

    Vector3 areaInitPos;
    Vector3 droneInitPos;
    Quaternion droneInitRot;

    EnvironmentParameters m_ResetParams;

    public Transform AreaTrans;
    public Transform DroneTrans;
    public Transform[] GoalTrans = new Transform[0];
    public Transform[] ObstacleTrans = new Transform[0];

    private Rigidbody DroneAgent_Rigidbody;

    private void Awake()
    {
        AreaTrans = transform;
        DroneTrans = DroneAgent.transform;
        DroneAgent_Rigidbody = DroneAgent.GetComponent<Rigidbody>();

        // 초기 위치 저장 (씬 처음 배치된 위치!)
        areaInitPos = AreaTrans.position;
        droneInitPos = DroneTrans.position;
        droneInitRot = DroneTrans.rotation;
    }

    void Start()
    {
        // 부모가 비어있으면 이름으로 찾아보기
        if (goalParent == null)      goalParent = transform.Find("CheckPoint");
        if (obstacleParent == null)  obstacleParent = transform.Find("Obstacle");

        // ✅ Goal 손자까지 스캔
        ScanGoalsDeep();

        // ✅ Obstacle (기존처럼 1단계 자식만 수집)
        if (obstacleParent != null)
        {
            int n = obstacleParent.childCount;
            Obstacle = new GameObject[n];
            ObstacleTrans = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                var child = obstacleParent.GetChild(i);
                Obstacle[i] = child.gameObject;
                ObstacleTrans[i] = child;
            }
        }
        else
        {
            if (Obstacle != null && Obstacle.Length > 0)
            {
                ObstacleTrans = new Transform[Obstacle.Length];
                for (int i = 0; i < Obstacle.Length; i++) ObstacleTrans[i] = Obstacle[i].transform;
            }
            else
            {
                Debug.LogWarning("[Area] Obstacle 부모를 찾지 못했고 Obstacle 배열도 비어있습니다.");
            }
        }

        Debug.Log($"[Area] Goals {Goals.Length}개, Obstacles {Obstacle.Length}개 자동 등록 완료");
            // 초기 위치 저장 (씬 처음 로드된 위치)

        droneInitPos = DroneTrans.position;
        droneInitRot = DroneTrans.rotation;

        // Goal 및 Obstacle 스캔
        ScanGoalsDeep();
        // ScanObstaclesDeep();
    }

    /// <summary>
    /// Goal 손자까지 훑어 'CheckPoint'(또는 Tag=Goal) 트리거를 Goals/GoalTrans에 채운다.
    /// </summary>
    private void ScanGoalsDeep() 
    {
        if (goalParent != null)
        {
            var all = goalParent.GetComponentsInChildren<Transform>(true);
            var list = new List<Transform>();

            foreach (var t in all)
            {
                bool nameMatch = string.Equals(t.name, goalTriggerName, StringComparison.OrdinalIgnoreCase);
                bool tagMatch  = (!string.IsNullOrEmpty(goalTag) && t.CompareTag(goalTag));
                if (!(nameMatch || tagMatch)) continue;

                if (requireTriggerCollider)
                {
                    var col = t.GetComponent<Collider>();
                    if (col == null || !col.isTrigger) continue;
                }
                list.Add(t);
            }

            int n = list.Count;
            Goals = new GameObject[n];
            GoalTrans = new Transform[n];
            for (int i = 0; i < n; i++)
            {
                Goals[i] = list[i].gameObject;  // 실제 충돌 트리거 오브젝트(=CheckPoint)
                GoalTrans[i] = list[i];
            }

            if (n == 0)
                Debug.LogWarning("[Area] Goal 아래에서 조건에 맞는 CheckPoint/Goal 태그 트리거를 찾지 못했습니다.");
        }
        else
        {
            // 인스펙터 수동 세팅 보조
            if (Goals != null && Goals.Length > 0)
            {
                GoalTrans = new Transform[Goals.Length];
                for (int i = 0; i < Goals.Length; i++) GoalTrans[i] = Goals[i].transform;
            }
            else
            {
                Debug.LogWarning("[Area] Goal 부모를 찾지 못했고 Goals 배열도 비어있습니다.");
            }
        }
    }

    public void AreaSetting()
    {
        // 드론 초기화
        DroneAgent_Rigidbody.velocity = Vector3.zero;
        DroneAgent_Rigidbody.angularVelocity = Vector3.zero;
        DroneTrans.position = droneInitPos;
        DroneTrans.rotation = droneInitRot;

        // (랜덤 재배치 등을 했다면) 다시 스캔
        ScanGoalsDeep();

        Debug.Log("목표 지점 위치 초기화 & 재스캔 완료");
    }
}
