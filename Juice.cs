using UnityEngine;

public class Juice : MonoBehaviour
{
    public Transform juiceBody;
    [Min(0f)]
    public float maximumBodyDistance = 0.25f;
    [Min(0f)]
    public float maximumScaleBounce = 0.15f;

    [Header("Position Spring")]
    [Min(0f)]
    [SerializeField] private float positionStiffness = 120f;
    [Min(0f)]
    [SerializeField] private float positionDamping = 18f;

    [Header("Scale Spring")]
    [Min(0f)]
    [SerializeField] private float scaleStiffness = 160f;
    [Min(0f)]
    [SerializeField] private float scaleDamping = 22f;
    [Header("Direction Space")]
    [SerializeField] private bool applyDirectionInWorldSpace = false;

    private Vector3 _startLocalPos;
    private Vector3 _startLocalScale;

    private Vector3 _posOffset;
    private Vector3 _posVelocity;

    // Relative scale displacement (0 = at rest). Example: 0.1 => 10% larger.
    private float _scaleOffset;
    private float _scaleVelocity;

    private void Awake()
    {
        if (juiceBody == null) juiceBody = transform;
        CacheStart();
    }

    private void OnEnable()
    {
        if (juiceBody == null) juiceBody = transform;
        CacheStart();
    }

    private void CacheStart()
    {
        _startLocalPos = juiceBody.localPosition;
        _startLocalScale = juiceBody.localScale;
    }

    private void FixedUpdate()
    {
        if (juiceBody == null) return;

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        // Position spring: x'' = -k x - c x'
        _posVelocity += (-positionStiffness * _posOffset - positionDamping * _posVelocity) * dt;
        _posOffset += _posVelocity * dt;

        float maxDist = Mathf.Max(0f, maximumBodyDistance);
        if (maxDist > 0f)
        {
            if (_posOffset.sqrMagnitude > maxDist * maxDist)
            {
                _posOffset = _posOffset.normalized * maxDist;
                // Remove outward velocity so it doesn't keep pushing into the clamp.
                float along = Vector3.Dot(_posVelocity, _posOffset.normalized);
                if (along > 0f) _posVelocity -= _posOffset.normalized * along;
            }
        }
        else
        {
            _posOffset = Vector3.zero;
            _posVelocity = Vector3.zero;
        }

        // Scale spring (relative): s'' = -k s - c s'
        _scaleVelocity += (-scaleStiffness * _scaleOffset - scaleDamping * _scaleVelocity) * dt;
        _scaleOffset += _scaleVelocity * dt;

        float maxScale = Mathf.Max(0f, maximumScaleBounce);
        if (maxScale > 0f)
        {
            _scaleOffset = Mathf.Clamp(_scaleOffset, -maxScale, maxScale);
        }
        else
        {
            _scaleOffset = 0f;
            _scaleVelocity = 0f;
        }

        juiceBody.localPosition = _startLocalPos + _posOffset;

        float scaleMul = 1f + _scaleOffset;
        juiceBody.localScale = _startLocalScale * scaleMul;
    }

    public void AddForce(Vector2 direction, float magnitude)
    {
        if (juiceBody == null) juiceBody = transform;
        if (direction.sqrMagnitude <= 0.000001f) return;

        Vector3 directionVector = new Vector3(direction.x, direction.y, 0f);
        if (applyDirectionInWorldSpace)
        {
            Transform localSpaceRoot = juiceBody.parent != null ? juiceBody.parent : juiceBody;
            directionVector = localSpaceRoot.InverseTransformDirection(directionVector);
        }

        if (directionVector.sqrMagnitude <= 0.000001f) return;

        Vector3 dir = directionVector.normalized;
        float mag = Mathf.Max(0f, magnitude);

        // Apply as an impulse to the spring velocity.
        _posVelocity += new Vector3(dir.x, dir.y, 0f) * mag;
    }

    public void AddBounce(float magnitude)
    {
        float mag = magnitude;

        // Positive makes it grow, negative makes it shrink.
        _scaleVelocity += mag;
    }
}
