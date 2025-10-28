using UnityEngine;

public class GameManagerLogic : MonoBehaviour
{
    public static GameManagerLogic Instance { get; private set; }

    [Header("Player / Teleport Target")]
    public Player player;

    [Header("Spawn Settings")]
    public Transform[] spawnPoints;          // 0,1,2...
    public int defaultEggsPerCheckpoint = 3; // 새 체크포인트 도달 시 알 개수
    public float respawnLiftY = 0.2f;        // 리스폰 시 살짝 띄워 겹침 방지

    [Header("Void Kill")]
    public bool enableVoidKill = true;
    public float voidY = -50f;

    [Header("BGM")]
    public AudioSource bgmSource;
    public AudioClip[] spawnBGMs; // 인덱스 = 스폰 인덱스
    public AudioClip deathBGM;    // 사망 브금
    public AudioClip deathSFX;    // 사망 효과음

    [Header("Death FX")]
    public DeathEffectController deathFX;    // 사망 연출 컨트롤러

    public int CurrentCheckpointIndex { get; private set; } = 0;

    private int[] eggsLeft;
    private Transform _playerTr;
    private bool _isDead = false;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.GetComponent<Player>();
        }
        _playerTr = player ? player.transform : null;

        if (spawnPoints == null) spawnPoints = new Transform[0];
        eggsLeft = new int[spawnPoints.Length];
        for (int i = 0; i < eggsLeft.Length; i++) eggsLeft[i] = defaultEggsPerCheckpoint;

        CurrentCheckpointIndex = 0;
        PlayBGMForCheckpoint(0);
    }

    private void Update()
    {
        if (!enableVoidKill || _playerTr == null) return;
        if (_playerTr.position.y < voidY) KillPlayer();
    }

    // ---------- Checkpoint ----------
    public void RegisterCheckpoint(int index)
    {
        if (!IsValidIndex(index)) return;
        if (index == CurrentCheckpointIndex) return;   // 같은 곳 재진입은 무시

        CurrentCheckpointIndex = index;
        eggsLeft[index] = defaultEggsPerCheckpoint;    // 새 도달 시 리필
        PlayBGMForCheckpoint(index);
    }

    // ---------- Death / Respawn ----------
    public void KillPlayer()
    {
        if (_isDead) return;
        _isDead = true;

        int cp = CurrentCheckpointIndex;
        if (IsValidIndex(cp))
        {
            int before = eggsLeft[cp];

            if (before <= 1)
            {
                // 이번 죽음으로 알이 0이 되면 즉시 롤백
                eggsLeft[cp] = 0;
                CurrentCheckpointIndex = Mathf.Max(cp - 1, 0);
            }
            else
            {
                // 아직 1개 이상 남음 → 현재 스폰에서 알만 줄이고 리스폰
                eggsLeft[cp] = before - 1;
            }
        }

        StartDeathSequence(); // (연출 → TeleportToSpawn(CurrentCheckpointIndex) 호출)
    }

    private void StartDeathSequence()
    {
        // 죽음 브금/효과음
        if (bgmSource)
        {
            bgmSource.Stop();
            if (deathBGM) { bgmSource.clip = deathBGM; bgmSource.loop = true; bgmSource.Play(); }
        }
        if (deathSFX && _playerTr) AudioSource.PlayClipAtPoint(deathSFX, _playerTr.position);

        // 사망 연출 → 끝나면 OnDeathEffectFinished 호출
        if (deathFX) deathFX.BeginDeathEffect(OnDeathEffectFinished);
        else OnDeathEffectFinished();
    }

    private void OnDeathEffectFinished()
    {
        // 연출(암전/왜곡) 완료 → 스폰 이동
        TeleportToSpawn(CurrentCheckpointIndex);
        PlayBGMForCheckpoint(CurrentCheckpointIndex);

        // 스폰 이동이 끝난 뒤 화면/카메라/스케일 복원 시작
        if (deathFX) deathFX.StartRestore();

        _isDead = false;
    }

    public void TeleportToSpawn(int index)
    {
        if (!IsValidIndex(index) || player == null) return;
        var pos = spawnPoints[index].position + Vector3.up * respawnLiftY;
        player.Teleport(pos);
    }

    // ---------- Utils ----------
    private bool IsValidIndex(int i) => (i >= 0 && i < spawnPoints.Length && spawnPoints[i] != null);

    private void PlayBGMForCheckpoint(int index)
    {
        if (!bgmSource || spawnBGMs == null) return;
        if (index < 0 || index >= spawnBGMs.Length) return;
        var clip = spawnBGMs[index];
        if (!clip) return;
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.loop = true;
        bgmSource.Play();
    }
}
