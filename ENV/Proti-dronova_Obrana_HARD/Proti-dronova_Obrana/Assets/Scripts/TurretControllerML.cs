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

    [Header("Movement")]
    public float baseTurnSpeed = 90f;
    public float barrelTurnSpeed = 60f;
    public float minPitch = -85f;
    public float maxPitch = 45f;

    [Header("Shooting")]
    public float muzzleSpeed = 60f;
    public float fireRate = 8f;
    public float projectileLifetime = 5f;

    private float currentPitch = 0f;
    private float nextShotTime = 0f;
    private MLDroneController droneController;

    public override void Initialize()
    {
        if (!turretBase) turretBase = transform.Find("TurretHead");
        if (!barrelPivot) barrelPivot = turretBase?.Find("BarrelsPivot");
        if (!bulletSpawnPoint) bulletSpawnPoint = barrelPivot?.Find("Barrel/BulletSpawnPoint");
        if (targetDrone) droneController = targetDrone.GetComponent<MLDroneController>();
        MaxStep = 5000;
    }

    public override void OnEpisodeBegin()
    {
        currentPitch = 0f;
        nextShotTime = 0f;
        if (turretBase) turretBase.rotation = Quaternion.identity;
        if (barrelPivot) barrelPivot.localRotation = Quaternion.identity;
        droneController?.ResetDronePosition();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Existence penalty
        AddReward(-0.25f / MaxStep);

        // Turret state (6)
        Vector3 e = turretBase ? turretBase.eulerAngles : Vector3.zero;
        sensor.AddObservation(NormAngle(e.y));
        sensor.AddObservation(NormAngle(barrelPivot?.localEulerAngles.x ?? 0f));
        sensor.AddObservation(NormAngle(e.z));
        Vector3 p = transform.position;
        sensor.AddObservation(p / 100f);

        // Drone observations (8)
        if (targetDrone)
        {
            Vector3 rel = turretBase ? turretBase.InverseTransformPoint(targetDrone.transform.position) : targetDrone.transform.position - p;
            sensor.AddObservation(rel / 100f);
            Vector3 v = droneController?.Velocity ?? Vector3.zero;
            sensor.AddObservation(v / 20f);
            sensor.AddObservation(Mathf.Clamp01(Vector3.Distance(p, targetDrone.transform.position) / 200f));
            bool inFront = turretBase && Vector3.Dot(turretBase.forward, (targetDrone.transform.position - p).normalized) > 0.3f;
            sensor.AddObservation(inFront ? 1f : 0f);
            
            // Facing penalty
            if (!inFront) AddReward(-0.5f / MaxStep);
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

        if (turretBase) turretBase.Rotate(Vector3.up, yaw * baseTurnSpeed * Time.deltaTime, Space.Self);
        if (barrelPivot)
        {
            currentPitch = Mathf.Clamp(currentPitch + pitch * barrelTurnSpeed * Time.deltaTime, minPitch, maxPitch);
            barrelPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }

        if (shoot > 0.5f && Time.time >= nextShotTime)
        {
            FireProjectile();
            nextShotTime = Time.time + 1f / fireRate;
            AddReward(-0.5f); // Shooting penalty
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
        }
    }

    private void FireProjectile()
    {
        if (!projectilePrefab || !bulletSpawnPoint) return;
        var bullet = Instantiate(projectilePrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        var rb = bullet.GetComponent<Rigidbody>();
        if (!rb) { Destroy(bullet); return; }
        
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
        AddReward(1f); // Hit reward
        droneController?.ResetDronePosition();
    }

    private float NormAngle(float deg)
    {
        deg %= 360f;
        return (deg > 180f ? deg - 360f : deg) / 180f;
    }
}
