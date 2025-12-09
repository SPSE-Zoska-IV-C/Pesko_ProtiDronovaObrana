using UnityEngine;

public class BulletHit : MonoBehaviour
{
    private void Start()
    {
        // Ensure bullet has proper setup
        if (GetComponent<Rigidbody>() == null)
        {
            gameObject.AddComponent<Rigidbody>().isKinematic = true;
        }
        
        // Ensure there's a collider
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<SphereCollider>().isTrigger = true;
        }
        
        // Set tag if not set
        if (!gameObject.CompareTag("Bullet"))
        {
            gameObject.tag = "Bullet";
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Drone") || other.CompareTag("Enemy"))
        {
            Debug.Log("🎯 Bullet hit: " + other.name);
            Destroy(gameObject);
        }
    }
}