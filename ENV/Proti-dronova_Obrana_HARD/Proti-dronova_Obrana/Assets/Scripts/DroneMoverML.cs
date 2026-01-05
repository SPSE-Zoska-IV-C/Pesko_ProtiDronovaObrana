using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MLDroneController : MonoBehaviour
{
    [Header("Bounds")]
    public Vector2 xBounds = new Vector2(0f, 100f);
    public Vector2 zBounds = new Vector2(0f, 100f);
    public Vector2 yBounds = new Vector2(3f, 20f);
    // Margin to stay away from top/bottom
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
    public float baseVerticalJitter = 2.0f;         // How far drones move up/down from mid (max +/- from yMid)
    public float verticalChangeInterval = 2.5f;     // How often to pick a new gentle float target
    public float verticalJitterNoise = 0.4f;        // Perlin noise amplitude for micro-jitter

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
    }

    void Start()
    {
        // Calculate vertical floating range ("middle box" for up/down)
        yInnerMin = yBounds.x + yMarginFromEdge;
        yInnerMax = yBounds.y - yMarginFromEdge;
        yMid = (yInnerMin + yInnerMax) * 0.5f;
        InitializeDrone();
    }

    public void InitializeDrone()
    {
        float sx = Random.Range(xBounds.x + marginXZ, xBounds.y - marginXZ);
        float sz = Random.Range(zBounds.x + marginXZ, zBounds.y - marginXZ);
        float sy = Random.Range(yInnerMin, yInnerMax);
        transform.position = new Vector3(sx, sy, sz);

        targetYawAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, targetYawAngle, 0f);

        yawTimeCounter = Random.Range(changeInterval * 0.6f, changeInterval * 1.4f);
        noiseTime = Random.value * 100f;
        PickNewFloatTarget();

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    void FixedUpdate()
    {
        UpdatePhysics();
        UpdateAI();
        ApplyMotion();
        ClampPosition();
    }

    private void UpdatePhysics()
    {
        float y = transform.position.y;
        float error = targetHeight - y;

        currentLift = Mathf.Clamp(liftForce + error * 2f, liftForce * 0.5f, maxLiftForce);

        verticalVel -= gravity * Time.fixedDeltaTime;
        verticalVel += currentLift * Time.fixedDeltaTime;
        verticalVel *= (1f - drag * Time.fixedDeltaTime);
    }

    private void UpdateAI()
    {
        // Horizontal steering
        yawTimeCounter -= Time.fixedDeltaTime;

        if (NearBoundary(transform.position, out Vector3 inward))
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

        // Keep drone floating in the middle vertical range; don't allow targets near top/bottom
        heightTimer -= Time.fixedDeltaTime;
        float currentY = transform.position.y;

        if (heightTimer <= 0f)
        {
            PickNewFloatTarget();
        }

        // If drone ever drifts too close to the edge (by physics accident), steer the target hard back to center
        if (currentY < yInnerMin + 0.25f)
        {
            targetHeight = yMid + Random.Range(0, baseVerticalJitter);
            verticalVel = Mathf.Abs(verticalVel); // boost up
            heightTimer = Random.Range(0.5f, 1.2f);
        }
        else if (currentY > yInnerMax - 0.25f)
        {
            targetHeight = yMid - Random.Range(0, baseVerticalJitter);
            verticalVel = -Mathf.Abs(verticalVel); // boost down
            heightTimer = Random.Range(0.5f, 1.2f);
        }

        // Apply yaw with noise
        noiseTime += Time.fixedDeltaTime * noiseYawSpeed;
        float yawNoise = (Mathf.PerlinNoise(noiseTime, 0f) - 0.5f) * 2f * noiseYawAmplitude;
        float desiredYaw = targetYawAngle + yawNoise;

        Quaternion target = Quaternion.Euler(0f, desiredYaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.fixedDeltaTime);
    }

    // Generates a new floating height inside the inner "box" (not near top or bottom)
    private void PickNewFloatTarget()
    {
        targetHeight = yMid + Random.Range(-baseVerticalJitter, baseVerticalJitter);
        targetHeight = Mathf.Clamp(targetHeight, yInnerMin + 0.2f, yInnerMax - 0.2f);
        heightTimer = Random.Range(verticalChangeInterval * 0.7f, verticalChangeInterval * 1.3f);
    }

    private void ApplyMotion()
    {
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
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, xBounds.x, xBounds.y);
        pos.z = Mathf.Clamp(pos.z, zBounds.x, zBounds.y);

        // Clamp Y so it stays in the middle region (never near box top or bottom)
        pos.y = Mathf.Clamp(pos.y, yInnerMin, yInnerMax);

        transform.position = pos;
    }

    private bool NearBoundary(Vector3 position, out Vector3 inward)
    {
        inward = Vector3.zero;
        bool near = false;

        if (position.x - xBounds.x < marginXZ) { inward += Vector3.right; near = true; }
        if (xBounds.y - position.x < marginXZ) { inward += Vector3.left; near = true; }
        if (position.z - zBounds.x < marginXZ) { inward += Vector3.forward; near = true; }
        if (zBounds.y - position.z < marginXZ) { inward += Vector3.back; near = true; }

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
        float nx = Random.Range(xBounds.x + marginXZ, xBounds.y - marginXZ);
        float nz = Random.Range(zBounds.x + marginXZ, zBounds.y - marginXZ);
        float ny = Random.Range(yInnerMin, yInnerMax);

        transform.SetPositionAndRotation(
            new Vector3(nx, ny, nz),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)
        );

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        verticalVel = 0f;
        currentLift = liftForce;

        targetYawAngle = transform.eulerAngles.y + Random.Range(-randomSpread, randomSpread);
        PickNewFloatTarget();
        yawTimeCounter = Random.Range(changeInterval * 0.6f, changeInterval * 1.4f);
    }
}
