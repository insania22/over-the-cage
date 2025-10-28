using UnityEngine;

public class GameManagerLogic : MonoBehaviour
{
    public static GameManagerLogic Instance { get; private set; }

    [Header("Debug")]
    public bool verbose = true;

    [Header("Player / Teleport Target")]
    public Player player; // Player.cs (�Ʒ�) ������Ʈ�� Inspector�� ���� ����

    [Header("Spawn(��ȭ��) ����")]
    public Transform[] spawnPoints;          // 0,1,2�� �� ������Ʈ�� ���� �ΰ� �ֱ�
    public int defaultEggsPerCheckpoint = 3; // �� üũ����Ʈ ���� ���� �� ���� ����

    [Header("�߶�(����) ����")]
    public bool enableVoidKill = true;
    public float voidY = -50f;

    [Header("Gate Suppress")]
    [Tooltip("�ڷ���Ʈ ���� SpawnGate ����� �����ϴ� �ð�(��)")]
    public float gateSuppressDuration = 0.5f;
    private float _gateSuppressUntil = 0f;

    [Tooltip("�ڷ���Ʈ �� ��¦ ���� ��� Ʈ���� ��ħ�� ���̴� ������")]
    public float respawnLiftY = 0.2f;

    public int CurrentCheckpointIndex { get; private set; } = 0;
    [SerializeField] private int[] eggsLeft;

    private Transform _playerTransform;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            if (verbose) Debug.LogWarning("[GameManager] Duplicate instance detected. Destroying this.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad(this.gameObject); // �� ��ȯ �����Ϸ��� �ּ� ����

        // Player ����
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.GetComponent<Player>();
        }
        if (player == null)
        {
            Debug.LogError("[GameManager] Player reference is NULL. Inspector�� Player�� �����ϰų� Player �±׸� Ȯ���ϼ���.");
        }
        else
        {
            _playerTransform = player.transform;
        }

