using UnityEngine;
using System.Collections.Generic;

public class MLDroneSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject dronePrefab;
    public int droneCount = 5;
    public bool autoSpawn = true;

    [Header("Spawn Area")]
    public Vector2 xBounds = new Vector2(0f, 100f);
    public Vector2 zBounds = new Vector2(0f, 100f);
    public Vector2 yBounds = new Vector2(3f, 20f);
    public float safetyMargin = 15f;

    [Header("ML-Agents")]
    public bool enableAutoRespawn = true;

    [Header("Debug")]
    public bool showSpawnZone = true;

    public List<GameObject> ActiveDrones { get; private set; } = new List<GameObject>();
    public int DestroyedDrones { get; private set; } = 0;

    void Start()
    {
        if (autoSpawn)
        {
            SpawnAllDrones();
        }
    }

    public void SpawnAllDrones()
    {
        ClearExistingDrones();

        if (dronePrefab == null)
        {
            Debug.LogError("MLDroneSpawner: No drone prefab assigned!");
            return;
        }

        for (int i = 0; i < droneCount; i++)
        {
            CreateNewDrone(i);
        }

        Debug.Log($"Spawned {ActiveDrones.Count} ML drones");
    }

    private void CreateNewDrone(int index)
    {
        float minX = xBounds.x + safetyMargin;
        float maxX = xBounds.y - safetyMargin;
        float minZ = zBounds.x + safetyMargin;
        float maxZ = zBounds.y - safetyMargin;
        float minY = yBounds.x + safetyMargin * 0.5f;
        float maxY = yBounds.y - safetyMargin * 0.5f;

        Vector3 spawnPos = new Vector3(
            Random.Range(minX, maxX),
            Random.Range(minY, maxY),
            Random.Range(minZ, maxZ)
        );

        GameObject drone = Instantiate(dronePrefab, spawnPos, Quaternion.identity, transform);
        drone.name = $"MLDrone_{index + 1}";

        ActiveDrones.Add(drone);
        Debug.Log($"Created {drone.name} at {spawnPos}");
    }

    public void ReportDroneDestroyed(GameObject drone)
    {
        if (ActiveDrones.Contains(drone))
        {
            ActiveDrones.Remove(drone);
            DestroyedDrones++;

            Debug.Log($"ML Drone destroyed! Remaining: {ActiveDrones.Count}, Total destroyed: {DestroyedDrones}");

            if (enableAutoRespawn && ActiveDrones.Count < droneCount)
            {
                // Auto-respawn logic here if needed
            }
        }
    }

    public void ResetAllDrones()
    {
        ClearExistingDrones();
        SpawnAllDrones();
        DestroyedDrones = 0;
        Debug.Log("All ML drones respawned!");
    }

    public void ClearExistingDrones()
    {
        foreach (GameObject drone in ActiveDrones)
        {
            if (drone != null)
            {
                DestroyImmediate(drone);
            }
        }
        ActiveDrones.Clear();
    }

    void OnDrawGizmos()
    {
        if (!showSpawnZone) return;

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

        Gizmos.color = Color.yellow;
        Vector3 safeCenter = new Vector3(
            (xBounds.x + safetyMargin + xBounds.y - safetyMargin) * 0.5f,
            (yBounds.x + safetyMargin * 0.5f + yBounds.y - safetyMargin * 0.5f) * 0.5f,
            (zBounds.x + safetyMargin + zBounds.y - safetyMargin) * 0.5f
        );
        Vector3 safeSize = new Vector3(
            size.x - 2f * safetyMargin,
            size.y - safetyMargin,
            size.z - 2f * safetyMargin
        );
        Gizmos.DrawWireCube(safeCenter, safeSize);

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position, 1f);
    }
}