using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckPointController : MonoBehaviour
{

    private void OnTriggerEnter(UnityEngine.Collider other)
    {
        if (other.transform.tag == "Player")
        {
            Debug.Log("체크포인트!!");
            // 매니저 스크립트에 신호 전달 SetNextPoint()
            ClearCheckPoint();
        }
    }

    public void ClearCheckPoint()
    {
        // Renderer 컴포넌트 가져오기
        Renderer renderer = this.GetComponent<Renderer>();
        if (renderer != null)
        {
            // 빨강 (R=1, G=0, B=0) + 알파=100/255 ≈ 0.39
            renderer.material.color = new Color(1f, 0f, 0f, 100f / 255f);
        }
    }


}
