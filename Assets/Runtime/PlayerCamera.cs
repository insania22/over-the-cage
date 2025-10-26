using UnityEngine;

public struct CameraInput
{
    public Vector2 Look; // x=Yaw, y=Pitch
}

public class PlayerCamera : MonoBehaviour
{
    public enum ViewMode { ThirdBack, ThirdFront, FirstPerson }

    [Header("Mouse Look")]
    [SerializeField] private float sensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;

    [Header("View Offsets")]
    [SerializeField] private float thirdDistance = 3.0f;
    [SerializeField] private float thirdHeight = 1.2f;
    [SerializeField] private float firstPersonHeight = 1.0f;

    [Header("Smoothing")]
    [SerializeField] private float positionLerp = 20f;
    [SerializeField] private float rotationLerp = 20f;

    private Vector3 _euler;        // 기본 카메라 오일러
    private float _modeYawOffset;  // ThirdFront일 때 180°
    private Transform _target;
    private ViewMode _mode = ViewMode.ThirdBack;

    public ViewMode Mode => _mode;

    public void Initianlize(Transform target)
    {
        _target = target;
        _euler = new Vector3(15f, target.eulerAngles.y, 0f); // 살짝 내려다보며 시작
        transform.rotation = Quaternion.Euler(_euler);
        transform.position = target.position;
    }

    public void UpdateRotation(CameraInput input)
    {
        _euler.x += -input.Look.y * sensitivity;
        _euler.y += input.Look.x * sensitivity;
        _euler.x = Mathf.Clamp(_euler.x, minPitch, maxPitch);

        var targetEuler = new Vector3(_euler.x, _euler.y + _modeYawOffset, 0f);
        var targetRot = Quaternion.Euler(targetEuler);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRot,
            1f - Mathf.Exp(-rotationLerp * Time.deltaTime)
        );
    }

    // 캐릭터 제어용: yaw만 넘김
    public Quaternion GetControlRotationYawOnly()
    {
        return Quaternion.Euler(0f, _euler.y + _modeYawOffset, 0f);
    }

    public void UpdatePosition(Transform target)
    {
        _target = target;
        Vector3 desiredPos;

        switch (_mode)
        {
            case ViewMode.FirstPerson:
                desiredPos = target.position + Vector3.up * firstPersonHeight;
                break;

            case ViewMode.ThirdBack:
            case ViewMode.ThirdFront:
            default:
                // 현재 카메라의 forward를 기준으로 타깃 뒤쪽에 배치(앞보기는 yaw 오프셋으로 자동 반전)
                var forward = transform.forward;
                desiredPos = target.position + Vector3.up * thirdHeight - forward * thirdDistance;
                break;
        }

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPos,
            1f - Mathf.Exp(-positionLerp * Time.deltaTime)
        );
    }

    public void CycleView()
    {
        switch (_mode)
        {
            case ViewMode.ThirdBack:
                _mode = ViewMode.ThirdFront;
                _modeYawOffset = 180f;
                break;
            case ViewMode.ThirdFront:
                _mode = ViewMode.FirstPerson;
                _modeYawOffset = 0f;
                break;
            default:
                _mode = ViewMode.ThirdBack;
                _modeYawOffset = 0f;
                break;
        }
    }
}
