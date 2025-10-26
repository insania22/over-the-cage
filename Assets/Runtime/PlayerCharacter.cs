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
    public Quaternion Rotation;   // 카메라 yaw
    public Vector2 Move;
    public bool Jump;
    public bool JumpSustain;
    public CrouchInput Crouch;
    public bool AlignToCamera;    // ★ 카메라와 정렬할지 여부
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [Header("References")]
    public GameManagerLogic manager;
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform root;
    [SerializeField] private Transform cameraTarget;

    public KinematicCharacterMotor Motor => motor;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 7f;
    [SerializeField] private float walkResponse = 25f;
    [SerializeField] private float crouchResponse = 25f;

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
    [SerializeField] private float slideSteer_Ice = 10f;

    [Header("Ice Settings")]
    [SerializeField] private float iceSlideBoost = 5f;
    [SerializeField] private float iceSteerMultiplier = 1.3f;
    [SerializeField] private float autoSlideSpeedThreshold = 25f;
    [SerializeField] private float iceWalkAcceleration = 4f;
    [SerializeField] private float iceSlideGravity = 6f;

    [Header("Slope Settings")]
    [SerializeField] private float minSlideAngle = 35f;
    [SerializeField] private float maxSlideAngle = 80f;
    [SerializeField] private float maxSlideGravity = 30f;

    [Header("Body Settings")]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponse = 15f;
    [Range(0f, 1f)][SerializeField] private float standCameraTargetHeight = 0.9f;
    [Range(0f, 1f)][SerializeField] private float crouchCameraTargetHeight = 0.7f;

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
    private bool _alignToCamera;   // ★

    public void Initinalize()
    {
        _state.Stance = Stance.Stand;
        _lastState = _state;
        _uncrouchOverlapResults = new Collider[8];
        motor.CharacterController = this;
        StartCoroutine(ForceDoubleCtrlWithDelay());
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

        if (motor.GroundingStatus.IsStableOnGround)
        {
            _timeSinceUngrounded = 0f;
            _ungroundedDueToJump = false;

            var groundedMovement = motor.GetDirectionTangentToSurface(_requestedMovement, motor.GroundingStatus.GroundNormal) * _requestedMovement.magnitude;
            float slopeAngle = Vector3.Angle(motor.GroundingStatus.GroundNormal, motor.CharacterUp);
            bool isRunning = currentVelocity.magnitude > (walkSpeed * 0.5f);

            if (_requestedCrouch && !_requestedJump && _state.Stance != Stance.Slide)
            {
                if (isRunning) StartSlide(ref currentVelocity, groundedMovement);
                else StartCrouchOnly();
            }

            if (_state.Stance == Stance.Stand || _state.Stance == Stance.Crouch)
            {
                float speed = _state.Stance == Stance.Stand ? walkSpeed : crouchSpeed;
                float response = _state.Stance == Stance.Stand ? walkResponse : crouchResponse;

                var targetVelocity = groundedMovement * speed;
                var moveVelocity = Vector3.Lerp(currentVelocity, targetVelocity, 1f - Mathf.Exp(-response * deltaTime));
                _state.Acceleration = moveVelocity - currentVelocity;
                currentVelocity = moveVelocity;
            }
            else if (_state.Stance == Stance.Slide)
            {
                _requestedCrouch = true;

                float friction = isOnIce ? slideFriction_Ice : slideFriction_Default;
                currentVelocity *= Mathf.Exp(-friction * deltaTime);

                float slopeFactor = Mathf.Clamp01(Mathf.InverseLerp(5f, maxSlideAngle, slopeAngle));
                float addedGravity = (isOnIce ? iceSlideGravity : maxSlideGravity) * slopeFactor;
                Vector3 slopeForce = Vector3.ProjectOnPlane(-motor.CharacterUp, motor.GroundingStatus.GroundNormal) * addedGravity;
                currentVelocity += slopeForce * deltaTime;

                float steerMul = isOnIce ? iceSteerMultiplier : 1f;
                var inputDir = motor.GetDirectionTangentToSurface(_requestedMovement, motor.GroundingStatus.GroundNormal);
                if (inputDir.sqrMagnitude > 0.01f)
                {
                    Vector3 desired = inputDir.normalized * currentVelocity.magnitude;
                    currentVelocity = Vector3.Lerp(currentVelocity, desired, (slideSteer_Default * steerMul) * deltaTime);
                }

                if (currentVelocity.magnitude < slideEndSpeed)
                {
                    _state.Stance = Stance.Stand;
                    _requestedCrouch = false;
                    motor.SetCapsuleDimensions(motor.Capsule.radius, standHeight, standHeight * 0.5f);
                }
            }
        }
        else
        {
            _timeSinceUngrounded += deltaTime;

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

            float effectiveGravity = gravity;
            float verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            if (_requestedSustainedJump && verticalSpeed > 0f)
                effectiveGravity *= jumpSustainGravity;

            currentVelocity += motor.CharacterUp * effectiveGravity * deltaTime;
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
            // 필요 시: 이동 방향으로 천천히 보간하고 싶다면 아래 주석 해제
            /*
            var velPlanar = Vector3.ProjectOnPlane(motor.Velocity, motor.CharacterUp);
            if (velPlanar.sqrMagnitude > 0.01f)
            {
                var target = Quaternion.LookRotation(velPlanar.normalized, motor.CharacterUp);
                currentRotation = Quaternion.Slerp(currentRotation, target, 1f - Mathf.Exp(-8f * deltaTime));
            }
            */
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
                motor.SetCapsuleDimensions(motor.Capsule.radius, crouchHeight, crouchHeight * 0.5f);
            }
        }

        _state.Grounded = motor.GroundingStatus.IsStableOnGround;
        _state.Velocity = motor.Velocity;
        _lastState = _tempState;
    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        int layer = hitCollider.gameObject.layer;
        isOnIce = layer == LayerMask.NameToLayer("ice");
    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {
        if (hitCollider.CompareTag("Finish"))
            SceneManager.LoadScene("normal_end");
    }

    public bool IsColliderValidForCollisions(Collider coll) => true;
    public void OnDiscreteCollisionDetected(Collider hitCCollider) { }
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport) { }

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
