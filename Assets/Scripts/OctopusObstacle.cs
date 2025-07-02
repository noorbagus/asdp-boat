using UnityEngine;
using System.Collections;

public class OctopusObstacle : ObstacleBase
{
    [Header("Octopus Properties")]
    [SerializeField] private float tentacleSpeed = 2f;
    [SerializeField] private float maxTentacleExtension = 3f;
    [SerializeField] private float timeBetweenMovements = 2f;
    
    [Header("Collision Handling")]
    [SerializeField] private bool hasCollided = false;
    
    // Component references
    private Rigidbody octopusRb;
    private Renderer octopusRenderer;
    private Collider octopusCollider;
    private bool isDisappearing = false;
    
    // Collision debugger reference
    private CollisionDebugger debugger;
    
    protected override void Start()
    {
        base.Start();
        
        // Cache components for better performance
        octopusRb = GetComponent<Rigidbody>();
        octopusRenderer = GetComponentInChildren<Renderer>();
        octopusCollider = GetComponent<Collider>();
        
        // Find collision debugger
        debugger = FindObjectOfType<CollisionDebugger>();
        
        Debug.Log($"Octopus initialized at {transform.position}");
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
        Debug.Log($"Octopus collision with {collision.gameObject.name}, velocity: {octopusRb?.velocity}, hasCollided: {hasCollided}");
        
        // Proceed with normal collision handling
        base.OnCollisionEnter(collision);
    }
    
    protected override void HandlePlayerCollision(Collision collision)
    {
        // Prevent multiple collisions
        if (hasCollided)
        {
            Debug.Log("Octopus collision ignored - already collided");
            return;
        }
        
        Debug.Log("Octopus collision handler executing");
        hasCollided = true;
        
        // Apply damage via game manager
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            // Use special octopus method if available
            if (gameManager.GetType().GetMethod("HitOctopus") != null)
            {
                gameManager.SendMessage("HitOctopus", gameObject, SendMessageOptions.DontRequireReceiver);
                Debug.Log("Called HitOctopus on GameManager");
            }
            else
            {
                gameManager.TakeDamage(damageAmount);
                Debug.Log($"Applied damage: {damageAmount}");
            }
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
            Debug.Log($"Applied knockback force: {knockbackDirection.normalized * knockbackForce}");
        }
        
        // Prevent further collisions
        if (octopusCollider != null && collision.collider != null)
        {
            Physics.IgnoreCollision(octopusCollider, collision.collider, true);
            Debug.Log($"Ignored future collisions between octopus and {collision.gameObject.name}");
        }
        
        // Start disappearing effect
        Debug.Log("Starting disappear coroutine");
        StartCoroutine(DisappearAfterCollision());
    }
    
    private IEnumerator DisappearAfterCollision()
    {
        isDisappearing = true;
        Debug.Log("Disappear coroutine started");
        
        // Wait for a short time
        yield return new WaitForSeconds(0.5f);
        
        // Fade out if renderer exists
        if (octopusRenderer != null && octopusRenderer.material.HasProperty("_Color"))
        {
            Color originalColor = octopusRenderer.material.color;
            float fadeOutTime = 1.0f;
            float elapsedTime = 0f;
            
            Debug.Log("Starting fade out animation");
            while (elapsedTime < fadeOutTime)
            {
                elapsedTime += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsedTime / fadeOutTime);
                
                Color newColor = originalColor;
                newColor.a = alpha;
                octopusRenderer.material.color = newColor;
                
                yield return null;
            }
            Debug.Log("Fade out animation completed");
        }
        else
        {
            Debug.LogWarning("Octopus has no renderer or material doesn't support alpha!");
        }
        
        // Disable colliders
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        Debug.Log($"Disabled {colliders.Length} colliders");
        
        // Make rigidbody kinematic to remove from physics
        if (octopusRb != null)
        {
            octopusRb.isKinematic = true;
            Debug.Log("Set Rigidbody to kinematic");
        }
        
        // Destroy game object
        Debug.Log("Scheduling destruction");
        Destroy(gameObject, 0.5f);
    }
    
    // Handle trigger collisions too (not an override since base class doesn't have this)
    protected void OnTriggerEnter(Collider other)
    {
        // Register trigger with debugger
        if (debugger != null)
        {
            debugger.RegisterTrigger(GetComponent<Collider>(), other);
        }
        
        // Check if we should handle this trigger
        bool isBoat = other.CompareTag("Player");
        bool isPaddle = other.CompareTag("Paddle");
        
        if (isBoat || isPaddle)
        {
            Debug.Log($"Octopus trigger with {other.gameObject.name} (isBoat: {isBoat}, isPaddle: {isPaddle})");
            
            // Get contact point
            Vector3 contactPoint = other.ClosestPoint(transform.position);
            
            GameObject collidingObject = other.gameObject;
            
            // If this is a paddle, try to find the boat
            if (isPaddle && other.transform.parent != null)
            {
                collidingObject = other.transform.parent.gameObject;
            }
            
            // Call trigger handler directly with necessary info
            HandleTriggerCollision(other, collidingObject, contactPoint);
        }
    }
    
    private void HandleTriggerCollision(Collider other, GameObject collidingObject, Vector3 contactPoint)
    {
        // Prevent multiple collisions
        if (hasCollided)
        {
            Debug.Log("Octopus trigger ignored - already collided");
            return;
        }
        
        Debug.Log("Octopus trigger handler executing");
        hasCollided = true;
        
        // Apply damage via game manager
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            // Use special octopus method if available
            if (gameManager.GetType().GetMethod("HitOctopus") != null)
            {
                gameManager.SendMessage("HitOctopus", gameObject, SendMessageOptions.DontRequireReceiver);
                Debug.Log("Called HitOctopus on GameManager from trigger");
            }
            else
            {
                gameManager.TakeDamage(damageAmount);
                Debug.Log($"Applied damage from trigger: {damageAmount}");
            }
        }
        
        // Play collision effects
        PlayCollisionEffects(contactPoint);
        
        // Apply knockback to player
        Rigidbody playerRb = collidingObject.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            Vector3 knockbackDirection = collidingObject.transform.position - transform.position;
            knockbackDirection.y = 0; // Keep knockback horizontal
            playerRb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.Impulse);
            Debug.Log($"Applied knockback force from trigger: {knockbackDirection.normalized * knockbackForce}");
        }
        
        // Prevent further collisions
        if (octopusCollider != null)
        {
            Physics.IgnoreCollision(octopusCollider, other, true);
            Debug.Log($"Ignored future collisions between octopus and {collidingObject.name} from trigger");
        }
        
        // Start disappearing effect
        Debug.Log("Starting disappear coroutine from trigger");
        StartCoroutine(DisappearAfterCollision());
    }
}