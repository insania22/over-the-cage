using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SpawnGate : MonoBehaviour
{
    [Tooltip("닿았을 때 등록할 스폰 인덱스")]
    public int targetSpawnIndex = 0;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        bool isPlayer = other.CompareTag("Player") ||
                        (other.transform.parent && other.transform.parent.CompareTag("Player"));
        if (!isPlayer) return;

        if (GameManagerLogic.Instance == null) return;
        GameManagerLogic.Instance.RegisterCheckpoint(targetSpawnIndex); // 등록만(텔레포트 없음)
    }
}
