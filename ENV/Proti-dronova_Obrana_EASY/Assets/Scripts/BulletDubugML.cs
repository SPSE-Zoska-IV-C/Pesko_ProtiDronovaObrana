using UnityEngine;

public class MLBullet : MonoBehaviour
{
    private TurretAgentML turretAgent;
    private bool hasHit;

    // These are now just strings (no CompareTag calls).
    private const string DroneTag = "Drone";
    private const string WallTag = "Wall";
    private const string GroundTag = "Ground";
    private const string CeilingTag = "Ceiling";

    // Keep your existing call working
    public void Setup(TurretAgentML agent)
    {
        Setup(agent, null);
    }

    // Optional: pass turret root to ignore self-collisions
    public void Setup(TurretAgentML agent, Transform shooterRoot)
    {
        turretAgent = agent;

        if (shooterRoot != null)
        {
            var myCols = GetComponentsInChildren<Collider>();
            var shooterCols = shooterRoot.GetComponentsInChildren<Collider>();

            foreach (var my in myCols)
                foreach (var sh in shooterCols)
                {
                    if (my && sh) Physics.IgnoreCollision(my, sh, true);
                }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        HandleHit(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        HandleHit(collision.gameObject);
    }

    private void HandleHit(GameObject hit)
    {
        if (!hit) return;

        var root = hit.transform.root ? hit.transform.root.gameObject : hit;

        if (IsDrone(hit, root))
        {
            hasHit = true;
            turretAgent?.RegisterSuccessfulHit();
            Destroy(gameObject);
            return;
        }

        if (IsEnv(hit, root))
        {
            hasHit = true;
            turretAgent?.RegisterWallHit(); // -3
            Destroy(gameObject);
            return;
        }

        // Do nothing (don’t destroy) so the bullet stays visible if it hits unrelated objects.
    }

    private bool HasTag(GameObject go, string tag)
    {
        // This avoids CompareTag() so it won't log "Tag is not defined."
        // It simply compares strings.
        return go != null && go.tag == tag;
    }

    private bool IsDrone(GameObject hit, GameObject root)
    {
        if (HasTag(hit, DroneTag) || HasTag(root, DroneTag)) return true;

        string n = hit.name.ToLowerInvariant();
        string rn = root.name.ToLowerInvariant();
        return n.Contains("drone") || rn.Contains("drone");
    }

    private bool IsEnv(GameObject hit, GameObject root)
    {
        if (HasTag(hit, WallTag) || HasTag(root, WallTag)) return true;
        if (HasTag(hit, GroundTag) || HasTag(root, GroundTag)) return true;
        if (HasTag(hit, CeilingTag) || HasTag(root, CeilingTag)) return true;

        string n = hit.name.ToLowerInvariant();
        string rn = root.name.ToLowerInvariant();
        return n.Contains("wall") || rn.Contains("wall")
            || n.Contains("ground") || rn.Contains("ground")
            || n.Contains("ceiling") || rn.Contains("ceiling");
    }
}
