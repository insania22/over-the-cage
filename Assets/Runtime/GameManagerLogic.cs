using UnityEngine;

public class GameManagerLogic : MonoBehaviour
{
    public static GameManagerLogic Instance { get; private set; }

    [Header("Debug")]
    public bool verbose = true;

    [Header("Player / Teleport Target")]
    public Player player; // Player.cs (아래) 오브젝트를 Inspector에 연결 권장

    [Header("Spawn(부화장) 설정")]
    public Transform[] spawnPoints;          // 0,1,2… 빈 오브젝트를 씬에 두고 넣기
    public int defaultEggsPerCheckpoint = 3; // 각 체크포인트 최초 도달 시 리필 개수

    [Header("추락(공허) 판정")]
    public bool enableVoidKill = true;
    public float voidY = -50f;

    [Header("Gate Suppress")]
    [Tooltip("텔레포트 직후 SpawnGate 등록을 무시하는 시간(초)")]
    public float gateSuppressDuration = 0.5f;
    private float _gateSuppressUntil = 0f;

    [Tooltip("텔레포트 시 살짝 위로 띄워 트리거 겹침을 줄이는 오프셋")]
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
        // DontDestroyOnLoad(this.gameObject); // 씬 전환 보존하려면 주석 해제

        // Player 참조
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.GetComponent<Player>();
        }
        if (player == null)
        {
            Debug.LogError("[GameManager] Player reference is NULL. Inspector에 Player를 연결하거나 Player 태그를 확인하세요.");
        }
        else
        {
            _playerTransform = player.transform;
        }

        // Spawn 초기화
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[GameManager] spawnPoints가 비었습니다. 최소 1개 이상 할당하세요.");
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

    // ===== 외부에서 호출하는 API =====

    /// 텔레포트 직후 SpawnGate 등록 허용 여부
    public bool CanAcceptCheckpointFromGate() => Time.time >= _gateSuppressUntil;

    /// SpawnGate에서 "성공 등록"된 경우에만 리필. 같은 스폰을 재진입하면 리필 없음.
    public void RegisterCheckpoint(int index)
    {
        if (!IsValidIndex(index)) return;

        // 텔레포트 직후 억제 시간에는 무시
        if (!CanAcceptCheckpointFromGate())
        {
            if (verbose) Debug.Log($"[GameManager] RegisterCheckpoint({index}) ignored (gate suppressed)");
            return;
        }

        bool isSameAsCurrent = (index == CurrentCheckpointIndex);
        if (!isSameAsCurrent)
        {
            CurrentCheckpointIndex = index;
            eggsLeft[index] = defaultEggsPerCheckpoint; // ★ 새롭게 도달했을 때만 리필
            if (verbose) Debug.Log($"[GameManager] RegisterCheckpoint -> {index} (NEW). eggs reset to {eggsLeft[index]}");
        }
        else
        {
            if (verbose) Debug.Log($"[GameManager] RegisterCheckpoint -> {index} (same as current). NO refill.");
        }

        DumpState("RegisterCheckpoint");
    }

    /// 특정 스폰으로 텔레포트(필요 시만). 기본적으로 makeCurrent=false 권장.
    public void TeleportToSpawn(int index, bool makeCurrent = false, bool killVelocity = true)
    {
        if (!IsValidIndex(index)) return;
        if (makeCurrent) RegisterCheckpoint(index);

        var basePos = spawnPoints[index].position;
        var pos = basePos + Vector3.up * respawnLiftY; // 겹침 방지용

        TeleportToPosition(pos, killVelocity);

        // 텔레포트 이후 잠시 게이트 무시
        SuppressGatesForAWhile();

        if (verbose) Debug.Log($"[GameManager] TeleportToSpawn index={index}, makeCurrent={makeCurrent}, pos={pos}");
    }

    /// 외부(다른 스크립트)에서 egg_spawn 값을 적용하고 싶을 때
    public void ApplyExternalEggSpawn(int egg_spawn, bool teleport = true, bool makeCurrent = true)
    {
        if (!IsValidIndex(egg_spawn)) return;
        if (makeCurrent) RegisterCheckpoint(egg_spawn);
        if (teleport) TeleportToSpawn(egg_spawn, makeCurrent: false);
        if (verbose) Debug.Log($"[GameManager] ApplyExternalEggSpawn({egg_spawn}) teleport={teleport}, makeCurrent={makeCurrent}");
    }

    /// 사망 처리: 현재 체크포인트 알 차감. 0이면 이전 체크포인트로 롤백 후 그곳에서 부활(리필 없음).
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
            CurrentCheckpointIndex = prev;                 // ★ 이전 스폰으로 롤백
            TeleportToSpawn(prev, makeCurrent: false);     // ★ 거기서 부활 (리필 없음)
        }

        DumpState("KillPlayer");
    }

    // ===== 내부 유틸 =====

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
        player.Teleport(pos); // Player → PlayerCharacter.SetPosition 호출
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
