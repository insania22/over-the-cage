using KinematicCharacterController;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum CrouchInput { None, Toggle }
public enum Stance { Stand, Crouch, Slide }

public struct CharacterState
{
    public bool Grounded;
    public Stance Stance;
    public Vector3 Velocity;
    public Vector3 Acceleration;
}

public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public bool JumpSustain;
    public CrouchInput Crouch;
    public bool AlignToCamera;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [Header("References")]
    public GameManagerLogic manager;
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform root;
    [SerializeField] private Transform cameraTarget;

    public KinematicCharacterMotor Motor => motor;

    [Header("Slide Rules")]
    [SerializeField] private float noUphillBrakeSpeed = 30f;
    [SerializeField] private float flatSlopeEps = 0.04f;
    [SerializeField] private float downhillFastAccelMultiplier = 1.8f;
    [SerializeField] private float taggedSlideMinSpeed = 0f;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 7f;
    [SerializeField] private float walkResponse = 25f;
    [SerializeField] private float crouchResponse = 25f;
    [SerializeField] private float normalDeceleration = 20f;

    [Header("Directional Speed Multipliers")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float backwardSpeedMultiplier = 0.5f;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float sidewaysSpeedMultiplier = 0.7f;

    [Header("Air Settings")]
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float airAcceleration_Default = 70f;
    [SerializeField] private float airAcceleration_Ice = 110f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpSpeed = 20f;
    [SerializeField] private float coyoteTime = 0.2f;
    [Range(0f, 1f)][SerializeField] private float jumpSustainGravity = 0.4f;
    [SerializeField] private float gravity = -90f;

    [Header("Slide Settings")]
    [SerializeField] private float slideStartSpeed = 14f;
    [SerializeField] private float slideEndSpeed = 4f;
    [SerializeField] private float slideFriction_Default = 0.28f;
    [SerializeField] private float slideSteer_Default = 6f;
    [SerializeField] private float slideFriction_Ice = 0.03f;

    [Header("Ice Settings")]
    [SerializeField] private float iceSlideBoost = 5f;
    [SerializeField] private float iceSteerMultiplier = 1.3f;
    [SerializeField] private float iceSlideGravity = 6f;

    [Header("Slope Settings")]
    [SerializeField] private float maxSlideGravity = 30f;

    [Header("Body Settings")]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponse = 15f;
    [Range(0f, 1f)][SerializeField] private float standCameraTargetHeight = 0.9f;
    [Range(0f, 1f)][SerializeField] private float crouchCameraTargetHeight = 0.7f;

    // 내부 상태
    private CharacterState _state, _lastState, _tempState;
    private Quaternion _requestedRotation;
    private Vector3 _requestedMovement;
    private bool _requestedJump;
    private bool _requestedSustainedJump;
    private bool _requestedCrouch;
    private bool _requestedCrouchInAir;
    private float _timeSinceUngrounded;
    private float _timeSinceJumpRequest;
    private bool _ungroundedDueToJump;
    private bool isOnIce = false;
    private Collider[] _uncrouchOverlapResults;
    private bool _alignToCamera;

    private float _slideLowSpeedTimer = 0f;
    [SerializeField] private float slideLowSpeedHold = 0.20f;
    [SerializeField] private float shallowSlopeEps = 0.12f;
    private bool _forceSlideByTagPending = false;

    // 나머지 모든 함수 (UpdateInput, UpdateVelocity, StartSlide, etc.)
    // ---- 그대로 유지 ----


// 슬라이드 쿨타임 관련 변수는 요청에 따라 제외됨


public void Initinalize()
    {
        _state.Stance = Stance.Stand;
        _lastState = _state;
        _uncrouchOverlapResults = new Collider[8];
        motor.CharacterController = this;
        StartCoroutine(ForceDoubleCtrlWithDelay()); // 원래 코드에 있던 그대로 유지
    }

    public void UpdateInput(CharacterInput input)
    {
        _requestedRotation = input.Rotation;
        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);
        _requestedMovement = input.Rotation * _requestedMovement;   // 카메라 기준 이동
        _alignToCamera = input.AlignToCamera;                        // ★

        var wasRequestingJump = _requestedJump;
        _requestedJump = input.Jump || _requestedJump;
        if (_requestedJump && !wasRequestingJump)
            _timeSinceJumpRequest = 0f;

        _requestedSustainedJump = input.JumpSustain;

        var wasRequestedCrouch = _requestedCrouch;
        _requestedCrouch = input.Crouch == CrouchInput.Toggle ? !_requestedCrouch : _requestedCrouch;

        if (_requestedCrouch && !wasRequestedCrouch)
            _requestedCrouchInAir = !_state.Grounded;
        else if (!_requestedCrouch && wasRequestedCrouch)
            _requestedCrouchInAir = false;
    }

    public void UpdateBody(float deltaTime)
    {
        var currentHeight = motor.Capsule.height;
        var normalizedHeight = currentHeight / standHeight;
        var cameraTargetHeight = currentHeight * (_state.Stance == Stance.Stand ? standCameraTargetHeight : crouchCameraTargetHeight);
        var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);

        cameraTarget.localPosition = Vector3.Lerp(
            cameraTarget.localPosition,
            new Vector3(0f, cameraTargetHeight, 0f),
            1f - Mathf.Exp(-crouchHeightResponse * deltaTime));

        root.localScale = Vector3.Lerp(
            root.localScale,
            rootTargetScale,
            1f - Mathf.Exp(-crouchHeightResponse * deltaTime));
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        _state.Acceleration = Vector3.zero;

        // 슬라이드 쿨타임 로직은 요청에 따라 제외됨

        if (motor.GroundingStatus.IsStableOnGround)
        {
            _timeSinceUngrounded = 0f;
            _ungroundedDueToJump = false;

            var groundedMovement = motor.GetDirectionTangentToSurface(_requestedMovement, motor.GroundingStatus.GroundNormal) * _requestedMovement.magnitude;
            bool isRunning = currentVelocity.magnitude > (walkSpeed * 0.5f); // 원래 코드에 있던 그대로 유지

            // 현재 수평 속도
            float planarSpeed = Vector3.ProjectOnPlane(currentVelocity, motor.CharacterUp).magnitude;

            //  slide 태그 충돌로 예약된 강제 슬라이드 진입
            if (_forceSlideByTagPending && _state.Stance != Stance.Slide)
            {
                if (planarSpeed >= taggedSlideMinSpeed)
                {
                    ForceSlide(ref currentVelocity);
                }
                _forceSlideByTagPending = false; // 소비
            }

            //  고속 강제 슬라이드 진입(기존 규칙 유지)
            if (_state.Stance != Stance.Slide && planarSpeed >= noUphillBrakeSpeed)
            {
                ForceSlide(ref currentVelocity);
            }

            // 크라우치 버튼 눌렀을 때 슬라이드
            if (_requestedCrouch && !_requestedJump && _state.Stance != Stance.Slide)
            {
                if (isRunning) StartSlide(ref currentVelocity, groundedMovement);
                else StartCrouchOnly();
            }

            // 🛑 ⬇️ 여기부터 앞뒤좌우 속도 차이 로직입니다. ⬇️ 🛑
            if (_state.Stance == Stance.Stand || _state.Stance == Stance.Crouch)
            {
                float speed = _state.Stance == Stance.Stand ? walkSpeed : crouchSpeed;
                float response = _state.Stance == Stance.Stand ? walkResponse : crouchResponse;

                if (_requestedMovement.sqrMagnitude > 0.01f) // 입력이 있을 때
                {
                    Vector3 localGroundedMove = Quaternion.Inverse(motor.transform.rotation) * groundedMovement;

                    // ====== ★ 앞뒤좌우 움직일 때 속도 감소 구현 ★ ======
                    float forwardSpeed = localGroundedMove.z > 0 // 전방 이동 (z축 양수)
                        ? localGroundedMove.z * speed
                        : localGroundedMove.z * speed * backwardSpeedMultiplier; // 뒤로 이동 (z축 음수) 시 배율 적용

                    float sidewaysSpeed = localGroundedMove.x * speed * sidewaysSpeedMultiplier; // 좌우 이동 (x축) 시 배율 적용

                    Vector3 scaledLocalMove = new Vector3(sidewaysSpeed, 0, forwardSpeed);
                    // ===============================================

                    var targetVelocity = motor.transform.rotation * scaledLocalMove;
                    var moveVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-response * deltaTime));
                    _state.Acceleration = moveVelocity - currentVelocity;
                    currentVelocity = moveVelocity;
                }
                else // 🛑 입력이 없을 때 (지상 감속 로직) 🛑
                {
                    float currentHorizontalSpeedMagnitude = new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude;
                    if (currentHorizontalSpeedMagnitude > 0.01f) // 아주 느린 속도라도 남아있다면 감속 적용
                    {
                        Vector3 frictionVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, normalDeceleration * deltaTime);
                        currentVelocity.x = frictionVelocity.x;
                        currentVelocity.z = frictionVelocity.z;

                        if (Mathf.Abs(currentVelocity.x) < 0.01f && Mathf.Abs(currentVelocity.z) < 0.01f)
                        {
                            currentVelocity.x = 0f;
                            currentVelocity.z = 0f;
                        }
                    }
                    else
                    {
                        currentVelocity.x = 0f;
                        currentVelocity.z = 0f;
                    }
                    _state.Acceleration = -currentVelocity / deltaTime;
                }
            }
            // 🛑 ⬆️ 여기까지 앞뒤좌우 속도 차이 로직입니다. ⬆️ 🛑
            else if (_state.Stance == Stance.Slide)
            {
                _requestedCrouch = true;

                // --- 경사/방향 ---
                var up = motor.CharacterUp;
                float slopeAngleDeg = Vector3.Angle(motor.GroundingStatus.GroundNormal, up);
                float slopeValue = Mathf.Sin(slopeAngleDeg * Mathf.Deg2Rad); // 0~1
                bool isFlat = slopeValue < flatSlopeEps;

                Vector3 downhill = Vector3.ProjectOnPlane(-up, motor.GroundingStatus.GroundNormal);
                if (downhill.sqrMagnitude > 1e-6f) downhill.Normalize();

                Vector3 velPlanar = Vector3.ProjectOnPlane(currentVelocity, up);
                float velMag = velPlanar.magnitude;
                Vector3 velDir = velMag > 1e-3f
                    ? velPlanar / Mathf.Max(velMag, 1e-6f)
                    : (downhill.sqrMagnitude > 0f ? downhill : motor.CharacterForward);

                float signed = Mathf.Sign(Vector3.Dot(velDir, downhill)); // +1 내리막, -1 오르막
                bool fast = velMag > noUphillBrakeSpeed;

                // --- 경사 비례 속도 변화 ---
                float slopeCoeff = isOnIce ? iceSlideGravity : maxSlideGravity;
                float deltaSpeed = 0f;

                if (slopeCoeff > 0f && slopeValue > 0f)
                {
                    bool shallowAndSlow = (slopeValue < shallowSlopeEps) && (velMag < slideEndSpeed * 1.25f);

                    if (signed > 0f)
                    {
                        if (!shallowAndSlow)
                        {
                            float mul = fast ? downhillFastAccelMultiplier : 1f;
                            deltaSpeed = slopeCoeff * slopeValue * mul * deltaTime;
                        }
                    }
                    else // signed < 0f (오르막)
                    {
                        if (!shallowAndSlow)
                            deltaSpeed = 0.35f * slopeCoeff * slopeValue * deltaTime; // 얕은 오르막 감속 비율
                        else if (!fast)
                            deltaSpeed = -(slopeCoeff * slopeValue * 1.8f) * deltaTime; // 느릴 땐 오르막 감속
                    }
                }

                float newMag = Mathf.Max(0f, velMag + deltaSpeed);
                currentVelocity = velDir * newMag;

                // --- 조향(방향만 보정) ---
                float steerMul = isOnIce ? iceSteerMultiplier : 1f;
                var inputDir = motor.GetDirectionTangentToSurface(_requestedMovement, motor.GroundingStatus.GroundNormal);
                if (inputDir.sqrMagnitude > 0.01f && currentVelocity.sqrMagnitude > 1e-6f)
                {
                    Vector3 desiredDir = inputDir.normalized;
                    float dirLerp = (slideSteer_Default * steerMul) * deltaTime;
                    Vector3 newDir = Vector3.Slerp(currentVelocity.normalized, desiredDir, dirLerp).normalized;
                    currentVelocity = newDir * currentVelocity.magnitude;
                }

                // --- 마찰 ---
                float friction = isOnIce ? slideFriction_Ice : slideFriction_Default;
                if (fast)
                {
                    if (isFlat)
                        currentVelocity *= Mathf.Exp(-friction * deltaTime);
                }
                else
                {
                    currentVelocity *= Mathf.Exp(-friction * deltaTime);
                }

                // --- 저속 타이머 누적/리셋 ---
                if (currentVelocity.magnitude < slideEndSpeed)
                    _slideLowSpeedTimer += deltaTime;
                else
                    _slideLowSpeedTimer = 0f;

                // --- 종료 조건 ---
                bool uphillNow = signed < 0f;
                bool tooSlow = currentVelocity.magnitude < slideEndSpeed;

                if ((tooSlow && (isFlat || uphillNow)) || (_slideLowSpeedTimer >= slideLowSpeedHold))
                {
                    _state.Stance = Stance.Stand;
                    _requestedCrouch = false;
                    motor.SetCapsuleDimensions(motor.Capsule.radius, standHeight, standHeight * 0.5f);
                    _slideLowSpeedTimer = 0f;
                    return;
                }
            }
        }
        else // 🛑 공중에 있을 때 🛑
        {
            _timeSinceUngrounded += deltaTime;

            // 중력 적용
            float effectiveGravity = gravity;
            float verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            if (_requestedSustainedJump && verticalSpeed > 0f)
                effectiveGravity *= jumpSustainGravity;

            currentVelocity += motor.CharacterUp * effectiveGravity * deltaTime;

            // 공중 수평 이동 로직
            if (_requestedMovement.sqrMagnitude > 0f)
            {
                var planarMovement = Vector3.ProjectOnPlane(_requestedMovement, motor.CharacterUp) * _requestedMovement.magnitude;
                var currentPlanarVelocity = Vector3.ProjectOnPlane(currentVelocity, motor.CharacterUp);

                float effectiveAirAccel = isOnIce ? airAcceleration_Ice : airAcceleration_Default;
                var movementForce = planarMovement * effectiveAirAccel * deltaTime;

                if (currentPlanarVelocity.magnitude < airSpeed)
                {
                    var targetPlanarVelocity = currentPlanarVelocity + movementForce;
                    targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);
                    movementForce = targetPlanarVelocity - currentPlanarVelocity;
                }
                else if (Vector3.Dot(currentPlanarVelocity, planarMovement) > 0f)
                {
                    movementForce = Vector3.ProjectOnPlane(movementForce, currentPlanarVelocity.normalized);
                }

                currentVelocity += movementForce;
            }

            // 공중 저항 (Drag) 로직은 요청에 따라 제외됨
        }

        // 점프
        if (_requestedJump)
        {
            if (_state.Stance == Stance.Slide)
            {
                _requestedJump = false;
                return;
            }

            bool grounded = motor.GroundingStatus.IsStableOnGround;
            bool canCoyoteJump = _timeSinceUngrounded < coyoteTime && !_ungroundedDueToJump;

            if (grounded || canCoyoteJump)
            {
                _requestedJump = false;
                _requestedCrouch = false;
                _requestedCrouchInAir = false;
                motor.ForceUnground(0f);
                _ungroundedDueToJump = true;

                float currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                float targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
            }
            else
            {
                _timeSinceJumpRequest += deltaTime;
                _requestedJump = (_timeSinceJumpRequest < coyoteTime);
            }
        }
    }

    private void StartSlide(ref Vector3 currentVelocity, Vector3 groundedMovement)
    {
        _state.Stance = Stance.Slide;
        _requestedCrouch = true;

        Vector3 slideDir = groundedMovement.sqrMagnitude > 0.01f ? groundedMovement.normalized : motor.CharacterForward;
        float startSpeed = Mathf.Max(currentVelocity.magnitude, slideStartSpeed);
        currentVelocity = slideDir * startSpeed;

        if (isOnIce) currentVelocity += slideDir * iceSlideBoost;

        // motor.SetCapsuleDimensions(motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
    }

    // ★ 강제 슬라이드 진입(현재 속도로)
    private void ForceSlide(ref Vector3 currentVelocity)
    {
        _state.Stance = Stance.Slide;
        _requestedCrouch = true;

        Vector3 up = motor.CharacterUp;
        Vector3 velPlanar = Vector3.ProjectOnPlane(currentVelocity, up);
        Vector3 slideDir = velPlanar.sqrMagnitude > 1e-6f ? velPlanar.normalized : motor.CharacterForward;

        float startSpeed = Mathf.Max(currentVelocity.magnitude, slideStartSpeed);
        currentVelocity = slideDir * startSpeed;

        if (isOnIce) currentVelocity += slideDir * iceSlideBoost;
        motor.SetCapsuleDimensions(motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
    }

    private void StartCrouchOnly()
    {
        if (_state.Stance != Stance.Crouch)
        {
            _state.Stance = Stance.Crouch;
            _requestedCrouch = true;
            motor.SetCapsuleDimensions(motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
        }
    }

    // 🛑 구현된 ICharacterController 인터페이스 멤버들 시작 🛑
    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        if (_alignToCamera)
        {
            var forward = Vector3.ProjectOnPlane(_requestedRotation * Vector3.forward, motor.CharacterUp);
            if (forward != Vector3.zero)
                currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
        }
        else
        {
            // 이동 방향 보간 로직 (원래 코드에 주석 처리)
        }
    }

    public void BeforeCharacterUpdate(float deltaTime) { _tempState = _state; }

    public void PostGroundingUpdate(float deltaTime)
    {
        if (!motor.GroundingStatus.IsStableOnGround && _state.Stance == Stance.Slide)
            _state.Stance = Stance.Crouch;
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        if (!_requestedCrouch && _state.Stance != Stance.Stand)
        {
            _state.Stance = Stance.Stand;
            motor.SetCapsuleDimensions(motor.Capsule.radius, standHeight, standHeight * 0.5f);

            var pos = motor.TransientPosition;
            var rot = motor.TransientRotation;
            var mask = motor.CollidableLayers;
            if (motor.CharacterOverlap(pos, rot, _uncrouchOverlapResults, mask, QueryTriggerInteraction.Ignore) > 0)
            {
                _requestedCrouch = true;
                _state.Stance = Stance.Crouch;
                motor.SetCapsuleDimensions(motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
            }
        }

        // characterAnimator 관련 로직은 요청에 따라 제외됨
        _state.Grounded = motor.GroundingStatus.IsStableOnGround;
        _state.Velocity = motor.Velocity;
        _lastState = _tempState;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        int layer = hitCollider.gameObject.layer;
        isOnIce = layer == LayerMask.NameToLayer("ice");

        if (hitCollider.CompareTag("slide"))
        {
            _forceSlideByTagPending = true;
        }
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        if (hitCollider.CompareTag("Finish"))
            SceneManager.LoadScene("normal_end");

        if (hitCollider.CompareTag("slide"))
        {
            _forceSlideByTagPending = true;
        }
    }

    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnDiscreteCollisionDetected(Collider hitCCollider) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }
    // 🛑 구현된 ICharacterController 인터페이스 멤버들 끝 🛑

    public Transform GetCameraTarget() => cameraTarget;
    public CharacterState GetState() => _state;
    public CharacterState GetLastState() => _lastState;

    public void SetPosition(Vector3 position, bool killVelocity = true)
    {
        motor.SetPosition(position);
        if (killVelocity) motor.BaseVelocity = Vector3.zero;
    }

    private IEnumerator ForceDoubleCtrlWithDelay()
    {
        PerformCtrlAction();
        yield return new WaitForSeconds(0.2f);
        PerformCtrlAction();
    }

    private void PerformCtrlAction()
    {
        if (_state.Stance == Stance.Stand)
        {
            _requestedCrouch = true;
            _state.Stance = Stance.Crouch;
            motor.SetCapsuleDimensions(motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
        }
        else if (_state.Stance == Stance.Crouch)
        {
            _requestedCrouch = false;
            _state.Stance = Stance.Stand;
            motor.SetCapsuleDimensions(motor.Capsule.radius, standHeight, standHeight * 0.5f);
        }
    }
}