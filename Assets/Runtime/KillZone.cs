using UnityEngine;

[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    public bool killOnEnter = true;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!killOnEnter) return;

        bool isPlayer = other.CompareTag("Player") ||
                        (other.transform.parent && other.transform.parent.CompareTag("Player"));
        if (!isPlayer) return;

        if (GameManagerLogic.Instance == null) return;
        GameManagerLogic.Instance.KillPlayer();
    }
}
