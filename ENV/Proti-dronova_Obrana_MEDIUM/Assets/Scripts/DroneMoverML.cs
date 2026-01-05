using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MLDroneController : MonoBehaviour
{
    [Header("Environment Parent")]
    public Transform environmentParent;

    [Header("Bounds (Local to Environment)")]
    public Vector2 xBounds = new Vector2(-50f, 50f);
    public Vector2 zBounds = new Vector2(-50f, 50f);
    public Vector2 yBounds = new Vector2(3f, 20f);

    public float yMarginFromEdge = 5.0f;
    public float marginXZ = 10f;

    [Header("Physics")]
    public float gravity = 9.81f;
    public float liftForce = 15f;
    public float maxLiftForce = 25f;
    public float drag = 0.5f;
    public float angularDrag = 2f;

    [Header("Motion (XZ)")]
    public float baseSpeed = 8f;
    public float turnSpeed = 100f;
    public float changeInterval = 2f;

    [Header("Randomness (XZ)")]
    public float randomSpread = 40f;
    public float boundarySpread = 25f;
    public float noiseYawAmplitude = 8f;
    public float noiseYawSpeed = 0.6f;

    [Header("Vertical Motion (Y)")]
    public float baseVerticalJitter = 2.0f;
    public float verticalChangeInterval = 2.5f;
    public float verticalJitterNoise = 0.4f;

    private float currentLift;
    private float verticalVel;
    private float targetYawAngle;
    private float yawTimeCounter;
    private float noiseTime;

    private float yMid;
    private float yInnerMin, yInnerMax;
    private float targetHeight;
    private float heightTimer;

    private Rigidbody rb;

    // New: robust init flags
    private bool derivedReady = false;

    public Vector3 Velocity => rb ? rb.linearVelocity : Vector3.zero;
    public Vector3 Position => transform.position;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearDamping = drag;
        rb.angularDamping = angularDrag;

        if (!GetComponent<Collider>()) gameObject.AddComponent<BoxCollider>();
        gameObject.tag = "Drone";

        if (environmentParent == null)
            environmentParent = transform.parent;

        // New: ensure derived values exist as early as possible
        EnsureInitialized();
    }

    void Start()
    {
        EnsureInitialized();
        InitializeDrone();
    }

    // New: make Reset/Init safe even when called early
    private void EnsureInitialized()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (environmentParent == null) environmentParent = transform.parent;

        // Force the Rigidbody into the mode your script assumes (non-kinematic).
        if (rb.isKinematic) rb.isKinematic = false;   // prevents the warning [web:151]
        rb.useGravity = false;
        rb.linearDamping = drag;
        rb.angularDamping = angularDrag;

        if (!derivedReady)
        {
            yInnerMin = yBounds.x + yMarginFromEdge;
            yInnerMax = yBounds.y - yMarginFromEdge;
            yMid = (yInnerMin + yInnerMax) * 0.5f;
            derivedReady = true;
        }
    }

    public void InitializeDrone()
    {
        EnsureInitialized();

        float localX = Random.Range(xBounds.x + marginXZ, xBounds.y - marginXZ);
        float localZ = Random.Range(zBounds.x + marginXZ, zBounds.y - marginXZ);
        float localY = Random.Range(yInnerMin, yInnerMax);

        Vector3 localPos = new Vector3(localX, localY, localZ);

        if (environmentParent != null) transform.position = environmentParent.TransformPoint(localPos);
        else transform.position = localPos;

        targetYawAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, targetYawAngle, 0f);

        yawTimeCounter = Random.Range(changeInterval * 0.6f, changeInterval * 1.4f);
        noiseTime = Random.value * 100f;
        PickNewFloatTarget();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        verticalVel = 0f;
        currentLift = liftForce;
    }

    void FixedUpdate()
    {
        EnsureInitialized();
        UpdatePhysics();
        UpdateAI();
        ApplyMotion();
        ClampPosition();
    }

    private void UpdatePhysics()
    {
        float y = GetLocalPosition().y;
        float error = targetHeight - y;

        currentLift = Mathf.Clamp(liftForce + error * 2f, liftForce * 0.5f, maxLiftForce);

        verticalVel -= gravity * Time.fixedDeltaTime;
        verticalVel += currentLift * Time.fixedDeltaTime;
        verticalVel *= (1f - drag * Time.fixedDeltaTime);
    }

    private void UpdateAI()
    {
        yawTimeCounter -= Time.fixedDeltaTime;
        Vector3 localPos = GetLocalPosition();

        if (NearBoundary(localPos, out Vector3 inward))
        {
            float baseYaw = YawFrom(inward);
            targetYawAngle = baseYaw + Random.Range(-boundarySpread, boundarySpread);
            yawTimeCounter = Random.Range(0.4f, 1.0f);
        }
        else if (yawTimeCounter <= 0f)
        {
            float baseYaw = transform.eulerAngles.y;
            targetYawAngle = baseYaw + Random.Range(-randomSpread, randomSpread);
            yawTimeCounter = Random.Range(changeInterval * 0.7f, changeInterval * 1.3f);
        }

        heightTimer -= Time.fixedDeltaTime;
        float currentY = localPos.y;

        if (heightTimer <= 0f) PickNewFloatTarget();

        if (currentY < yInnerMin + 0.25f)
        {
            targetHeight = yMid + Random.Range(0, baseVerticalJitter);
            verticalVel = Mathf.Abs(verticalVel);
            heightTimer = Random.Range(0.5f, 1.2f);
        }
        else if (currentY > yInnerMax - 0.25f)
        {
            targetHeight = yMid - Random.Range(0, baseVerticalJitter);
            verticalVel = -Mathf.Abs(verticalVel);
            heightTimer = Random.Range(0.5f, 1.2f);
        }

        noiseTime += Time.fixedDeltaTime * noiseYawSpeed;
        float yawNoise = (Mathf.PerlinNoise(noiseTime, 0f) - 0.5f) * 2f * noiseYawAmplitude;
        float desiredYaw = targetYawAngle + yawNoise;

        Quaternion target = Quaternion.Euler(0f, desiredYaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.fixedDeltaTime);
    }

    private void PickNewFloatTarget()
    {
        EnsureInitialized();
        targetHeight = yMid + Random.Range(-baseVerticalJitter, baseVerticalJitter);
        targetHeight = Mathf.Clamp(targetHeight, yInnerMin + 0.2f, yInnerMax - 0.2f);
        heightTimer = Random.Range(verticalChangeInterval * 0.7f, verticalChangeInterval * 1.3f);
    }

    private void ApplyMotion()
    {
        EnsureInitialized(); // make sure it's not kinematic before writing velocity

        Vector3 forward = transform.forward * baseSpeed;
        Vector3 vertical = Vector3.up * verticalVel;
        rb.linearVelocity = forward + vertical;

        float jitter = (Mathf.PerlinNoise(0f, noiseTime * 0.7f) - 0.5f) * 2f * verticalJitterNoise;
        Vector3 wobble = new Vector3(
            Mathf.Sin(noiseTime * 1.3f) * 0.1f,
            jitter * 0.2f,
            Mathf.Cos(noiseTime * 1.1f) * 0.1f
        );
        rb.angularVelocity = wobble;
    }

    private void ClampPosition()
    {
        Vector3 localPos = GetLocalPosition();

        localPos.x = Mathf.Clamp(localPos.x, xBounds.x, xBounds.y);
        localPos.z = Mathf.Clamp(localPos.z, zBounds.x, zBounds.y);
        localPos.y = Mathf.Clamp(localPos.y, yInnerMin, yInnerMax);

        if (environmentParent != null) transform.position = environmentParent.TransformPoint(localPos);
        else transform.position = localPos;
    }

    private Vector3 GetLocalPosition()
    {
        if (environmentParent != null) return environmentParent.InverseTransformPoint(transform.position);
        return transform.position;
    }

    private bool NearBoundary(Vector3 localPosition, out Vector3 inward)
    {
        inward = Vector3.zero;
        bool near = false;

        if (localPosition.x - xBounds.x < marginXZ) { inward += Vector3.right; near = true; }
        if (xBounds.y - localPosition.x < marginXZ) { inward += Vector3.left; near = true; }
        if (localPosition.z - zBounds.x < marginXZ) { inward += Vector3.forward; near = true; }
        if (zBounds.y - localPosition.z < marginXZ) { inward += Vector3.back; near = true; }

        if (near) inward = inward.normalized;
        return near;
    }

    private float YawFrom(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return transform.eulerAngles.y;
        return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet")) HandleBulletImpact(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Bullet")) HandleBulletImpact(collision.gameObject);
    }

    private void HandleBulletImpact(GameObject bullet)
    {
        Destroy(bullet);
        ResetDronePosition();
    }

    public void ResetDronePosition()
    {
        EnsureInitialized();

        float localX = Random.Range(xBounds.x + marginXZ, xBounds.y - marginXZ);
        float localZ = Random.Range(zBounds.x + marginXZ, zBounds.y - marginXZ);
        float localY = Random.Range(yInnerMin, yInnerMax);

        Vector3 localPos = new Vector3(localX, localY, localZ);

        if (environmentParent != null)
        {
            transform.SetPositionAndRotation(
                environmentParent.TransformPoint(localPos),
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            );
        }
        else
        {
            transform.SetPositionAndRotation(
                localPos,
                Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
            );
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        verticalVel = 0f;
        currentLift = liftForce;

        targetYawAngle = transform.eulerAngles.y + Random.Range(-randomSpread, randomSpread);
        PickNewFloatTarget();
        yawTimeCounter = Random.Range(changeInterval * 0.6f, changeInterval * 1.4f);
    }
}
