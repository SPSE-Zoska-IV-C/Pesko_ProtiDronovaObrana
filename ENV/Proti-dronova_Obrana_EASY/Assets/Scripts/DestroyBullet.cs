using UnityEngine;

public class WallBulletDestroyer : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        // Check if the object that hit the wall is a bullet
        if (collision.gameObject.name.Contains("Bullet(Clone)"))
        {
            Destroy(collision.gameObject);
        }
    }
}
