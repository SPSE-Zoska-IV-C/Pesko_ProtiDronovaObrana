using UnityEngine;

public class MLBullet : MonoBehaviour
{
    private TurretAgentML turretAgent;
    private bool hasHit = false;

    public void Setup(TurretAgentML agent) => turretAgent = agent;

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        HandleCollision(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject hitObject)
    {
        string objectName = hitObject.name.ToLower();

        // Hit the drone
        if (hitObject.CompareTag("Drone") || objectName.Contains("drone"))
        {
            hasHit = true;
            Debug.Log("✓ BULLET HIT DRONE!");  // Console feedback
            turretAgent?.RegisterSuccessfulHit();
            Destroy(gameObject);
            return;
        }

        // Hit wall/ground/ceiling
        if (objectName.Contains("wall") ||
            objectName.Contains("ground") ||
            objectName.Contains("ceiling"))
        {
            hasHit = true;
            Debug.Log($"✗ Bullet hit {hitObject.name}");
            turretAgent?.RegisterWallHit();
            Destroy(gameObject);
            return;
        }

        // Hit something else - ignore
    }
}
