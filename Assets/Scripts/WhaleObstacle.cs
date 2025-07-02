using UnityEngine;

public class WhaleObstacle : ObstacleBase
{
    [Header("Whale Properties")]
    [SerializeField] private bool hasCollided = false;
    
    protected override void Start()
    {
        base.Start();
        Debug.Log($"Simple Whale initialized at {transform.position}");
    }
    
    protected override void HandlePlayerCollision(Collision collision)
    {
        if (hasCollided) return;
        
        hasCollided = true;
        
        // Standard damage handling
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.TakeDamage(damageAmount);
        }
        
        // Play effects
        PlayCollisionEffects(collision.contacts[0].point);
        
        // Apply knockback
        Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            Vector3 knockbackDirection = collision.transform.position - transform.position;
            knockbackDirection.y = 0;
            playerRb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.Impulse);
        }
        
        Debug.Log("Whale collision with player!");
    }
}