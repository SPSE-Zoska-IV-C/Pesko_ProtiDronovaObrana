using UnityEngine;
using System.Collections.Generic;

public class DroneSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject dronePrefab;
    [SerializeField] private int numberOfDrones = 5;
    [SerializeField] private bool spawnOnStart = true;

    [Header("Spawn Area")]
    [SerializeField] private Vector2 xBounds = new Vector2(0f, 100f);
    [SerializeField] private Vector2 zBounds = new Vector2(0f, 100f);
    [SerializeField] private Vector2 yBounds = new Vector2(3f, 20f);
    [SerializeField] private float safeMargin = 15f;

    [Header("Debug")]
    [SerializeField] private bool showSpawnArea = true;

    private List<GameObject> activeDrones = new List<GameObject>();
    private int dronesDestroyed = 0;

    void Start()
    {
        if (spawnOnStart)
        {
            SpawnDrones();
        }
    }

    public void SpawnDrones()
    {
        // Clear any existing drones first
        ClearAllDrones();

        if (dronePrefab == null)
        {
            Debug.LogError("❌ DroneSpawner: No drone prefab assigned!");
            return;
        }

        for (int i = 0; i < numberOfDrones; i++)
        {
            SpawnSingleDrone(i);
        }

        Debug.Log($"🚁 Spawned {activeDrones.Count} drones");
        UpdateUI();
    }

    private void SpawnSingleDrone(int droneIndex)
    {
        // Calculate safe spawn area (inner bounds)
        float minX = xBounds.x + safeMargin;
        float maxX = xBounds.y - safeMargin;
        float minZ = zBounds.x + safeMargin;
        float maxZ = zBounds.y - safeMargin;
        float minY = yBounds.x + safeMargin * 0.5f;
        float maxY = yBounds.y - safeMargin * 0.5f;

        Vector3 spawnPosition = new Vector3(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY),
            Random.Range(minZ, maxZ)
        );

        GameObject drone = Instantiate(dronePrefab, spawnPosition, Quaternion.identity, transform);
        drone.name = $"Drone_{droneIndex + 1}";

        // Get or add DroneController and set bounds
        DroneController droneController = drone.GetComponent<DroneController>();
        if (droneController != null)
        {
            // You might want to set bounds here if your DroneController has setters
            // Or the bounds can be pre-configured in the prefab
        }

        // Add hit listener to track when drones are destroyed
        DroneHitListener hitListener = drone.AddComponent<DroneHitListener>();
        hitListener.Initialize(this, drone);

        activeDrones.Add(drone);

        Debug.Log($"📍 Spawned {drone.name} at {spawnPosition}");
    }

    public void OnDroneDestroyed(GameObject drone)
    {
        if (activeDrones.Contains(drone))
        {
            activeDrones.Remove(drone);
            dronesDestroyed++;

            Debug.Log($"💀 Drone destroyed! Remaining: {activeDrones.Count}, Total destroyed: {dronesDestroyed}");
            UpdateUI();

            // Optional: Auto-respawn after delay
            // StartCoroutine(RespawnDroneAfterDelay(3f));
        }
    }

    public void RespawnAllDrones()
    {
        ClearAllDrones();
        SpawnDrones();
        dronesDestroyed = 0;
        Debug.Log("🔄 All drones respawned!");
        UpdateUI();
    }

    public void ClearAllDrones()
    {
        foreach (GameObject drone in activeDrones)
        {
            if (drone != null)
            {
                DestroyImmediate(drone);
            }
        }
        activeDrones.Clear();
    }

    public void SetDroneCount(int count)
    {
        numberOfDrones = Mathf.Max(0, count);
        Debug.Log($"🎯 Drone count set to: {numberOfDrones}");
    }

    public void AddDrones(int count)
    {
        int startIndex = activeDrones.Count;
        for (int i = 0; i < count; i++)
        {
            SpawnSingleDrone(startIndex + i);
        }
        Debug.Log($"➕ Added {count} drones. Total: {activeDrones.Count}");
        UpdateUI();
    }

    public void RemoveDrones(int count)
    {
        int dronesToRemove = Mathf.Min(count, activeDrones.Count);
        for (int i = 0; i < dronesToRemove; i++)
        {
            if (activeDrones.Count > 0)
            {
                GameObject drone = activeDrones[0];
                activeDrones.RemoveAt(0);
                if (drone != null)
                {
                    Destroy(drone);
                }
            }
        }
        Debug.Log($"➖ Removed {dronesToRemove} drones. Remaining: {activeDrones.Count}");
        UpdateUI();
    }

    private void UpdateUI()
    {
        // Here you can update your game UI
        // For example, if you have a UI manager:
        // UIManager.Instance.UpdateDroneCount(activeDrones.Count, dronesDestroyed, numberOfDrones);

        Debug.Log($"📊 Drones: {activeDrones.Count} active, {dronesDestroyed} destroyed, {numberOfDrones} target");
    }

    // Public properties for other scripts to access
    public int ActiveDroneCount => activeDrones.Count;
    public int TotalDronesDestroyed => dronesDestroyed;
    public int TargetDroneCount => numberOfDrones;

    void OnDrawGizmos()
    {
        if (!showSpawnArea) return;

        // Draw spawn area
        Gizmos.color = Color.green;
        Vector3 center = new Vector3(
            (xBounds.x + xBounds.y) * 0.5f,
            (yBounds.x + yBounds.y) * 0.5f,
            (zBounds.x + zBounds.y) * 0.5f
        );
        Vector3 size = new Vector3(
            xBounds.y - xBounds.x,
            yBounds.y - yBounds.x,
            zBounds.y - zBounds.x
        );
        Gizmos.DrawWireCube(center, size);

        // Draw safe spawn area (inner bounds)
        Gizmos.color = Color.yellow;
        Vector3 safeCenter = new Vector3(
            (xBounds.x + safeMargin + xBounds.y - safeMargin) * 0.5f,
            (yBounds.x + safeMargin * 0.5f + yBounds.y - safeMargin * 0.5f) * 0.5f,
            (zBounds.x + safeMargin + zBounds.y - safeMargin) * 0.5f
        );
        Vector3 safeSize = new Vector3(
            size.x - 2f * safeMargin,
            size.y - safeMargin,
            size.z - 2f * safeMargin
        );
        Gizmos.DrawWireCube(safeCenter, safeSize);

        // Draw spawner position
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 1f);
    }
}

// Helper class to listen for drone destruction
public class DroneHitListener : MonoBehaviour
{
    private DroneSpawner spawner;
    private GameObject drone;

    public void Initialize(DroneSpawner spawner, GameObject drone)
    {
        this.spawner = spawner;
        this.drone = drone;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Bullet"))
        {
            // Notify spawner that this drone was hit
            spawner?.OnDroneDestroyed(drone);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Bullet"))
        {
            // Notify spawner that this drone was hit
            spawner?.OnDroneDestroyed(drone);
        }
    }
}