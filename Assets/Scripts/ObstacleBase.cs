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
    
    protected AudioSource audioSource;
    
    protected virtual void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && collisionSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    protected virtual void Update()
    {
        if (canMove)
        {
            Move();
        }
    }
    
    protected virtual void Move()
    {
        transform.Translate(moveDirection * moveSpeed * Time.deltaTime);
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
}