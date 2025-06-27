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
    
    private AudioSource audioSource;
    private Vector3 startPosition;
    private bool collected = false;
    
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
        if (collected) return;
        
        if (other.CompareTag("Player"))
        {
            CollectTreasure(other.gameObject);
        }
    }
    
    private void CollectTreasure(GameObject player)
    {
        // Mark as collected
        collected = true;
        
        // Add score
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.AddScore(pointValue);
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
        }
        
        // Destroy after effects finish
        Destroy(gameObject, collectEffect != null ? collectEffect.main.duration + 0.5f : 0.5f);
    }
    
    private void PlayCollectEffects()
    {
        // Play particle effect
        if (collectEffect != null)
        {
            ParticleSystem effect = Instantiate(collectEffect, transform.position, Quaternion.identity);
            effect.Play();
        }
        
        // Play sound
        if (audioSource != null && collectSound != null)
        {
            audioSource.PlayOneShot(collectSound);
        }
    }
}