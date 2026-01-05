using UnityEngine;

public class MLBullet : MonoBehaviour
{
    private TurretAgentML turretAgent;
    private bool hasHitTarget = false;

    public void Setup(TurretAgentML agent) => turretAgent = agent;

    private void OnTriggerEnter(Collider other)
    {
        if (!hasHitTarget && other.CompareTag("Drone"))
        {
            hasHitTarget = true;
            turretAgent?.RegisterSuccessfulHit();
            Destroy(gameObject);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!hasHitTarget && collision.gameObject.CompareTag("Drone"))
        {
            hasHitTarget = true;
            turretAgent?.RegisterSuccessfulHit();
            Destroy(gameObject);
        }
    }
}
