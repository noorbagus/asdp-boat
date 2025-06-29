using UnityEngine;

public abstract class ObstacleBase : MonoBehaviour
{
    [Header("Obstacle Properties")]
    [SerializeField] protected int damageAmount = 10;
    [SerializeField] protected ParticleSystem collisionEffect;
    [SerializeField] protected AudioClip collisionSound;
    [SerializeField] protected float knockbackForce = 500f;
    
    [Header("Movement")]
    [SerializeField] protected bool canMove = false;
    [SerializeField] protected float moveSpeed = 1f;
    [SerializeField] protected Vector3 moveDirection = Vector3.zero;
    
    [Header("Movement Constraints")]
    [SerializeField] protected bool moveX = true;
    [SerializeField] protected bool moveY = false;
    [SerializeField] protected bool moveZ = true;
    
    [Header("Boundary Constraints")]
    [SerializeField] protected float maxDistanceFromOrigin = 50f;
    [SerializeField] protected bool enableBoundaryConstraint = true;
    [SerializeField] protected Vector3 spawnAreaCenter = Vector3.zero;
    [SerializeField] protected Vector3 spawnAreaSize = new Vector3(50f, 1f, 50f);
    
    [Header("Debug")]
    [SerializeField] protected bool enableDebugTracking = false;
    [SerializeField] protected bool enableDebugLogs = true;
    
    protected AudioSource audioSource;
    protected float lastDebugTime = 0f;
    protected float debugInterval = 1f;
    protected Vector3 initialPosition;
    
    protected virtual void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && collisionSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Store initial position
        initialPosition = transform.position;
        
        DebugLog($"{gameObject.name} initialized at position: {initialPosition}");
    }
    
    protected virtual void Update()
    {
        if (canMove)
        {
            Vector3 oldPos = transform.position;
            Move();
            
            // Debug tracking
            if (enableDebugTracking && Time.time - lastDebugTime >= debugInterval)
            {
                DebugLog($"[{gameObject.name}] Pos: {transform.position:F2}, Dir: {moveDirection:F2}, Speed: {moveSpeed:F2}");
                lastDebugTime = Time.time;
            }
        }
    }
    
    protected virtual void Move()
    {
        // Check boundary constraint BEFORE moving
        if (enableBoundaryConstraint)
        {
            float distanceFromCenter = Vector3.Distance(transform.position, spawnAreaCenter);
            
            if (distanceFromCenter > maxDistanceFromOrigin)
            {
                // Teleport back to spawn area
                Vector3 newPos = new Vector3(
                    Random.Range(spawnAreaCenter.x - spawnAreaSize.x * 0.4f, spawnAreaCenter.x + spawnAreaSize.x * 0.4f),
                    spawnAreaCenter.y,
                    Random.Range(spawnAreaCenter.z - spawnAreaSize.z * 0.4f, spawnAreaCenter.z + spawnAreaSize.z * 0.4f)
                );
                
                transform.position = newPos;
                
                // Randomize direction after teleport
                moveDirection = new Vector3(
                    Random.Range(-0.5f, 0.5f),
                    0f,
                    Random.Range(-0.5f, 0.5f)
                ).normalized;
                
                DebugLog($"{gameObject.name} teleported back to spawn area: {newPos}");
                return;
            }
            
            // Check if next move would go out of bounds
            Vector3 nextPosition = transform.position + (moveDirection * moveSpeed * Time.deltaTime);
            float nextDistance = Vector3.Distance(nextPosition, spawnAreaCenter);
            
            if (nextDistance > maxDistanceFromOrigin * 0.9f) // 90% of max distance
            {
                // Reverse direction to stay in bounds
                moveDirection = -moveDirection;
                DebugLog($"{gameObject.name} reversed direction to stay in bounds");
            }
        }
        
        // Apply movement
        Vector3 movement = moveDirection * moveSpeed * Time.deltaTime;
        
        // Apply axis constraints
        if (!moveX) movement.x = 0;
        if (!moveY) movement.y = 0;
        if (!moveZ) movement.z = 0;
        
        transform.Translate(movement);
    }
    
    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            HandlePlayerCollision(collision);
        }
    }
    
    protected virtual void HandlePlayerCollision(Collision collision)
    {
        // Get player's game manager
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.TakeDamage(damageAmount);
        }
        
        // Play effects
        PlayCollisionEffects(collision.contacts[0].point);
        
        // Apply knockback to player
        Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            Vector3 knockbackDirection = collision.transform.position - transform.position;
            knockbackDirection.y = 0; // Keep knockback horizontal
            playerRb.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.Impulse);
        }
    }
    
    protected virtual void PlayCollisionEffects(Vector3 position)
    {
        // Play particle effect
        if (collisionEffect != null)
        {
            ParticleSystem effect = Instantiate(collisionEffect, position, Quaternion.identity);
            Destroy(effect.gameObject, effect.main.duration + 0.5f);
        }
        
        // Play sound
        if (audioSource != null && collisionSound != null)
        {
            audioSource.PlayOneShot(collisionSound);
        }
    }
    
    // Public methods for LevelManager
    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
        DebugLog($"{gameObject.name} movement enabled: {enabled}");
    }
    
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = speed;
        DebugLog($"{gameObject.name} move speed set to: {speed}");
    }
    
    public void SetMoveDirection(Vector3 direction)
    {
        moveDirection = direction;
        DebugLog($"{gameObject.name} move direction set to: {direction}");
    }
    
    public void SetBoundaryConstraints(Vector3 center, Vector3 size, float maxDistance)
    {
        spawnAreaCenter = center;
        spawnAreaSize = size;
        maxDistanceFromOrigin = maxDistance;
        enableBoundaryConstraint = true;
        
        DebugLog($"{gameObject.name} boundary constraints set - Center: {center}, Size: {size}, MaxDist: {maxDistance}");
    }
    
    // Getters
    public int GetDamageAmount() => damageAmount;
    public float GetMoveSpeed() => moveSpeed;
    public bool IsMovementEnabled() => canMove;
    public Vector3 GetMoveDirection() => moveDirection;
    public Vector3 GetInitialPosition() => initialPosition;
    
    // Force position reset
    public void ResetToSpawnArea()
    {
        Vector3 newPos = new Vector3(
            Random.Range(spawnAreaCenter.x - spawnAreaSize.x * 0.4f, spawnAreaCenter.x + spawnAreaSize.x * 0.4f),
            spawnAreaCenter.y,
            Random.Range(spawnAreaCenter.z - spawnAreaSize.z * 0.4f, spawnAreaCenter.z + spawnAreaSize.z * 0.4f)
        );
        
        transform.position = newPos;
        DebugLog($"{gameObject.name} manually reset to spawn area: {newPos}");
    }
    
    protected void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[ObstacleBase] {message}");
        }
    }
    
    // Gizmos for debugging
    private void OnDrawGizmosSelected()
    {
        if (enableBoundaryConstraint)
        {
            // Draw spawn area bounds
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
            
            // Draw max distance circle
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(spawnAreaCenter, maxDistanceFromOrigin);
            
            // Draw current position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
            
            // Draw line to center
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, spawnAreaCenter);
        }
    }
}