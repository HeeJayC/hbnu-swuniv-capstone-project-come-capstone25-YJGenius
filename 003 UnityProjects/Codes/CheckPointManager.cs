using System.Collections.Generic;
using UnityEngine;

public class CheckPointManager : MonoBehaviour
{
    public static CheckPointManager Instance;

    [Header("체크포인트 오브젝트 리스트 (순서대로 할당)")]
    public List<GameObject> m_CheckPointList = new List<GameObject>();

    [Header("체크포인트 머티리얼 설정")]
    public Material mat_CurrentPoint;  // 현재 목표 포인트
    public Material mat_NextPoint;     // 다음 목표 포인트
    public Material mat_DisablePoint;  // 비활성 포인트

    private int currentIndex = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetPoint();
    }

    /// <summary>
    /// 다음 포인트로 넘어갈 때 호출
    /// </summary>
    public void NextPoint()
    {
        if (currentIndex < m_CheckPointList.Count - 1)
        {
            currentIndex++;
            SetPoint();
        }
        else
        {
            Debug.Log("모든 체크포인트를 통과했습니다!");
        }
    }

    /// <summary>
    /// 현재/다음/이전 포인트의 머티리얼 상태를 업데이트
    /// </summary>
    public void SetPoint()
    {
        for (int i = 0; i < m_CheckPointList.Count; i++)
        {
            Renderer rend = m_CheckPointList[i].GetComponent<Renderer>();

            if (i < currentIndex)
                rend.material = mat_DisablePoint;      // 이미 지난 포인트
            else if (i == currentIndex)
                rend.material = mat_CurrentPoint;      // 현재 목표
            else if (i == currentIndex + 1)
                rend.material = mat_NextPoint;         // 다음 목표 예고
            else
                rend.material = mat_DisablePoint;      // 나머지 비활성
        }
    }
}
