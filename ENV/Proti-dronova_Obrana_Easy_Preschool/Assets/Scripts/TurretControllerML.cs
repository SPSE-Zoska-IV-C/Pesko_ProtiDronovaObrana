using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class TurretAgentML : Agent
{
    [Header("References")]
    public Transform turretBase;
    public Transform barrelPivot;
    public Transform bulletSpawnPoint;
    public GameObject projectilePrefab;
    public GameObject targetDrone;
    public TrainingStatsUI statsUI;

    [Header("Movement - Base Speeds")]
    public float minTurnSpeed = 20f;
    public float maxTurnSpeed = 150f;
    public float speedChangeRate = 30f;

    public float minPitch = -85f;
    public float maxPitch = 45f;

    [Header("Shooting")]
    public float muzzleSpeed = 60f;
    public float fireRate = 8f;
    public float projectileLifetime = 5f;

    [Header("Rewards")]
    public float hitReward = 5.0f;
    public float wallHitPenalty = -1.0f;
    public float goodShotReward = 0.10f;
    public float decentShotReward = 0.03f;
    public float badShotPenalty = -0.02f;
    public float aimingRewardPerStep = 0.0003f;

    [Header("Statistics (Runtime)")]
    public int totalShots = 0;           // Total bullets fired
    public int successfulHits = 0;       // Drone hits
    public int wallHits = 0;             // Wall/ground/ceiling hits
    public float hitAccuracy = 0f;       // Hit percentage

    [Header("Debug")]
    public bool showBarrelAim = true;
    public float aimLineLength = 50f;
    public float currentAimAccuracy;
    public float currentAngleToDrone;

    [Header("Current Speeds (Runtime)")]
    public float currentYawSpeed = 90f;
    public float currentPitchSpeed = 60f;

    private float currentPitch = 0f;
    private float nextShotTime = 0f;
    private MLDroneController droneController;

    public override void Initialize()
    {
        if (!turretBase) turretBase = transform.Find("TurretHead");
        if (!barrelPivot) barrelPivot = turretBase?.Find("BarrelsPivot");
        if (!bulletSpawnPoint) bulletSpawnPoint = barrelPivot?.Find("Barrel/BulletSpawnPoint");
        if (targetDrone) droneController = targetDrone.GetComponent<MLDroneController>();
        MaxStep = 2000;

        currentYawSpeed = (minTurnSpeed + maxTurnSpeed) * 0.5f;
        currentPitchSpeed = (minTurnSpeed + maxTurnSpeed) * 0.5f;
    }

    public override void OnEpisodeBegin()
    {
        currentPitch = 0f;
        nextShotTime = 0f;

        // Reset stats for new episode
        totalShots = 0;
        successfulHits = 0;
        wallHits = 0;
        hitAccuracy = 0f;

        currentYawSpeed = (minTurnSpeed + maxTurnSpeed) * 0.5f;
        currentPitchSpeed = (minTurnSpeed + maxTurnSpeed) * 0.5f;

        if (turretBase) turretBase.rotation = Quaternion.identity;
        if (barrelPivot) barrelPivot.localRotation = Quaternion.identity;
        droneController?.ResetDronePosition();
    }

    private void OnDrawGizmos()
    {
        if (!showBarrelAim || !bulletSpawnPoint) return;
        Gizmos.color = Color.red;
        Gizmos.DrawRay(bulletSpawnPoint.position, bulletSpawnPoint.forward * aimLineLength);
        Gizmos.DrawWireSphere(bulletSpawnPoint.position, 0.3f);
    }

    private void Update()
    {
        if (targetDrone && bulletSpawnPoint)
        {
            Vector3 toDrone = (targetDrone.transform.position - bulletSpawnPoint.position).normalized;
            currentAimAccuracy = Vector3.Dot(bulletSpawnPoint.forward, toDrone);
            currentAngleToDrone = Vector3.Angle(bulletSpawnPoint.forward, toDrone);
        }

        // Update accuracy percentage
        if (totalShots > 0)
        {
            hitAccuracy = (float)successfulHits / totalShots * 100f;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AddReward(-0.1f / MaxStep);

        Vector3 e = turretBase ? turretBase.eulerAngles : Vector3.zero;
        sensor.AddObservation(NormAngle(e.y));
        sensor.AddObservation(NormAngle(barrelPivot?.localEulerAngles.x ?? 0f));
        sensor.AddObservation(NormAngle(e.z));

        Vector3 p = transform.position;
        sensor.AddObservation(p / 100f);

        if (targetDrone)
        {
            Vector3 rel = turretBase ? turretBase.InverseTransformPoint(targetDrone.transform.position)
                                     : targetDrone.transform.position - p;
            sensor.AddObservation(rel / 100f);

            Vector3 v = droneController?.Velocity ?? Vector3.zero;
            sensor.AddObservation(v / 20f);

            sensor.AddObservation(Mathf.Clamp01(Vector3.Distance(p, targetDrone.transform.position) / 200f));

            bool inFront = turretBase && Vector3.Dot(turretBase.forward, (targetDrone.transform.position - p).normalized) > 0.3f;
            sensor.AddObservation(inFront ? 1f : 0f);

            if (!inFront) AddReward(-0.3f / MaxStep);

            if (bulletSpawnPoint && turretBase)
            {
                Vector3 toDrone = (targetDrone.transform.position - bulletSpawnPoint.position).normalized;
                float aimAccuracy = Vector3.Dot(bulletSpawnPoint.forward, toDrone);
                AddReward(Mathf.Max(0, aimAccuracy) * aimingRewardPerStep);
            }
        }
        else
        {
            for (int i = 0; i < 8; i++) sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        var ca = actions.ContinuousActions;
        float yaw = Mathf.Clamp(ca[0], -1f, 1f);
        float pitch = Mathf.Clamp(ca[1], -1f, 1f);
        float shoot = ca[2];
        float speedAdjust = Mathf.Clamp(ca[3], -1f, 1f);

        float speedDelta = speedAdjust * speedChangeRate * Time.deltaTime;
        currentYawSpeed = Mathf.Clamp(currentYawSpeed + speedDelta, minTurnSpeed, maxTurnSpeed);
        currentPitchSpeed = Mathf.Clamp(currentPitchSpeed + speedDelta, minTurnSpeed, maxTurnSpeed);

        if (turretBase)
            turretBase.Rotate(Vector3.up, yaw * currentYawSpeed * Time.deltaTime, Space.Self);

        if (barrelPivot)
        {
            currentPitch = Mathf.Clamp(currentPitch + pitch * currentPitchSpeed * Time.deltaTime, minPitch, maxPitch);
            barrelPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }

        if (shoot > 0.5f && Time.time >= nextShotTime)
        {
            FireProjectile();
            nextShotTime = Time.time + 1f / fireRate;

            if (bulletSpawnPoint && targetDrone)
            {
                Vector3 toDrone = (targetDrone.transform.position - bulletSpawnPoint.position).normalized;
                float aimAccuracy = Vector3.Dot(bulletSpawnPoint.forward, toDrone);

                if (aimAccuracy > 0.997f) AddReward(goodShotReward);
                else if (aimAccuracy > 0.99f) AddReward(decentShotReward);
                else if (aimAccuracy > 0.95f) AddReward(0.05f);
                else AddReward(badShotPenalty);
            }
            else
            {
                AddReward(badShotPenalty);
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;
        var kb = UnityEngine.InputSystem.Keyboard.current;

        if (kb != null)
        {
            ca[0] = (kb.aKey.isPressed ? -1f : 0f) + (kb.dKey.isPressed ? 1f : 0f);
            ca[1] = (kb.wKey.isPressed ? 1f : 0f) + (kb.sKey.isPressed ? -1f : 0f);
            ca[2] = kb.spaceKey.isPressed ? 1f : 0f;

            float speedInput = 0f;
            if (kb.eKey.isPressed) speedInput = 1f;
            if (kb.qKey.isPressed) speedInput = -1f;
            ca[3] = speedInput;
        }
    }

    private void FireProjectile()
    {
        if (!projectilePrefab || !bulletSpawnPoint) return;

        totalShots++;  // Increment shot counter

        var bullet = Instantiate(projectilePrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        var rb = bullet.GetComponent<Rigidbody>();

        if (!rb)
        {
            Destroy(bullet);
            return;
        }

        var mlb = bullet.GetComponent<MLBullet>();
        if (!mlb) mlb = bullet.AddComponent<MLBullet>();
        mlb.Setup(this);

        rb.linearVelocity = bulletSpawnPoint.forward * muzzleSpeed;
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;

        Destroy(bullet, projectileLifetime);
    }

    public void RegisterSuccessfulHit()
    {
        successfulHits++;  // Increment hit counter
        AddReward(hitReward);
        droneController?.ResetDronePosition();

        // Update UI
        if (statsUI != null)
        {
            statsUI.TriggerHitFlash();
            statsUI.UpdateHitStats(successfulHits, totalShots);
        }

        Debug.Log($"🎯 HIT! ({successfulHits}/{totalShots} = {hitAccuracy:F1}%)");
    }

    public void RegisterWallHit()
    {
        wallHits++;  // Increment wall hit counter
        AddReward(wallHitPenalty);

        Debug.Log($"💥 Wall hit ({wallHits} wall hits this episode)");
    }

    private float NormAngle(float deg)
    {
        deg %= 360f;
        return (deg > 180f ? deg - 360f : deg) / 180f;
    }
}
