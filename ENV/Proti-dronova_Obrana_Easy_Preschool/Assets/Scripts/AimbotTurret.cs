using UnityEngine;

public class AimbotTurret : MonoBehaviour
{
    [Header("References")]
    public Transform turretBase;        // TurretHead
    public Transform barrelPivot;       // BarrelsPivot
    public Transform bulletSpawnPoint;  // BulletSpawnPoint
    public Transform targetDrone;       // Drone
    public GameObject projectilePrefab; // Bullet prefab

    [Header("Rotation")]
    public float baseTurnSpeed = 90f;   // deg/s okolo Y
    public float barrelTurnSpeed = 60f; // deg/s pitch
    public float minPitch = -85f;
    public float maxPitch = 45f;

    [Header("Shooting")]
    public float muzzleSpeed = 60f;
    public float fireRate = 8f;             // strely za sekundu
    public float projectileLifetime = 5f;
    public float fireAngleThreshold = 5f;   // koæko stupÚov tolerujeme

    private float nextShotTime = 0f;

    private void Update()
    {
        if (!targetDrone || !turretBase || !barrelPivot || !bulletSpawnPoint)
            return;

        Vector3 toTarget = targetDrone.position - bulletSpawnPoint.position;
        if (toTarget.sqrMagnitude < 0.001f) return;

        Vector3 dir = toTarget.normalized;

        // 1) Yaw: otoË z·kladÚu smerom k dronovi (len v rovine XZ)
        Vector3 flatDir = new Vector3(dir.x, 0f, dir.z);
        if (flatDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetYaw = Quaternion.LookRotation(flatDir, Vector3.up);
            float yawStep = baseTurnSpeed * Time.deltaTime;
            turretBase.rotation = Quaternion.RotateTowards(turretBase.rotation, targetYaw, yawStep);
        }

        // 2) Pitch: sklon hlavne
        Vector3 localDir = barrelPivot.InverseTransformDirection(dir);
        float targetPitch = Mathf.Atan2(localDir.y, localDir.z) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);

        float currentPitch = barrelPivot.localEulerAngles.x;
        if (currentPitch > 180f) currentPitch -= 360f;

        float pitchStep = barrelTurnSpeed * Time.deltaTime;
        float newPitch = Mathf.MoveTowards(currentPitch, targetPitch, pitchStep);
        barrelPivot.localRotation = Quaternion.Euler(newPitch, 0f, 0f);

        // 3) Auto-fire: keÔ je uhol dosù mal˝ ? streæ
        Vector3 aimDir = bulletSpawnPoint.forward;
        float angleToTarget = Vector3.Angle(aimDir, dir);

        if (angleToTarget <= fireAngleThreshold && Time.time >= nextShotTime)
        {
            FireProjectile();
            nextShotTime = Time.time + 1f / fireRate;
        }
    }

    private void FireProjectile()
    {
        if (!projectilePrefab || !bulletSpawnPoint) return;

        GameObject bullet = Instantiate(
            projectilePrefab,
            bulletSpawnPoint.position,
            bulletSpawnPoint.rotation
        );

        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (!rb)
        {
            Destroy(bullet);
            return;
        }

        // Unity 6 ñ linearVelocity
        rb.linearVelocity = bulletSpawnPoint.forward * muzzleSpeed;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;

        Destroy(bullet, projectileLifetime);
    }
}
