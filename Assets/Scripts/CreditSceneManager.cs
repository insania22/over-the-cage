// CreditSceneManager.cs
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class CreditSceneManager : MonoBehaviour
{
    // 크레딧 표시 시간 (Inspector에서 조절 가능)
    [SerializeField] private float displayDuration = 10.0f;

    void Start()
    {
        Debug.Log($"[CreditScene] CreditSceneManager Start! '{gameObject.name}'");
        StartCoroutine(ReturnToMainMenuAfterDelay(displayDuration));
        // 크레딧 씬에서는 마우스 커서를 보여주는 것이 일반적입니다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    IEnumerator ReturnToMainMenuAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log("[CreditScene] Credits Finished! Returning to Main Menu...");
        SceneTransitionManager.LoadScene("MainMenu");
    }

    // 아무 키나 누르면 바로 메인 메뉴로 돌아가는 기능 추가 (선택 사항)
    void Update()
    {
        if (Input.anyKeyDown)
        {
            StopAllCoroutines(); // 자동 전환 코루틴 중지
            Debug.Log("[CreditScene] Any key pressed! Returning to Main Menu...");
            SceneTransitionManager.LoadScene("MainMenu");
        }
    }
}