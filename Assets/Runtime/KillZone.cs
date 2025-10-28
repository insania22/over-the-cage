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
        _col.isTrigger = true; // Ʈ���� ����
    }

    private void Awake()
    {
        _col = GetComponent<Collider>();
        if (!_col.isTrigger)
            Debug.LogWarning($"[KillZone:{name}] Collider.isTrigger=false. true ����.");

        // Ʈ���� �̺�Ʈ ������ ���� kinematic Rigidbody ����
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

        // �ڽ� �ݶ��̴� ���: �θ� Ȯ��
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
