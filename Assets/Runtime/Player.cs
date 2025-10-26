using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class Player : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerCamera playerCamera;
    [SerializeField] private CameraSpring cameraSpring;
    [SerializeField] private CameraLean cameraLean;
    [SerializeField] private Volume volume;
    [SerializeField] private StanceVignette stanceVignette;

    private PlayerInputActions _inputActions;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        _inputActions = new PlayerInputActions();
        _inputActions.Enable();

        playerCharacter.Initinalize();
        playerCamera.Initianlize(playerCharacter.GetCameraTarget());

        cameraSpring.Initialize();
        cameraLean.Initialize();
        stanceVignette.Initialize(volume.profile);
    }

    void OnDestroy()
    {
        _inputActions?.Dispose();
    }

    void Update()
    {
        var input = _inputActions.Gameplay;
        var deltaTime = Time.deltaTime;

        // 마우스 회전 입력
        var cameraInput = new CameraInput { Look = input.Look.ReadValue<Vector2>() };
        playerCamera.UpdateRotation(cameraInput);

        // 정렬 조건: 1인칭 / 이동 중 / RMB 조준
        var move = input.Move.ReadValue<Vector2>();
        bool align =
            playerCamera.Mode == PlayerCamera.ViewMode.FirstPerson ||
            move.sqrMagnitude > 0.0001f ||
            (Mouse.current != null && Mouse.current.rightButton.isPressed);

        // 캐릭터 입력
        var characterInput = new CharacterInput
        {
            Rotation = playerCamera.GetControlRotationYawOnly(),
            Move = move,
            Jump = input.Jump.WasPressedThisFrame(),
            JumpSustain = input.Jump.IsPressed(),
            Crouch = input.Crouch.WasPressedThisFrame() ? CrouchInput.Toggle : CrouchInput.None,
            AlignToCamera = align
        };

        playerCharacter.UpdateInput(characterInput);
        playerCharacter.UpdateBody(deltaTime);

        // H: 시점 순환 (뒤→앞→1인칭)
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            playerCamera.CycleView();
        }
    }

    void LateUpdate()
    {
        var deltaTime = Time.deltaTime;
        var cameraTarget = playerCharacter.GetCameraTarget();
        var state = playerCharacter.GetState();

        playerCamera.UpdatePosition(cameraTarget);
        cameraSpring.UpdateSpring(deltaTime, cameraTarget.up);
        cameraLean.UpdateLean(deltaTime, state.Stance == Stance.Slide, state.Acceleration, cameraTarget.up);
        stanceVignette.UpdateVignette(deltaTime, state.Stance);
    }

    public void Teleport(Vector3 position)
    {
        playerCharacter.SetPosition(position);
    }
}
