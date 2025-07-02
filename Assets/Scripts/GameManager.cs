using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Game Settings")]
    [SerializeField] private float startingHealth = 100f;
    [SerializeField] private int maxLives = 5;
    [SerializeField] private float levelTimeLimit = 180f; // 3 minutes
    [SerializeField] private int scoreToWin = 300; // Optional win condition

    [Header("References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private BoatController playerBoat;

    // Game state
    private float currentHealth;
    private int currentScore = 0;
    private int currentLives;
    private bool isGameActive = false;
    private bool isLevelComplete = false;
    private bool isGameOver = false;

    // Singleton pattern
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        // Singleton implementation
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
    }

    private void Start()
    {
        // Initialize game state
        currentHealth = startingHealth;
        currentScore = 0;
        currentLives = maxLives;
        isGameActive = true;
        isLevelComplete = false;
        isGameOver = false;
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateHealth(currentHealth / startingHealth);
            uiManager.UpdateScore(currentScore);
            uiManager.UpdateLives(currentLives);
        }
        
        // Find references if not set
        if (uiManager == null)
        {
            uiManager = FindObjectOfType<UIManager>();
        }
        
        if (playerBoat == null)
        {
            playerBoat = FindObjectOfType<BoatController>();
        }
        
        // Start the game
        StartGame();
    }

    private void StartGame()
    {
        isGameActive = true;
        Time.timeScale = 1.0f;
        
        // Enable player controls
        if (playerBoat != null)
        {
            playerBoat.enabled = true;
        }
    }

    public void PauseGame()
    {
        isGameActive = false;
        Time.timeScale = 0f;
        
        if (uiManager != null)
        {
            uiManager.TogglePause();
        }
    }

    public void ResumeGame()
    {
        isGameActive = true;
        Time.timeScale = 1.0f;
        
        if (uiManager != null)
        {
            uiManager.TogglePause();
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (!isGameActive || isGameOver || isLevelComplete) return;
        
        currentHealth -= damageAmount;
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateHealth(currentHealth / startingHealth);
        }
        
        // Check for game over
        if (currentHealth <= 0)
        {
            GameOver();
        }
    }

    public void AddScore(int points)
    {
        if (!isGameActive || isGameOver || isLevelComplete) return;
        
        currentScore += points;
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateScore(currentScore);
        }
        
        // Optional: Check for score-based win condition
        if (currentScore >= scoreToWin)
        {
            LevelComplete();
        }
    }

    // NEW: Reduce score (for whale collision)
    public void ReduceScore(int points)
    {
        if (!isGameActive || isGameOver || isLevelComplete) return;
        
        currentScore -= points;
        
        // Ensure score doesn't go below 0
        if (currentScore < 0)
        {
            currentScore = 0;
        }
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateScore(currentScore);
        }
    }

    // NEW: Reduce lives (for octopus collision)
    public void ReduceLives(int livesToReduce = 1)
    {
        if (!isGameActive || isGameOver || isLevelComplete) return;
        
        currentLives -= livesToReduce;
        
        // Ensure lives don't go below 0
        if (currentLives < 0)
        {
            currentLives = 0;
        }
        
        // Update UI
        if (uiManager != null)
        {
            uiManager.UpdateLives(currentLives);
        }
        
        // Check for game over
        if (currentLives <= 0)
        {
            GameOver();
        }
    }

    // NEW: Handle treasure collection
    public void CollectTreasure(GameObject treasure)
    {
        AddScore(100); // Fixed 100 points per treasure
        
        // Destroy treasure
        if (treasure != null)
        {
            Destroy(treasure);
        }
        
        Debug.Log("Treasure collected! +100 points");
    }

    // NEW: Handle whale collision
    public void HitWhale(GameObject whale)
    {
        ReduceScore(50); // Lose 50 points
        
        // Destroy whale
        if (whale != null)
        {
            Destroy(whale);
        }
        
        Debug.Log("Hit whale! -50 points");
    }

    // NEW: Handle octopus collision
    public void HitOctopus(GameObject octopus)
    {
        ReduceLives(1); // Lose 1 life
        
        // Destroy octopus
        if (octopus != null)
        {
            Destroy(octopus);
        }
        
        Debug.Log("Hit octopus! -1 life");
    }

    public void GameOver()
    {
        isGameOver = true;
        isGameActive = false;
        
        // Disable player controls
        if (playerBoat != null)
        {
            playerBoat.enabled = false;
        }
        
        // Show game over UI
        if (uiManager != null)
        {
            uiManager.GameOver();
        }
    }

    public void LevelComplete()
    {
        isLevelComplete = true;
        isGameActive = false;
        
        // Disable player controls
        if (playerBoat != null)
        {
            playerBoat.enabled = false;
        }
        
        // Show level complete UI
        if (uiManager != null)
        {
            uiManager.LevelComplete(currentScore);
        }
    }

    public void RestartLevel()
    {
        // Reload current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void LoadMainMenu()
    {
        // Load main menu scene
        SceneManager.LoadScene("MainMenu");
    }

    public void NextLevel()
    {
        // Load next scene
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        
        if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
        {
            SceneManager.LoadScene(nextSceneIndex);
        }
        else
        {
            // No more levels, go back to main menu
            LoadMainMenu();
        }
    }
    
    // Getters for current game state
    public float GetHealthPercent() { return currentHealth / startingHealth; }
    public int GetScore() { return currentScore; }
    public int GetLives() { return currentLives; }
    public int GetMaxLives() { return maxLives; }
    public bool IsGameActive() { return isGameActive; }
    public bool IsGameOver() { return isGameOver; }
    public bool IsLevelComplete() { return isLevelComplete; }
}