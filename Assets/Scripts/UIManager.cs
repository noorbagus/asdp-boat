using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI livesText;
    [SerializeField] private TextMeshProUGUI connectionStatusText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider healthBar;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject levelCompletePanel;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    
    [Header("Game Settings")]
    [SerializeField] private float levelTimeLimit = 180f; // 3 minutes
    
    private float currentTime;
    private bool isGameActive = false;
    private bool isPaused = false;
    
    private void Start()
    {
        // Initialize UI elements
        UpdateScore(0);
        UpdateLives(5); // Initialize with max lives
        UpdateConnectionStatus("Disconnected");
        
        if (healthBar != null)
        {
            healthBar.value = 1.0f;
        }
        
        // Hide panels
        if (pausePanel != null) pausePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);
        
        // Start timer
        currentTime = levelTimeLimit;
        isGameActive = true;
        
        // Set time scale
        Time.timeScale = 1.0f;
    }
    
    private void Update()
    {
        if (isGameActive && !isPaused)
        {
            // Update timer
            UpdateTimer();
            
            // Check for pause input
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }
    }
    
    private void UpdateTimer()
    {
        // Decrease time
        currentTime -= Time.deltaTime;
        
        if (currentTime <= 0)
        {
            // Time's up - game over
            currentTime = 0;
            GameOver();
        }
        
        // Update timer display
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentTime / 60);
            int seconds = Mathf.FloorToInt(currentTime % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    
    public void UpdateScore(int score)
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }
    }
    
    public void UpdateLives(int lives)
    {
        if (livesText != null)
        {
            livesText.text = $"{lives}/5";
            
            // Change color based on lives
            if (lives <= 1)
            {
                livesText.color = Color.red;
            }
            else if (lives <= 2)
            {
                livesText.color = Color.yellow;
            }
            else
            {
                livesText.color = Color.white;
            }
        }
    }
    
    public void UpdateHealth(float healthPercent)
    {
        if (healthBar != null)
        {
            healthBar.value = Mathf.Clamp01(healthPercent);
            
            // Change color based on health
            if (healthBar.value < 0.3f)
            {
                healthBar.fillRect.GetComponent<Image>().color = Color.red;
            }
            else if (healthBar.value < 0.6f)
            {
                healthBar.fillRect.GetComponent<Image>().color = Color.yellow;
            }
            else
            {
                healthBar.fillRect.GetComponent<Image>().color = Color.green;
            }
        }
    }
    
    public void UpdateConnectionStatus(string status)
    {
        if (connectionStatusText != null)
        {
            connectionStatusText.text = status;
            
            // Change color based on status
            if (status.Contains("Connected"))
            {
                connectionStatusText.color = Color.green;
            }
            else if (status.Contains("Disconnected") || status.Contains("failed"))
            {
                connectionStatusText.color = Color.red;
            }
            else
            {
                connectionStatusText.color = Color.yellow;
            }
        }
    }
    
    public void TogglePause()
    {
        isPaused = !isPaused;
        
        if (pausePanel != null)
        {
            pausePanel.SetActive(isPaused);
        }
        
        // Set time scale
        Time.timeScale = isPaused ? 0f : 1f;
    }
    
    public void Resume()
    {
        if (isPaused)
        {
            TogglePause();
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
    
    public void GameOver()
    {
        isGameActive = false;
        
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }
        
        // Update final score
        if (finalScoreText != null && scoreText != null)
        {
            finalScoreText.text = "Final Score: " + scoreText.text.Replace("Score: ", "");
        }
        
        // Pause game
        Time.timeScale = 0f;
    }
    
    public void LevelComplete(int finalScore)
    {
        isGameActive = false;
        
        if (levelCompletePanel != null)
        {
            levelCompletePanel.SetActive(true);
        }
        
        // Update final score
        if (finalScoreText != null)
        {
            finalScoreText.text = "Final Score: " + finalScore;
        }
        
        // Pause game
        Time.timeScale = 0f;
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
}