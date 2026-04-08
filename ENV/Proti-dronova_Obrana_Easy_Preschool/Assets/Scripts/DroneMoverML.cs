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

    [Header("Spawn Margins")]
    public float marginXZ = 10f;
    public float yMarginFromEdge = 5.0f;

    private Rigidbody rb;
    private float yInnerMin, yInnerMax;
    private bool isInitialized = false;

    public Vector3 Velocity => Vector3.zero;  // Always zero (stationary)
    public Vector3 Position => transform.position;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Kinematic = no physics, drone stays frozen in place
        rb.isKinematic = true;
        rb.useGravity = false;

        if (!GetComponent<Collider>())
            gameObject.AddComponent<BoxCollider>();

        gameObject.tag = "Drone";

        if (environmentParent == null)
            environmentParent = transform.parent;

        Initialize();
    }

    void Start()
    {
        Initialize();
        SpawnAtRandomPosition();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (environmentParent == null)
            environmentParent = transform.parent;

        // Calculate vertical spawn boundaries
        yInnerMin = yBounds.x + yMarginFromEdge;
        yInnerMax = yBounds.y - yMarginFromEdge;

        isInitialized = true;
    }

    public void InitializeDrone()
    {
        Initialize();
        SpawnAtRandomPosition();
    }

    private void SpawnAtRandomPosition()
    {
        Initialize();

        // Generate random position within bounds
        float localX = Random.Range(xBounds.x + marginXZ, xBounds.y - marginXZ);
        float localZ = Random.Range(zBounds.x + marginXZ, zBounds.y - marginXZ);
        float localY = Random.Range(yInnerMin, yInnerMax);

        Vector3 localPos = new Vector3(localX, localY, localZ);

        // Convert to world position if we have environment parent
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
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet"))
            HandleBulletImpact(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Bullet"))
            HandleBulletImpact(collision.gameObject);
    }

    private void HandleBulletImpact(GameObject bullet)
    {
        Destroy(bullet);
        ResetDronePosition();
    }

    public void ResetDronePosition()
    {
        SpawnAtRandomPosition();
    }
}
