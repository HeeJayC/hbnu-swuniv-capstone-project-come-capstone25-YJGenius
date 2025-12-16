using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class Goal_Collision : MonoBehaviour
{
    private Renderer goalRenderer;
    private bool isTouchedGoal = false;

    // Start is called before the first frame update
    void Start()
    {
        goalRenderer = GetComponent<Renderer>();
        if (goalRenderer != null)
            goalRenderer.material.color = Color.red; // 기본 색상
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isTouchedGoal && other.CompareTag("Agent")) // 드론에 Agent 태그가 붙어있다고 가정
        {
            isTouchedGoal = true;
            if (goalRenderer != null)
                goalRenderer.material.color = Color.green; // 도달 시 색상 변경

            // 필요하다면 Drone.cs에 신호를 보내거나, 점수/보상 처리
            // 예: other.GetComponent<Drone>().OnGoalReached(this.gameObject);
        }
    }
    public void OnEpisodeBegin()
    {
        if (goalRenderer != null)
            goalRenderer.material.color = Color.red;
        isTouchedGoal = false;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
