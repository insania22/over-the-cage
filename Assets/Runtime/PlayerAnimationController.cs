// PlayerAnimationController.cs
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
    public float stopSpeedThreshold = 0.5f;

    [Header("Idle Break Settings")]
    public float idleSpeedThreshold = 0.1f;
    public float idleMinInterval = 3f;
    public float idleMaxInterval = 7f;
    public string[] idleBreakStates = new string[] { "Idle_A", "Idle_B", "Idle_C" };

    private float idleTimer;
    private float nextIdleTime;
    private bool playingIdleBreak;

    void Reset()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (playerCharacter == null) playerCharacter = GetComponent<PlayerCharacter>();
    }

    void Start()
    {
        nextIdleTime = Random.Range(idleMinInterval, idleMaxInterval);
    }

    void Update()
    {
        if (playerCharacter == null || animator == null) return;

        var state = playerCharacter.GetState();

        // Speed 파라미터
        Vector3 planar = Vector3.ProjectOnPlane(state.Velocity, Vector3.up);
        float targetSpeed = planar.magnitude;
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeed, Time.deltaTime / smoothing);
        animator.SetFloat("Speed", currentSpeed);

        // Ground / Jump
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

        animator.SetBool("IsCrouch", state.Stance == Stance.Crouch);
        animator.SetBool("IsSliding", state.Stance == Stance.Slide);

        // Ice 감지
        bool onIce = false;
        var motor = playerCharacter.Motor;
        if (motor != null && motor.GroundingStatus.FoundAnyGround)
        {
            var ground = motor.GroundingStatus.GroundCollider;
            if (ground != null)
                onIce = (ground.gameObject.layer == LayerMask.NameToLayer("Ice"));
        }
        animator.SetBool("IsIce", onIce);

        HandleRandomIdle(state);
        lastGrounded = isGrounded;
    }

    private void HandleRandomIdle(CharacterState state)
    {
        var si = animator.GetCurrentAnimatorStateInfo(0);
        bool isTrueIdle =
            state.Grounded &&
            state.Stance == Stance.Stand &&
            currentSpeed < idleSpeedThreshold &&
            !animator.GetBool("IsJump") &&
            !animator.GetBool("IsCrouch") &&
            !animator.GetBool("IsSliding");

        bool inBreakState = IsInAnyIdleBreakState(si);

        if (!isTrueIdle)
        {
            idleTimer = 0f;
            if (!si.IsName("std")) playingIdleBreak = false;
            return;
        }

        if (inBreakState)
        {
            playingIdleBreak = true;
            return;
        }

        if (playingIdleBreak && si.IsName("std"))
        {
            playingIdleBreak = false;
            idleTimer = 0f;
            nextIdleTime = Random.Range(idleMinInterval, idleMaxInterval);
        }

        if (!playingIdleBreak && si.IsName("std"))
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= nextIdleTime && idleBreakStates != null && idleBreakStates.Length > 0)
            {
                int pick = Random.Range(0, idleBreakStates.Length);
                animator.SetInteger("IdleIndex", pick);
                animator.SetTrigger("IdleBreak");
                playingIdleBreak = true;
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
}
