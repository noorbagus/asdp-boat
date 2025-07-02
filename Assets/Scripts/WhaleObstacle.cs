using UnityEngine;
using System.Collections;

public class WhaleObstacle : ObstacleBase
{
    [Header("Collision Handling")]
    [SerializeField] private bool hasCollided = false;
    [SerializeField] private float bounceForce = 8f;
    [SerializeField] private float boatBounceMultiplier = 1.2f;
    [SerializeField] private float whaleBounceMultiplier = 0.8f;
    [SerializeField] private float fadeOutTime = 1.5f;
    
    // Component references
    private Rigidbody whaleRb;
    private Renderer whaleRenderer;
    private Collider whaleCollider;
    private bool isDisappearing = false;
    
    // Collision debugger reference
    private CollisionDebugger debugger;
    
    protected override void Start()
    {
        base.Start();
        
        // Cache components for better performance
        whaleRb = GetComponent<Rigidbody>();
        whaleRenderer = GetComponentInChildren<Renderer>();
        whaleCollider = GetComponent<Collider>();
        
        // Find collision debugger
        debugger = FindObjectOfType<CollisionDebugger>();
        
        Debug.Log($"Whale initialized at {transform.position}");
    }
    
    protected override void Update()
    {
        // Early exit if already handled collision
        if (hasCollided || isDisappearing) return;
        
        // Other update logic handled by parent class
        base.Update();
    }
    
    protected override void OnCollisionEnter(Collision collision)
    {
        // Register collision with debugger
        if (debugger != null)
        {
            debugger.RegisterCollision(collision);
        }
        
        // Log extended collision data
        Debug.Log($"Whale collision with {collision.gameObject.name}, velocity: {whaleRb?.velocity}, hasCollided: {hasCollided}");
        
        // Proceed with normal collision handling
        base.OnCollisionEnter(collision);
    }
    
    protected override void HandlePlayerCollision(Collision collision)
    {
        // Prevent multiple collisions
        if (hasCollided)
        {
            Debug.Log("Whale collision ignored - already collided");
            return;
        }
        
        Debug.Log("Whale collision handler executing");
        hasCollided = true;
        
        // Apply damage via game manager
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.TakeDamage(damageAmount);
            Debug.Log($"Applied damage: {damageAmount}");
        }
        
        // Play collision effects
        PlayCollisionEffects(collision.contacts[0].point);
        
        // Calculate bounce direction
        Vector3 bounceDirection = (transform.position - collision.transform.position).normalized;
        bounceDirection.y = 0.2f; // Slight upward component
        
        // Apply bounce to whale
        if (whaleRb != null)
        {
            whaleRb.velocity = Vector3.zero; // Reset velocity
            whaleRb.AddForce(bounceDirection * bounceForce * whaleBounceMultiplier, ForceMode.Impulse);
            Debug.Log($"Applied whale bounce force: {bounceDirection * bounceForce * whaleBounceMultiplier}");
        }
        else
        {
            Debug.LogWarning("Whale has no Rigidbody component!");
        }
        
        // Apply bounce to boat
        Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            Vector3 playerBounceDir = -bounceDirection;
            playerBounceDir.y = 0; // Keep player bounce horizontal
            playerRb.AddForce(playerBounceDir * bounceForce * boatBounceMultiplier, ForceMode.Impulse);
            Debug.Log($"Applied boat bounce force: {playerBounceDir * bounceForce * boatBounceMultiplier}");
        }
        
        // Prevent further collisions
        if (whaleCollider != null && collision.collider != null)
        {
            Physics.IgnoreCollision(whaleCollider, collision.collider, true);
            Debug.Log($"Ignored future collisions between whale and {collision.gameObject.name}");
        }
        
        // Start disappearing effect
        Debug.Log("Starting disappear coroutine");
        StartCoroutine(DisappearAfterCollision());
    }
    
    private IEnumerator DisappearAfterCollision()
    {
        isDisappearing = true;
        Debug.Log("Disappear coroutine started");
        
        // Wait for bounce to complete
        yield return new WaitForSeconds(0.8f);
        Debug.Log("Bounce animation completed");
        
        // Fade out if renderer exists
        if (whaleRenderer != null && whaleRenderer.material.HasProperty("_Color"))
        {
            Color originalColor = whaleRenderer.material.color;
            float elapsedTime = 0f;
            
            Debug.Log("Starting fade out animation");
            while (elapsedTime < fadeOutTime)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeOutTime);
                
                Color newColor = originalColor;
                newColor.a = alpha;
                whaleRenderer.material.color = newColor;
                
                yield return null;
            }
            Debug.Log("Fade out animation completed");
        }
        else
        {
            Debug.LogWarning("Whale has no renderer or material doesn't support alpha!");
        }
        
        // Disable colliders
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        Debug.Log($"Disabled {colliders.Length} colliders");
        
        // Make rigidbody kinematic to remove from physics
        if (whaleRb != null)
        {
            whaleRb.isKinematic = true;
            Debug.Log("Set Rigidbody to kinematic");
        }
        
        // Destroy game object
        Debug.Log("Scheduling destruction");
        Destroy(gameObject, 0.5f);
    }
}