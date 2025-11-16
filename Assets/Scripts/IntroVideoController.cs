// IntroVideoController.cs
using UnityEngine;
using UnityEngine.Video; // VideoPlayer 컴포넌트를 사용하기 위해 필요
using UnityEngine.SceneManagement; // SceneTransitionManager를 사용하기 위해 필요 (실제 씬 전환은 SceneTransitionManager가 담당)

public class IntroVideoController : MonoBehaviour
{
    private VideoPlayer videoPlayer; // 이 오브젝트에 붙어있는 VideoPlayer 컴포넌트 참조

    void Start()
    {
        Debug.Log("[IntroVideoController] 초기화 시작!");
        // 이 GameObject에 붙어있는 VideoPlayer 컴포넌트를 가져옵니다.
        videoPlayer = GetComponent<VideoPlayer>();

        // VideoPlayer가 제대로 연결되었는지 확인
        if (videoPlayer == null)
        {
            Debug.LogError("IntroVideoController: VideoPlayer 컴포넌트를 찾을 수 없습니다!", this);
            return; // 컴포넌트 없으면 여기서 중단
        }

        // 동영상 재생이 끝났을 때 호출될 이벤트를 연결합니다.
        // videoPlayer.loopPointReached 이벤트는 동영상이 마지막 프레임에 도달하면 발생합니다.
        videoPlayer.loopPointReached += OnVideoEnd;

        // (디버깅용) 현재 동영상의 정보를 로그로 출력
        if (videoPlayer.clip != null)
        {
            Debug.Log($"[IntroVideoController] '{videoPlayer.clip.name}' 동영상 로드 완료. 길이: {videoPlayer.clip.length} 초.");
        }
        else
        {
            Debug.LogWarning("IntroVideoController: Video Clip이 할당되지 않았습니다! 동영상이 재생되지 않을 수 있습니다.", this);
        }
    }

    // 동영상 재생이 끝났을 때 호출되는 콜백 함수
    void OnVideoEnd(VideoPlayer vp)
    {
        Debug.Log("[IntroVideoController] 동영상 재생 완료! 메인 씬으로 전환합니다.");
        // 동영상 재생이 끝나면 SceneTransitionManager를 통해 메인 씬으로 전환을 요청합니다.
        // Time.timeScale이 0일 수 있으니, 씬 전환 전에는 1로 재설정합니다.
        Time.timeScale = 1f;
        SceneTransitionManager.LoadScene("MainScene"); // 전환할 메인 씬의 정확한 이름 "MainScene"
    }

    // 이 스크립트가 파괴될 때 이벤트 리스너를 해제하여 메모리 누수를 방지합니다.
    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoEnd; // 이벤트 연결 해제
        }
    }
}