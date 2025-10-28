using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    public bool killOnEnter = true;
    public bool verbose = true;

    private Collider _col;
    private Rigidbody _rb;

    private void Reset()
    {
        _col = GetComponent<Collider>();
        _col.isTrigger = true; // 트리거 권장
    }

    private void Awake()
    {
        _col = GetComponent<Collider>();
        if (!_col.isTrigger)
            Debug.LogWarning($"[KillZone:{name}] Collider.isTrigger=false. true 권장.");

        // 트리거 이벤트 보장을 위해 kinematic Rigidbody 부착
        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;
            if (verbose) Debug.Log($"[KillZone:{name}] Kinematic Rigidbody attached for trigger events.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!killOnEnter) return;

        if (verbose)
            Debug.Log($"[KillZone:{name}] OnTriggerEnter -> other={other.name}, tag={other.tag}");

        // 자식 콜라이더 대비: 부모도 확인
        bool isPlayer = other.CompareTag("Player") ||
                        (other.transform.parent && other.transform.parent.CompareTag("Player"));
        if (!isPlayer) return;

        if (GameManagerLogic.Instance == null)
        {
            Debug.LogError("[KillZone] GameManagerLogic.Instance is NULL");
            return;
        }
        GameManagerLogic.Instance.KillPlayer($"KillZone:{name}");
    }
}
