using UnityEngine;

public class CameraLean : MonoBehaviour
{
    [SerializeField] private float attackDamping = 0.5f;
    [SerializeField] private float decayDamping = 0.3f;
    [SerializeField] private float walkStrength = 0.075f;
    [SerializeField] private float slideStrength = 0.2f;
    [SerializeField] private float strengthResponse = 5f;

    private Vector3 _dampedAcceleration;
    private Vector3 _dampedAccelerationVel;
    private float _smoothStrength;

    public void Initialize()
    {
        _smoothStrength = walkStrength;
    }

    public void UpdateLean(float deltaTime, bool sliding, Vector3 acceleration, Vector3 up)
    {
        var planarAcceleration = Vector3.ProjectOnPlane(acceleration, up);

        var damping = planarAcceleration.magnitude > _dampedAcceleration.magnitude
            ? attackDamping
            : decayDamping;

        _dampedAcceleration = Vector3.SmoothDamp(
            _dampedAcceleration,
            planarAcceleration,
            ref _dampedAccelerationVel,
            damping,
            float.PositiveInfinity,
            deltaTime
        );

        var targetStrength = sliding ? slideStrength : walkStrength;
        _smoothStrength = Mathf.Lerp(_smoothStrength, targetStrength, 1f - Mathf.Exp(-strengthResponse * deltaTime));

        if (_dampedAcceleration.sqrMagnitude > 0.0001f)
        {
            var leanAxis = Vector3.Cross(_dampedAcceleration.normalized, up).normalized;
            float leanAngle = -_dampedAcceleration.magnitude * _smoothStrength;
            transform.localRotation = Quaternion.AngleAxis(leanAngle, leanAxis);
        }
    }
}
