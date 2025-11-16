// SceneTransitionManager.cs
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 로드에 필요
using System.Collections;          // 코루틴 사용에 필요

public class SceneTransitionManager : MonoBehaviour
{
    // Inspector에서 연결할 페이드 패널의 Animator 컴포넌트
    [SerializeField] private Animator fadePanelAnimator;
    // 페이드 인/아웃 애니메이션의 길이 (초)
    [SerializeField] private float fadeDuration = 1.0f; // 애니메이션 클립 길이와 일치시켜야 합니다.

    // 전환할 다음 씬 이름을 저장하는 static 변수
    private static string s_NextSceneName; // s_ 접두사는 static 변수임을 나타내는 관례

    public static void LoadScene(string sceneName)
    {
        Debug.Log($"[SceneTransition] 씬 전환 요청: '{sceneName}' -> TransitionScene 로드 시작");
        s_NextSceneName = sceneName; // 다음 씬 이름을 static 변수에 저장
        SceneManager.LoadScene("TransitionScene"); // TransitionScene을 로드
    }

    void Start()
    {
        Debug.Log($"[SceneTransition] TransitionScene Start! '{gameObject.name}'");

        // 페이드 패널 Animator가 연결되었는지 확인
        if (fadePanelAnimator != null)
        {
            // TransitionScene이 로드되면 화면은 이미 완전히 검은색(불투명한 패널)으로 가려진 상태입니다.
            // 이제 그 검은색 패널을 서서히 투명하게 만들어서 실제 다음 씬을 보여주기 위해 "FadeInTrigger"를 발동합니다.
            fadePanelAnimator.SetTrigger("FadeInTrigger");
            Debug.Log("[SceneTransition] FadeInTrigger 발동!");
        }
        else
        {
            Debug.LogError("SceneTransitionManager: fadePanelAnimator가 연결되지 않았습니다!", this);
        }

        // 페이드 인 애니메이션이 끝날 때까지 기다린 후, 실제 다음 씬을 비동기적으로 로드합니다.
        StartCoroutine(LoadNextSceneAfterFadeIn());
    }

    // FadeIn 애니메이션 재생 시간을 기다린 후 다음 씬을 로드하는 코루틴
    IEnumerator LoadNextSceneAfterFadeIn()
    {
        // fadeDuration 만큼 기다립니다. 이 시간은 Fade_In 애니메이션 클립의 길이와 같아야 합니다.
        yield return new WaitForSeconds(fadeDuration);

        // 로드할 다음 씬 이름이 유효한지 확인
        if (!string.IsNullOrEmpty(s_NextSceneName))
        {
            Debug.Log($"[SceneTransition] FadeIn 완료, 실제 씬 로드 시작: '{s_NextSceneName}'");
            // 다음 씬을 비동기적으로 로드 (게임이 멈추지 않도록)
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(s_NextSceneName);

            // 씬 로드가 완료될 때까지 기다립니다.
            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            Debug.Log($"[SceneTransition] 씬 로드 완료: '{s_NextSceneName}'");
        }
        else
        {
            Debug.LogWarning("[SceneTransition] 다음 씬 이름(s_NextSceneName)이 설정되지 않았습니다. TransitionScene이 대상 없이 로드되었습니다.", this);
        }
    }
}