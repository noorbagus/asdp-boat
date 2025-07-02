using UnityEngine;

public class TreasureBox : MonoBehaviour
{
    [Header("Treasure Properties")]
    public int pointValue = 100;
    [SerializeField] private ParticleSystem collectEffect;
    [SerializeField] private AudioClip collectSound;
    [SerializeField] private GameObject visualModel;
    [SerializeField] private float rotationSpeed = 30f;
    [SerializeField] private float bobHeight = 0.2f;
    [SerializeField] private float bobSpeed = 1f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    
    private AudioSource audioSource;
    private Vector3 startPosition;
    private bool collected = false;
    
    // Collision debugger reference
    private CollisionDebugger debugger;
    
    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && collectSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        startPosition = transform.position;
        
        // If visualModel is not assigned, use the first child or this object
        if (visualModel == null)
        {
            if (transform.childCount > 0)
            {
                visualModel = transform.GetChild(0).gameObject;
            }
            else
            {
                visualModel = gameObject;
            }
        }
        
        // Find collision debugger
        debugger = FindObjectOfType<CollisionDebugger>();
        
        DebugLog($"TreasureBox initialized at {transform.position} with value {pointValue}");
    }
    
    private void Update()
    {
        if (!collected && visualModel != null)
        {
            // Rotate the treasure
            visualModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
            
            // Bob up and down
            float newY = startPosition.y + (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Register trigger with debugger
        if (debugger != null)
        {
            debugger.RegisterTrigger(GetComponent<Collider>(), other);
        }
        
        DebugLog($"Trigger entered by {other.gameObject.name}, collected: {collected}");
        
        if (collected) return;
        
        if (other.CompareTag("Player") || other.CompareTag("Paddle"))
        {
            // If paddle hit us, try to find the boat
            GameObject player = other.gameObject;
            if (other.CompareTag("Paddle") && other.transform.parent != null)
            {
                player = other.transform.parent.gameObject;
                DebugLog($"Paddle hit, found parent boat: {player.name}");
            }
            
            CollectTreasure(player);
        }
    }
    
    private void CollectTreasure(GameObject player)
    {
        // Mark as collected
        collected = true;
        DebugLog($"Collecting treasure by {player.name}");
        
        // Add score
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.AddScore(pointValue);
            DebugLog($"Added {pointValue} points to score");
        }
        else
        {
            DebugLog("Warning: GameManager not found!");
        }
        
        // Play effects
        PlayCollectEffects();
        
        // Hide visual model
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }
        
        // Disable collider
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.enabled = false;
            DebugLog("Disabled collider");
        }
        
        // Destroy after effects finish
        float destroyDelay = collectEffect != null ? collectEffect.main.duration + 0.5f : 0.5f;
        DebugLog($"Scheduling destruction in {destroyDelay} seconds");
        Destroy(gameObject, destroyDelay);
    }
    
    private void PlayCollectEffects()
    {
        // Play particle effect
        if (collectEffect != null)
        {
            ParticleSystem effect = Instantiate(collectEffect, transform.position, Quaternion.identity);
            effect.Play();
            DebugLog("Playing collect particle effect");
        }
        
        // Play sound
        if (audioSource != null && collectSound != null)
        {
            audioSource.PlayOneShot(collectSound);
            DebugLog("Playing collect sound");
        }
    }
    
    private void DebugLog(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[TreasureBox] {message}");
        }
    }
}