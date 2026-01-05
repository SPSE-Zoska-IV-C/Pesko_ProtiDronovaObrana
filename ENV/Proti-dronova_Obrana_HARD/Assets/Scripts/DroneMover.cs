using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    [Header("Bounds")]
    [SerializeField] private Vector2 xBounds = new Vector2(0f, 100f);
    [SerializeField] private Vector2 zBounds = new Vector2(0f, 100f);
    [SerializeField] private Vector2 yBounds = new Vector2(3f, 20f);
    [SerializeField] private float marginXZ = 10f;
    [SerializeField] private float marginY = 1.5f;

    [Header("Physics")]
    [SerializeField] private float gravity = 9.81f;
    [SerializeField] private float liftForce = 15f;
    [SerializeField] private float maxLiftForce = 25f;
    [SerializeField] private float drag = 0.5f;
    [SerializeField] private float angularDrag = 2f;

    [Header("Motion (XZ)")]
    [SerializeField] private float baseSpeed = 8f;
    [SerializeField] private float turnSpeed = 100f;
    [SerializeField] private float changeInterval = 2f;

    [Header("Randomness (XZ)")]
    [SerializeField] private float randomSpread = 40f;
    [SerializeField] private float boundarySpread = 25f;
    [SerializeField] private float noiseYawAmplitude = 8f;
    [SerializeField] private float noiseYawSpeed = 0.6f;

    [Header("Vertical Motion (Y)")]
    [SerializeField] private float verticalChangeInterval = 2.2f; // Removed verticalSpeed
    [SerializeField] private float verticalJitter = 0.6f;

    [Header("Collision")]
    [SerializeField] private string bulletTag = "Bullet";

    // Physics variables
    private float currentLiftForce;
    private Vector3 velocity;
    private float verticalVelocity;

    // AI control variables
    private float targetYaw;
    private float yawTimer;
    private float noiseT;
    private float targetY;
    private float yTimer;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.linearDamping = drag;
        rb.angularDamping = angularDrag;

        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>();
        }
    }

    void Start()
    {
        float sx = Random.Range(xBounds.x + marginXZ, xBounds.y - marginXZ);
        float sz = Random.Range(zBounds.x + marginXZ, zBounds.y - marginXZ);
        float sy = Random.Range(yBounds.x + marginY, yBounds.y - marginY);
        transform.position = new Vector3(sx, sy, sz);

        targetYaw = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, targetYaw, 0f);

        yawTimer = Random.Range(changeInterval * 0.6f, changeInterval * 1.4f);
        yTimer = Random.Range(verticalChangeInterval * 0.6f, verticalChangeInterval * 1.4f);
        noiseT = Random.value * 100f;
        targetY = sy;
        currentLiftForce = liftForce;

        Debug.Log("🚁 Drone spawned at: " + transform.position);
    }

    void FixedUpdate()
    {
        HandlePhysics();
        HandleAI();
        ApplyForces();
        ClampPosition();
    }

    private void HandlePhysics()
    {
        float currentY = transform.position.y;
        float yError = targetY - currentY;

        currentLiftForce = Mathf.Clamp(liftForce + yError * 2f, liftForce * 0.5f, maxLiftForce);

        verticalVelocity -= gravity * Time.fixedDeltaTime;
        verticalVelocity += currentLiftForce * Time.fixedDeltaTime;
        verticalVelocity *= (1f - drag * Time.fixedDeltaTime);
    }

    private void HandleAI()
    {
        yawTimer -= Time.fixedDeltaTime;

        if (TryGetInwardDirectionXZ(transform.position, out Vector3 inwardXZ))
        {
            float baseYaw = YawFromDirection(inwardXZ);
            targetYaw = baseYaw + Random.Range(-boundarySpread, boundarySpread);
            yawTimer = Random.Range(0.4f, 1.0f);
        }
        else if (yawTimer <= 0f)
        {
            float baseYaw = transform.eulerAngles.y;
            targetYaw = baseYaw + Random.Range(-randomSpread, randomSpread);
            yawTimer = Random.Range(changeInterval * 0.7f, changeInterval * 1.3f);
        }

        yTimer -= Time.fixedDeltaTime;
        float y = transform.position.y;

        bool nearBottom = y - yBounds.x < marginY;
        bool nearTop = yBounds.y - y < marginY;

        if (nearBottom)
        {
            targetY = Mathf.Min(y + Random.Range(1.0f, 2.5f), yBounds.y - marginY);
            yTimer = Random.Range(0.4f, 1.0f);
        }
        else if (nearTop)
        {
            targetY = Mathf.Max(y - Random.Range(1.0f, 2.5f), yBounds.x + marginY);
            yTimer = Random.Range(0.4f, 1.0f);
        }
        else if (yTimer <= 0f)
        {
            float delta = Random.Range(-2.5f, 2.5f);
            targetY = Mathf.Clamp(y + delta, yBounds.x + marginY, yBounds.y - marginY);
            yTimer = Random.Range(verticalChangeInterval * 0.7f, verticalChangeInterval * 1.3f);
        }

        noiseT += Time.fixedDeltaTime * noiseYawSpeed;
        float yawNoise = (Mathf.PerlinNoise(noiseT, 0f) - 0.5f) * 2f * noiseYawAmplitude;
        float desiredYaw = targetYaw + yawNoise;

        Quaternion qTarget = Quaternion.Euler(0f, desiredYaw, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, qTarget, turnSpeed * Time.fixedDeltaTime);
    }

    private void ApplyForces()
    {
        Vector3 forwardForce = transform.forward * baseSpeed;
        Vector3 verticalForce = Vector3.up * verticalVelocity;
        Vector3 totalForce = forwardForce + verticalForce;

        rb.linearVelocity = totalForce;

        float jitter = (Mathf.PerlinNoise(0f, noiseT * 0.7f) - 0.5f) * 2f * verticalJitter;
        Vector3 wobble = new Vector3(
            Mathf.Sin(noiseT * 1.3f) * 0.1f,
            jitter * 0.2f,
            Mathf.Cos(noiseT * 1.1f) * 0.1f
        );

        rb.angularVelocity = wobble;
    }

    private void ClampPosition()
    {
        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, xBounds.x, xBounds.y);
        pos.z = Mathf.Clamp(pos.z, zBounds.x, zBounds.y);

        if (pos.y < yBounds.x)
        {
            pos.y = yBounds.x;
            verticalVelocity = Mathf.Max(verticalVelocity, 0f);
        }
        else if (pos.y > yBounds.y)
        {
            pos.y = yBounds.y;
            verticalVelocity = Mathf.Min(verticalVelocity, 0f);
        }

        transform.position = pos;
    }

    void Update()
    {
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"🚁 Drone Physics - Altitude: {transform.position.y:F1}, Target: {targetY:F1}, " +
                     $"Lift: {currentLiftForce:F1}, VerticalVel: {verticalVelocity:F2}");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(bulletTag))
        {
            Debug.Log("💥 DRONE HIT! Bullet: " + other.name);
            Destroy(other.gameObject);
            RespawnRandom();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag(bulletTag))
        {
            Debug.Log("💥 DRONE HIT (Collision)! Bullet: " + collision.gameObject.name);
            Destroy(collision.gameObject);
            RespawnRandom();
        }
    }

    public void RespawnRandom()
    {
        Debug.Log("🔄 Drone respawning...");

        float x = Random.Range(xBounds.x + marginXZ, xBounds.y - marginXZ);
        float z = Random.Range(zBounds.x + marginXZ, zBounds.y - marginXZ);
        float y = Random.Range(yBounds.x + marginY, yBounds.y - marginY);

        transform.SetPositionAndRotation(new Vector3(x, y, z),
                                         Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        verticalVelocity = 0f;
        currentLiftForce = liftForce;

        targetYaw = transform.eulerAngles.y + Random.Range(-randomSpread, randomSpread);
        targetY = y;
        yawTimer = Random.Range(changeInterval * 0.6f, changeInterval * 1.4f);
        yTimer = Random.Range(verticalChangeInterval * 0.6f, verticalChangeInterval * 1.4f);

        Debug.Log("📍 Drone respawned at: " + transform.position);
    }

    private bool TryGetInwardDirectionXZ(Vector3 pos, out Vector3 inward)
    {
        inward = Vector3.zero;
        bool near = false;

        if (pos.x - xBounds.x < marginXZ) { inward += Vector3.right; near = true; }
        if (xBounds.y - pos.x < marginXZ) { inward += Vector3.left; near = true; }
        if (pos.z - zBounds.x < marginXZ) { inward += Vector3.forward; near = true; }
        if (zBounds.y - pos.z < marginXZ) { inward += Vector3.back; near = true; }

        if (near) inward = inward.normalized;
        return near;
    }

    private float YawFromDirection(Vector3 dir)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return transform.eulerAngles.y;
        return Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = new Vector3((xBounds.x + xBounds.y) * 0.5f, (yBounds.x + yBounds.y) * 0.5f, (zBounds.x + zBounds.y) * 0.5f);
        Vector3 size = new Vector3(xBounds.y - xBounds.x, yBounds.y - yBounds.x, zBounds.y - zBounds.x);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(center, size);

        Vector3 inner = new Vector3(size.x - 2f * marginXZ, size.y - 2f * marginY, size.z - 2f * marginXZ);
        if (inner.x > 0f && inner.y > 0f && inner.z > 0f)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, inner);
        }

        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, Vector3.up * (currentLiftForce / maxLiftForce) * 2f);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, rb.linearVelocity * 0.5f);

            Gizmos.color = Color.blue;
            Vector3 targetPos = new Vector3(transform.position.x, targetY, transform.position.z);
            Gizmos.DrawWireSphere(targetPos, 0.5f);
            Gizmos.DrawLine(transform.position, targetPos);
        }
    }
}