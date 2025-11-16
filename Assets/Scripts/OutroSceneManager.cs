// OutroSceneManager.cs
using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class OutroSceneManager : MonoBehaviour
{
    // 애니메이션 재생 시간 (Inspector에서 조절 가능)
    [SerializeField] private float animationDuration = 5.0f;

    void Start()
    {
        Debug.Log($"[OutroScene] OutroSceneManager Start! '{gameObject.name}'");
        StartCoroutine(LoadCreditSceneAfterDelay(animationDuration));
    }

    IEnumerator LoadCreditSceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Debug.Log("[OutroScene] Outro Animation Finished! Loading Credit Scene...");
        SceneTransitionManager.LoadScene("CreditScene");
    }
}