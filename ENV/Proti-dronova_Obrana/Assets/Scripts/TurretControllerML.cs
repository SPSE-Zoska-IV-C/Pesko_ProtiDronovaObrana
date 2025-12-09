using UnityEngine;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class TurretAgentML : Agent
{
    [Header("References")]
    public Transform turretBase;      // yaw parent
    public Transform barrelPivot;     // pitch parent
    public Transform bulletSpawnPoint;// muzzle
    public GameObject projectilePrefab; // Rigidbody + Collider

    [Header("Movement")]
    public float baseTurnSpeed = 90f;
    public float barrelTurnSpeed = 60f;
    public float minPitch = -85f;
    public float maxPitch = 45f;

    [Header("Shooting")]
    public float muzzleSpeed = 60f;
    public float fireRate = 8f;
    public float projectileLifetime = 5f;

    [Header("Rewards/Penalties")]
    public float existencePenalty = -0.001f;
    public float shootingPenalty = -0.01f;
    public float hitReward = 1.0f;

    [Header("Manual test (optional)")]
    public bool enableManualControl = true; // Set true to enable spacebar shooting

    // Internal state
    private float currentPitch = 0f;
    private float nextShotTime = 0f;
    private MLDroneSpawner droneSpawner;

    // Observation sizing (constant 87)
    private const int MaxDrones = 10;          // up to 10 drones
    private const int PerDroneObs = 8;         // relPos(3)+vel(3)+dist(1)+inFront(1)
    private const int StaticObs = 3 + 3 + 1;   // rot(3)+pos(3)+count(1)
    private const int ObsSize = StaticObs + MaxDrones * PerDroneObs; // 87

    public override void Initialize()
    {
        if (!turretBase) turretBase = FindInChildren(transform, "TurretHead");
        if (!barrelPivot && turretBase) barrelPivot = FindInChildren(turretBase, "BarrelsPivot");

        if (!bulletSpawnPoint)
        {
            string[] names = { "BulletSpawnPoint", "SpawnPoint", "Muzzle", "BarrelEnd" };
            foreach (var n in names)
            {
                bulletSpawnPoint = FindInChildren(transform, n);
                if (bulletSpawnPoint) break;
            }
            if (!bulletSpawnPoint)
            {
                var go = new GameObject("BulletSpawnPoint");
                go.transform.SetParent(barrelPivot ? barrelPivot : transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, 2f);
                go.transform.localRotation = Quaternion.identity;
                bulletSpawnPoint = go.transform;
            }
        }
        droneSpawner = FindAnyObjectByType<MLDroneSpawner>();
        MaxStep = 5000;
    }

    public override void OnEpisodeBegin()
    {
        currentPitch = 0f;
        nextShotTime = 0f;
        if (turretBase) turretBase.rotation = Quaternion.identity;
        if (barrelPivot) barrelPivot.localRotation = Quaternion.Euler(0f, 0f, 0f);
        droneSpawner?.ResetAllDrones();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AddReward(existencePenalty);

        Vector3 e = turretBase ? turretBase.eulerAngles : Vector3.zero;
        sensor.AddObservation(NormAngle(e.y));
        sensor.AddObservation(NormAngle(barrelPivot ? barrelPivot.localEulerAngles.x : 0f));
        sensor.AddObservation(NormAngle(e.z));

        Vector3 p = transform.position;
        sensor.AddObservation(p.x / 100f);
        sensor.AddObservation(p.y / 100f);
        sensor.AddObservation(p.z / 100f);

        var drones = droneSpawner?.ActiveDrones ?? new List<GameObject>();
        sensor.AddObservation(Mathf.Clamp01(drones.Count / 10f));

        int used = 0;
        foreach (var d in drones)
        {
            if (used >= MaxDrones) break;
            var ctrl = d ? d.GetComponent<MLDroneController>() : null;
            Vector3 rel = turretBase
                ? turretBase.InverseTransformPoint(d.transform.position)
                : d.transform.position - p;
            sensor.AddObservation(rel.x / 100f);
            sensor.AddObservation(rel.y / 100f);
            sensor.AddObservation(rel.z / 100f);

            Vector3 v = ctrl ? ctrl.Velocity : Vector3.zero;
            sensor.AddObservation(v.x / 20f);
            sensor.AddObservation(v.y / 20f);
            sensor.AddObservation(v.z / 20f);

            float dist = Vector3.Distance(p, d.transform.position);
            sensor.AddObservation(Mathf.Clamp01(dist / 200f));

            bool inFront = turretBase
                ? Vector3.Dot(turretBase.forward, (d.transform.position - p).normalized) > 0.3f
                : false;
            sensor.AddObservation(inFront ? 1f : 0f);
            used++;
        }

        int count = StaticObs + used * PerDroneObs;
        for (int i = count; i < ObsSize; i++) sensor.AddObservation(0f);
    }

    // Continuous Actions: [0] = yaw [-1:1], [1] = pitch [-1:1], [2] = shoot [0=don't shoot, 1=shoot]
    public override void OnActionReceived(ActionBuffers actions)
    {
        var ca = actions.ContinuousActions;
        float yawInput   = Mathf.Clamp(ca.Length > 0 ? ca[0] : 0f, -1f, 1f);
        float pitchInput = Mathf.Clamp(ca.Length > 1 ? ca[1] : 0f, -1f, 1f);
        float shoot      = ca.Length > 2 ? ca[2] : 0f;

        // Apply yaw
        if (turretBase)
            turretBase.Rotate(Vector3.up, yawInput * baseTurnSpeed * Time.deltaTime, Space.Self);

        // Apply pitch
        if (barrelPivot)
        {
            currentPitch = Mathf.Clamp(currentPitch + pitchInput * barrelTurnSpeed * Time.deltaTime, minPitch, maxPitch);
            barrelPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
        }

        // Fire when shoot > 0.5 and enough time has passed
        if (shoot > 0.5f && Time.time >= nextShotTime)
        {
            FireProjectile();
            nextShotTime = Time.time + 1f / Mathf.Max(1e-3f, fireRate);
            AddReward(shootingPenalty);
        }
        GrantOrientationBonus();
    }

    // For manual keyboard testing (ignored during training)
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var ca = actionsOut.ContinuousActions;

        // Manual test enabled?
        if (!enableManualControl)
        {
            for (int i = 0; i < ca.Length; i++) ca[i] = 0f;
            return;
        }

        var kb = UnityEngine.InputSystem.Keyboard.current;
        float yaw = 0f, pitch = 0f, shoot = 0f;

        if (kb != null)
        {
            yaw   = (kb.aKey.isPressed ? -1f : 0f) + (kb.dKey.isPressed ? 1f : 0f);
            pitch = (kb.wKey.isPressed ? 1f : 0f)  + (kb.sKey.isPressed ? -1f : 0f);
            shoot = kb.spaceKey.isPressed ? 1f : 0f;   // **Spacebar for shooting**
        }
        ca[0] = Mathf.Clamp(yaw, -1f, 1f);
        ca[1] = Mathf.Clamp(pitch, -1f, 1f);
        ca[2] = shoot;
    }

    // Bonus for facing drones
    private void GrantOrientationBonus()
    {
        var drones = droneSpawner?.ActiveDrones;
        if (drones == null || drones.Count == 0 || !turretBase) return;
        int inFront = 0;
        Vector3 tp = transform.position;
        foreach (var d in drones)
        {
            if (!d) continue;
            Vector3 dir = (d.transform.position - tp).normalized;
            if (Vector3.Dot(turretBase.forward, dir) > 0.3f) inFront++;
        }
        float bonus = -0.001f * (1f - (float)inFront / drones.Count);
        AddReward(bonus);
    }

    private void FireProjectile()
    {
        if (!projectilePrefab || !bulletSpawnPoint) return;
        var bullet = Instantiate(projectilePrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        var rb = bullet.GetComponent<Rigidbody>();
        if (!rb)
        {
            Debug.LogError("Bullet prefab has no Rigidbody!");
            Destroy(bullet);
            return;
        }

        // Setup bullet script
        var mlb = bullet.GetComponent<MLBullet>();
        if (!mlb) mlb = bullet.AddComponent<MLBullet>();
        mlb.Setup(this);

        rb.linearVelocity = bulletSpawnPoint.forward * muzzleSpeed; // Unity 6 API
        rb.angularVelocity = Vector3.zero;
        rb.useGravity = false;
        Destroy(bullet, projectileLifetime);
    }

    public void RegisterSuccessfulHit()
    {
        AddReward(hitReward);
        // Optionally EndEpisode when all drones gone
        // if (droneSpawner != null && droneSpawner.ActiveDrones.Count == 0) EndEpisode();
    }

    private float NormAngle(float deg)
    {
        deg %= 360f;
        if (deg > 180f) deg -= 360f;
        return deg / 180f;
    }

    private Transform FindInChildren(Transform parent, string name)
    {
        if (!parent) return null;
        if (parent.name == name) return parent;
        foreach (Transform c in parent)
        {
            if (c.name == name) return c;
            var r = FindInChildren(c, name);
            if (r) return r;
        }
        return null;
    }
}
