// OutroTrigger.cs
using UnityEngine;
using UnityEngine.SceneManagement; // SceneTransitionManager를 사용하기 위해 필요

public class OutroTrigger : MonoBehaviour
{
    // 씬 전환이 여러 번 발생하지 않도록 방지하는 플래그
    private bool hasTriggered = false;

    // 다른 콜라이더가 이 트리거에 진입했을 때 호출됩니다.
    private void OnTriggerEnter(Collider other)
    {
        // Debug.Log($"[OutroTrigger] OnTriggerEnter 호출됨! 닿은 오브젝트: {other.gameObject.name}", this); // 디버깅용

        // 이미 트리거가 발생했다면 더 이상 진행하지 않습니다.
        if (hasTriggered)
        {
            Debug.Log("[OutroTrigger] 이미 트리거되어 무시합니다.");
            return;
        }

        // 진입한 콜라이더가 "Player" 태그를 가지고 있는지 확인합니다.
        if (other.CompareTag("Player"))
        {
            hasTriggered = true; // 플래그를 true로 설정하여 중복 실행 방지

            Debug.Log($"[OutroTrigger] 플레이어 '{other.gameObject.name}'가 'OutroTriggerObject'에 닿았습니다! OutroAnimationScene으로 전환 시작.", this);

            // 필요한 경우, 플레이어 움직임을 멈추거나 UI를 비활성화하는 등의 추가 작업을 할 수 있습니다.
            // 예를 들어, 플레이어 컨트롤 스크립트에 접근하여 움직임을 정지시킬 수 있습니다.
            // PlayerMovement playerMovement = other.GetComponent<PlayerMovement>();
            // if (playerMovement != null) playerMovement.DisableMovement();

            // SceneTransitionManager의 static 메서드를 통해 "OutroAnimationScene"으로 전환을 요청합니다.
            Time.timeScale = 1f; // 혹시 게임이 일시정지 상태였다면, 씬 전환을 위해 Time.timeScale을 1로 복구
            SceneTransitionManager.LoadScene("OutroAnimationScene");
        }
        else
        {
            // 플레이어가 아닌 다른 오브젝트가 트리거에 닿았을 때 (디버깅용)
            Debug.Log($"[OutroTrigger] '{other.gameObject.name}'이(가) 'OutroTriggerObject'에 닿았지만 플레이어가 아닙니다. 무시합니다.");
        }
    }
}