using UnityEngine;

public class FinishTrigger : MonoBehaviour
{
    [Header("Finish Settings")]
    [SerializeField] private ParticleSystem completionEffect;
    [SerializeField] private AudioClip completionSound;
    [SerializeField] private float minTimeRequired = 30f; // Minimum time to prevent accidental quick finishes
    
    private float gameStartTime;
    private bool hasTriggered = false;
    private AudioSource audioSource;
    
    private void Start()
    {
        gameStartTime = Time.time;
        
        // Get or add audio source
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && completionSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        // Skip if already triggered or not enough time has passed
        if (hasTriggered || (Time.time - gameStartTime) < minTimeRequired)
            return;
            
        if (other.CompareTag("Player"))
        {
            TriggerLevelComplete();
        }
    }
    
    private void TriggerLevelComplete()
    {
        hasTriggered = true;
        
        // Play effects
        if (completionEffect != null)
        {
            completionEffect.Play();
        }
        
        if (audioSource != null && completionSound != null)
        {
            audioSource.PlayOneShot(completionSound);
        }
        
        // Notify game manager
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.LevelComplete();
        }
        else
        {
            Debug.LogWarning("FinishTrigger: No GameManager found");
        }
    }
}