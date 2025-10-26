using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimationController : MonoBehaviour
{
    [Header("References")]
    public PlayerCharacter playerCharacter;
    public Animator animator;

    [Header("Blend Settings")]
    public float smoothing = 0.1f;
    private float currentSpeed;

    private bool lastGrounded;
    private bool isInAir;

    [Header("Slide thresholds")]
    public float stopSpeedThreshold = 0.5f;   // 정지로 볼 속도 임계값 (Animator 조건과 맞추기)

    // ====== ▶ 추가: 랜덤 Idle 브레이크 설정 ======
    [Header("Idle Break Settings")]
    public float idleSpeedThreshold = 0.1f;   // '정지'로 판정할 속도
    public float idleMinInterval = 3f;      // 최소 대기 시간(초)
    public float idleMaxInterval = 7f;      // 최대 대기 시간(초)

    // Idle 브레이크 State 이름들(Animator 상의 정확한 스테이트 이름과 일치해야 함)
    public string[] idleBreakStates = new string[] { "Idle_A", "Idle_B", "Idle_C" };

    private float idleTimer;
    private float nextIdleTime;
    private bool playingIdleBreak;
    // ==========================================

    void Reset()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (playerCharacter == null) playerCharacter = GetComponent<PlayerCharacter>();
    }

    // ====== ▶ 추가: 첫 타이밍 초기화 ======
    void Start()
    {
        nextIdleTime = Random.Range(idleMinInterval, idleMaxInterval);
    }
    // =======================================

    void Update()
    {
        if (playerCharacter == null || animator == null) return;

        var state = playerCharacter.GetState();

        // 1) 수평 속도 -> Speed
        Vector3 planar = Vector3.ProjectOnPlane(state.Velocity, Vector3.up);
        float targetSpeed = planar.magnitude;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime / smoothing);
        animator.SetFloat("Speed", currentSpeed);

        // 2) Ground / Jump
        bool isGrounded = state.Grounded;
        animator.SetBool("IsGround", isGrounded);

        if (lastGrounded && !isGrounded)
        {
            animator.SetBool("IsJump", true);
            isInAir = true;
            animator.SetBool("IsGround", false);
        }
        if (!lastGrounded && isGrounded)
        {
            animator.SetBool("IsJump", false);
            animator.SetBool("IsGround", true);
            isInAir = false;
        }
        if (isInAir && !isGrounded)
        {
            animator.SetBool("IsGround", false);
        }

        // 3) Crouch
        animator.SetBool("IsCrouch", state.Stance == Stance.Crouch);

        // 4) Slide 플래그
        bool isSliding = (state.Stance == Stance.Slide);
        animator.SetBool("IsSliding", isSliding);

        // 5) Ice 감지 (PlayerCharacter.Motor 필요)
        bool onIce = false;
        var motor = playerCharacter.Motor;
        if (motor != null && motor.GroundingStatus.FoundAnyGround)
        {
            var ground = motor.GroundingStatus.GroundCollider;
            if (ground != null)
                onIce = (ground.gameObject.layer == LayerMask.NameToLayer("Ice"));
        }
        animator.SetBool("IsIce", onIce);

        // ====== ▶ 추가: 랜덤 Idle 브레이크 처리 ======
        HandleRandomIdle(state);
        // ============================================

        lastGrounded = isGrounded;
    }

    // ====== ▶ 추가 메서드: 랜덤 Idle 브레이크 로직 ======
    private void HandleRandomIdle(CharacterState state)
    {
        // 현재 프레임의 애니메이터 상태
        var si = animator.GetCurrentAnimatorStateInfo(0);

        // 진짜 'Idle'로 간주할 조건: 서 있고, 지면에 있고, 속도 거의 0, 점프/슬라이드/크라우치 아님
        bool isTrueIdle =
            state.Grounded &&
            state.Stance == Stance.Stand &&
            currentSpeed < idleSpeedThreshold &&
            !animator.GetBool("IsJump") &&
            !animator.GetBool("IsCrouch") &&
            !animator.GetBool("IsSliding");

        // 지금 Idle 브레이크 클립(Idle_A/B/C 등) 안에 있는가?
        bool inBreakState = IsInAnyIdleBreakState(si);

        if (!isTrueIdle)
        {
            // Idle이 아니면 타이머와 플래그 리셋
            idleTimer = 0f;
            if (!si.IsName("std")) playingIdleBreak = false; // 다른 상태면 리셋
            return;
        }

        // 브레이크 재생 중이면 끝날 때까지 대기
        if (inBreakState)
        {
            playingIdleBreak = true;
            return;
        }

        // std로 돌아왔으면 다음 랜덤 타이밍 예약
        if (playingIdleBreak && si.IsName("std"))
        {
            playingIdleBreak = false;
            idleTimer = 0f;
            nextIdleTime = Random.Range(idleMinInterval, idleMaxInterval);
        }

        // 평온한 std 상태에서 타이머 누적
        if (!playingIdleBreak && si.IsName("std"))
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= nextIdleTime && idleBreakStates != null && idleBreakStates.Length > 0)
            {
                int pick = Random.Range(0, idleBreakStates.Length);

                // Animator 전이 조건: IdleIndex == pick AND IdleBreak(trigger)
                animator.SetInteger("IdleIndex", pick);
                animator.SetTrigger("IdleBreak");

                playingIdleBreak = true;
                // 다음 타이밍은 std로 복귀했을 때 갱신
            }
        }
    }

    private bool IsInAnyIdleBreakState(AnimatorStateInfo si)
    {
        if (idleBreakStates == null) return false;
        for (int i = 0; i < idleBreakStates.Length; i++)
        {
            if (!string.IsNullOrEmpty(idleBreakStates[i]) && si.IsName(idleBreakStates[i]))
                return true;
        }
        return false;
    }
    // ===============================================
}
