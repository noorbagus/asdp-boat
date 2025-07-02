using UnityEngine;

public class OctopusObstacle : ObstacleBase
{
    [Header("Octopus Properties")]
    private bool hasCollided = false; // Prevent multiple collisions
    
    protected override void Start()
    {
        base.Start();
        
        Debug.Log($"Simple Octopus initialized at {transform.position}");
    }
    
    // OVERRIDE: Custom collision handling for octopus
    protected override void HandlePlayerCollision(Collision collision)
    {
        if (hasCollided) return; // Prevent multiple collisions
        
        hasCollided = true;
        
        // Use GameManager's octopus collision method
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.HitOctopus(gameObject);
        }
        
        // Play collision effects
        PlayCollisionEffects(collision.contacts[0].point);
        
        // Apply knockback to player
        Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            Vector3 knockbackDirection = collision.transform.position - transform.position;
            knockbackDirection.y = 0; // Keep knockback horizontal
            playerRb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.Impulse);
        }
        
        Debug.Log("Octopus collision with player - lives reduced!");
    }
}