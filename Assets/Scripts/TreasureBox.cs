using UnityEngine;

public class TreasureBox : MonoBehaviour
{
    [Header("Treasure Properties")]
    public int pointValue = 100; // Fixed 100 points
    private bool collected = false;
    
    private void Start()
    {
        // Always set to 100 points
        pointValue = 100;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        
        if (other.CompareTag("Player"))
        {
            CollectTreasure();
        }
    }
    
    private void CollectTreasure()
    {
        // Mark as collected
        collected = true;
        
        // Use GameManager's new method
        GameManager gameManager = GameManager.Instance;
        if (gameManager != null)
        {
            gameManager.CollectTreasure(gameObject);
        }
        
        Debug.Log("Treasure collected! +100 points");
    }
}