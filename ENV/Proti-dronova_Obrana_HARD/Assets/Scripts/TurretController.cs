using UnityEngine;
using UnityEngine.InputSystem;

public class TurretController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform turretBase;
    [SerializeField] private Transform barrelPivot;
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private GameObject projectilePrefab; // Changed to GameObject

    [Header("Movement")]
    [SerializeField] private float baseTurnSpeed = 90f;
    [SerializeField] private float barrelTurnSpeed = 60f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 45f;

    [Header("Shooting")]
    [SerializeField] private float muzzleSpeed = 60f;
    [SerializeField] private float fireRate = 8f;
    [SerializeField] private float projectileLifetime = 5f;

    private float targetPitch = 0f;
    private float nextShotTime = 0f;
    private Keyboard keyboard;

    private void Start()
    {
        Debug.Log("=== TURRET CONTROLLER STARTED ===");
        FindAllReferences();
        
        if (Keyboard.current != null)
        {
            keyboard = Keyboard.current;
            Debug.Log("Input System: WORKING");
        }
        else
        {
            Debug.LogError("Input System: Keyboard not detected!");
        }
    }

    private void FindAllReferences()
    {
        // Find turretBase
        if (!turretBase)
        {
            turretBase = transform.Find("TurretHead");
            if (!turretBase) Debug.LogError("TURRET BASE NOT FOUND");
        }

        // Find barrelPivot
        if (!barrelPivot)
        {
            if (turretBase) barrelPivot = turretBase.Find("BarrelsPivot");
            if (!barrelPivot) Debug.LogError("BARREL PIVOT NOT FOUND");
        }

        // Find bulletSpawnPoint
        if (!bulletSpawnPoint)
        {
            string[] possibleNames = { "BulletSpawnPoint", "SpawnPoint", "Muzzle", "BarrelEnd" };
            foreach (string name in possibleNames)
            {
                bulletSpawnPoint = FindChildRecursive(transform, name);
                if (bulletSpawnPoint) break;
            }
            
            if (!bulletSpawnPoint)
            {
                Debug.LogError("NO BULLET SPAWN POINT FOUND! Creating one...");
                CreateSpawnPoint();
            }
        }

        // Log what we found
        Debug.Log("Turret Base: " + (turretBase ? turretBase.name : "MISSING"));
        Debug.Log("Barrel Pivot: " + (barrelPivot ? barrelPivot.name : "MISSING"));
        Debug.Log("Spawn Point: " + (bulletSpawnPoint ? bulletSpawnPoint.name + " at " + bulletSpawnPoint.position : "MISSING"));
        Debug.Log("Projectile Prefab: " + (projectilePrefab ? projectilePrefab.name : "NOT ASSIGNED"));
    }

    private void CreateSpawnPoint()
    {
        GameObject spawnPoint = new GameObject("BulletSpawnPoint");
        if (barrelPivot)
        {
            spawnPoint.transform.SetParent(barrelPivot);
            spawnPoint.transform.localPosition = new Vector3(0, 0, 2f);
            spawnPoint.transform.localRotation = Quaternion.identity;
        }
        else
        {
            spawnPoint.transform.SetParent(transform);
            spawnPoint.transform.localPosition = new Vector3(0, 1, 2f);
        }
        bulletSpawnPoint = spawnPoint.transform;
    }

    private void Update()
    {
        if (keyboard == null)
        {
            keyboard = Keyboard.current;
            if (keyboard == null) return;
        }

        HandleRotation();
        HandleShooting();
    }

    private void HandleRotation()
    {
        // Yaw - rotate base left/right (A/D keys)
        if (turretBase)
        {
            float yawInput = 0f;
            if (keyboard.aKey.isPressed) yawInput = -1f;
            if (keyboard.dKey.isPressed) yawInput = 1f;
            
            turretBase.Rotate(Vector3.up, yawInput * baseTurnSpeed * Time.deltaTime, Space.Self);
        }

        // Pitch - rotate barrel up/down (W/S keys)
        if (barrelPivot)
        {
            float pitchInput = 0f;
            if (keyboard.sKey.isPressed) pitchInput = -1f;
            if (keyboard.wKey.isPressed) pitchInput = 1f;
            
            targetPitch += pitchInput * barrelTurnSpeed * Time.deltaTime;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

            barrelPivot.localRotation = Quaternion.Euler(targetPitch, 0f, 0f);
        }
    }

    private void HandleShooting()
    {
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("FIRE BUTTON PRESSED");
            Shoot();
            nextShotTime = Time.time + (1f / fireRate);
        }
        else if (keyboard.spaceKey.isPressed && Time.time >= nextShotTime)
        {
            Shoot();
            nextShotTime = Time.time + (1f / fireRate);
        }
    }

    private void Shoot()
    {
        if (!projectilePrefab)
        {
            Debug.LogError("CANNOT SHOOT: No projectile prefab assigned!");
            return;
        }

        if (!bulletSpawnPoint)
        {
            Debug.LogError("CANNOT SHOOT: No bullet spawn point!");
            return;
        }

        Debug.Log("Spawning bullet at: " + bulletSpawnPoint.position);

        // Instantiate the bullet as GameObject
        GameObject bulletObject = Instantiate(projectilePrefab, 
                                            bulletSpawnPoint.position, 
                                            bulletSpawnPoint.rotation);

        // Get the Rigidbody
        Rigidbody bulletRb = bulletObject.GetComponent<Rigidbody>();
        if (bulletRb == null)
        {
            Debug.LogError("Bullet prefab has no Rigidbody component!");
            return;
        }

        // Apply velocity
        bulletRb.linearVelocity = bulletSpawnPoint.forward * muzzleSpeed;
        bulletRb.angularVelocity = Vector3.zero;
        
        // Disable gravity for straight shooting
        bulletRb.useGravity = false;

        Debug.Log("Bullet spawned with velocity: " + bulletRb.linearVelocity);

        // Destroy after lifetime
        Destroy(bulletObject, projectileLifetime);
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName) return parent;
        
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            
            Transform result = FindChildRecursive(child, childName);
            if (result != null) return result;
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        if (bulletSpawnPoint)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(bulletSpawnPoint.position, 0.1f);
            Gizmos.color = Color.red;
            Gizmos.DrawRay(bulletSpawnPoint.position, bulletSpawnPoint.forward * 2f);
        }
    }
}