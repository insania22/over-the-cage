using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpawnGate : MonoBehaviour
{
    [Tooltip("닿았을 때 '등록'할 스폰 인덱스 (텔레포트 없음)")]
    public int targetSpawnIndex = 0;

    public bool verbose = true;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // 자식 콜라이더 대비: 부모 태그도 검사
        bool isPlayer = other.CompareTag("Player") ||
                        (other.transform.parent && other.transform.parent.CompareTag("Player"));

        if (verbose)
        {
            string parentName = other.transform.parent ? other.transform.parent.name : "(no parent)";
            Debug.Log($"[SpawnGate:{name}] Trigger by {other.name}, tag={other.tag}, parent={parentName}, isPlayer={isPlayer}, idx={targetSpawnIndex}");
        }

        if (!isPlayer) return;
        if (GameManagerLogic.Instance == null) return;

        // 텔레포트 직후 억제 중이면 무시
        if (!GameManagerLogic.Instance.CanAcceptCheckpointFromGate())
        {
            if (verbose) Debug.Log($"[SpawnGate:{name}] Gate suppressed, ignore register.");
            return;
        }

        // 체크포인트 등록 (같은 인덱스 재진입 시 리필 안 함)
        GameManagerLogic.Instance.RegisterCheckpoint(targetSpawnIndex);
        if (verbose) Debug.Log($"[SpawnGate:{name}] RegisterCheckpoint({targetSpawnIndex})");
    }
}