        // Spawn �ʱ�ȭ
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[GameManager] spawnPoints�� ������ϴ�. �ּ� 1�� �̻� �Ҵ��ϼ���.");
            spawnPoints = new Transform[0];
        }
        eggsLeft = new int[spawnPoints.Length];
        for (int i = 0; i < eggsLeft.Length; i++)
            eggsLeft[i] = defaultEggsPerCheckpoint;

        if (verbose) DumpState("Awake");
    }

    private void Update()
    {
        if (!enableVoidKill || _playerTransform == null) return;

        if (_playerTransform.position.y < voidY)
        {
            if (verbose) Debug.Log($"[GameManager] Void kill triggered at Y={_playerTransform.position.y} (thr={voidY})");
            KillPlayer("VoidFall");
        }
    }

    // ===== �ܺο��� ȣ���ϴ� API =====

    /// �ڷ���Ʈ ���� SpawnGate ��� ��� ����
    public bool CanAcceptCheckpointFromGate() => Time.time >= _gateSuppressUntil;

    /// SpawnGate���� "���� ���"�� ��쿡�� ����. ���� ������ �������ϸ� ���� ����.
    public void RegisterCheckpoint(int index)
    {
        if (!IsValidIndex(index)) return;

        // �ڷ���Ʈ ���� ���� �ð����� ����
        if (!CanAcceptCheckpointFromGate())
        {
            if (verbose) Debug.Log($"[GameManager] RegisterCheckpoint({index}) ignored (gate suppressed)");
            return;
        }

        bool isSameAsCurrent = (index == CurrentCheckpointIndex);
        if (!isSameAsCurrent)
        {
            CurrentCheckpointIndex = index;
            eggsLeft[index] = defaultEggsPerCheckpoint; // �� ���Ӱ� �������� ���� ����
            if (verbose) Debug.Log($"[GameManager] RegisterCheckpoint -> {index} (NEW). eggs reset to {eggsLeft[index]}");
        }
        else
        {
            if (verbose) Debug.Log($"[GameManager] RegisterCheckpoint -> {index} (same as current). NO refill.");
        }

        DumpState("RegisterCheckpoint");
    }

    /// Ư�� �������� �ڷ���Ʈ(�ʿ� �ø�). �⺻������ makeCurrent=false ����.
    public void TeleportToSpawn(int index, bool makeCurrent = false, bool killVelocity = true)
    {
        if (!IsValidIndex(index)) return;
        if (makeCurrent) RegisterCheckpoint(index);

        var basePos = spawnPoints[index].position;
        var pos = basePos + Vector3.up * respawnLiftY; // ��ħ ������

        TeleportToPosition(pos, killVelocity);

        // �ڷ���Ʈ ���� ��� ����Ʈ ����
        SuppressGatesForAWhile();

        if (verbose) Debug.Log($"[GameManager] TeleportToSpawn index={index}, makeCurrent={makeCurrent}, pos={pos}");
    }

    /// �ܺ�(�ٸ� ��ũ��Ʈ)���� egg_spawn ���� �����ϰ� ���� ��
    public void ApplyExternalEggSpawn(int egg_spawn, bool teleport = true, bool makeCurrent = true)
    {
        if (!IsValidIndex(egg_spawn)) return;
        if (makeCurrent) RegisterCheckpoint(egg_spawn);
        if (teleport) TeleportToSpawn(egg_spawn, makeCurrent: false);
        if (verbose) Debug.Log($"[GameManager] ApplyExternalEggSpawn({egg_spawn}) teleport={teleport}, makeCurrent={makeCurrent}");
    }

    /// ��� ó��: ���� üũ����Ʈ �� ����. 0�̸� ���� üũ����Ʈ�� �ѹ� �� �װ����� ��Ȱ(���� ����).
    public void KillPlayer(string reason = "Unknown")
    {
        if (!IsValidIndex(CurrentCheckpointIndex))
        {
            Debug.LogWarning("[GameManager] KillPlayer: invalid CurrentCheckpointIndex.");
            return;
        }

        int cp = CurrentCheckpointIndex;
        int before = eggsLeft[cp];

        if (before > 0)
        {
            eggsLeft[cp] = before - 1;
            if (verbose) Debug.Log($"[GameManager] KillPlayer({reason}) -> CP {cp} eggs {before} -> {eggsLeft[cp]} (stay CP {cp})");
            TeleportToSpawn(cp, makeCurrent: false);
        }
        else
        {
            int prev = Mathf.Max(cp - 1, 0);
            if (verbose) Debug.Log($"[GameManager] KillPlayer({reason}) -> eggs=0, rollback to CP {prev}");
            CurrentCheckpointIndex = prev;                 // �� ���� �������� �ѹ�
            TeleportToSpawn(prev, makeCurrent: false);     // �� �ű⼭ ��Ȱ (���� ����)
        }

        DumpState("KillPlayer");
    }

    // ===== ���� ��ƿ =====

    private void SuppressGatesForAWhile()
    {
        _gateSuppressUntil = Time.time + gateSuppressDuration;
    }

    private bool IsValidIndex(int index)
    {
        bool ok = (index >= 0 && index < spawnPoints.Length && spawnPoints[index] != null);
        if (!ok) Debug.LogWarning($"[GameManager] Invalid spawn index {index} (count={spawnPoints.Length}) or null Transform.");
        return ok;
    }

    private void TeleportToPosition(Vector3 pos, bool killVelocity)
    {
        if (player == null)
        {
            Debug.LogError("[GameManager] Teleport failed: Player is null");
            return;
        }
        player.Teleport(pos); // Player �� PlayerCharacter.SetPosition ȣ��
        if (verbose) Debug.Log($"[GameManager] TeleportToPosition {pos} (killVelocity={killVelocity})");
    }

    private void DumpState(string where)
    {
        if (!verbose) return;
        string eggs = (eggsLeft != null) ? string.Join(",", eggsLeft) : "(null)";
        Debug.Log($"[GameManager] [{where}] CP={CurrentCheckpointIndex}, eggs=[{eggs}], spawns={spawnPoints.Length}");
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (spawnPoints == null) return;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            var t = spawnPoints[i];
            if (t == null) continue;

            Gizmos.color = (i == CurrentCheckpointIndex) ? Color.green : Color.cyan;
            Gizmos.DrawSphere(t.position + Vector3.up * 0.2f, 0.25f);
            UnityEditor.Handles.color = Gizmos.color;
            UnityEditor.Handles.Label(t.position + Vector3.up * 0.8f, $"Spawn[{i}]");
        }
    }
#endif
}
